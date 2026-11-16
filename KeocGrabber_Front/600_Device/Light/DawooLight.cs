﻿/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 *******************************************************************************
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace KeocGrabber_Front
{
    //! 다우시스템 50W LED SPOT CONTROLLER. (DLC-050-2-3500-B02, 2CH)  2026/11/16
    //  - RS-232C, 9600, 8, None, 1.
    //  - 송신 : 0xFF | Command | Channel | Parameter(1~2) | Checksum
    //    Checksum = Header ^ Command ^ Channel ^ Parameter.
    //  - 채널 : ch1 = 0x01, ch2 = 0x02, 전체 = 0x03. (비트 마스크)
    //  - 밝기 : 0 ~ 3500. (2byte, 상위 바이트 먼저)
    //  - 모든 명령에 응답이 오며, 1000ms 이상 무응답이면 타임아웃으로 판단.
    //  - 응답 : 정상 0xFF 0x00 0xFF / 에러 0xFF <에러코드> <Checksum>
    //           조회 0xFF <Command> <Data...> <Checksum>
    //  - 컨트롤러가 LOCAL 모드면 모든 명령이 0x80 에러로 거부되므로 REMOTE 모드로 두어야 함.
    //  - 조명 온도 70도 도달 시 컨트롤러가 출력을 자동 차단하며, ON 명령 재전송으로 복귀.
    //  - 기존 3채널 다우 컨트롤러용 코드를 DLC-050 사양으로 재작성.
    class DawooLight : ILightController
    {
        SerialComm m_comm = new SerialComm();

        public bool IsConnected { get { return m_comm.IsConnected; } }

        const byte HEADER       = 0xFF;
        const byte CMD_DIMMING  = 0x01;
        const byte CMD_ONOFF    = 0x02;
        const byte CMD_RD_DIM   = 0x11;
        const byte CMD_RD_ONOFF = 0x12;

        const int MAX_CHANNEL = 2;
        const byte CH_ALL = 0x03;
        const int MAX_VALUE = 3500;

        const int RECV_TIMEOUT = 1000;

        bool[] m_bOnoff = new bool[MAX_CHANNEL];
        int[] m_nLightValue = new int[MAX_CHANNEL];

        List<byte> m_bufRecv = new List<byte>();
        readonly object m_lockQuery = new object();     // 조회 명령 동시 실행 방지.
        volatile byte m_nWaitCmd = 0x00;                // 응답 대기 중인 조회 명령.
        volatile bool m_bQueryError = false;            // 조회 명령에 에러 응답 수신.

        public void fn_InitPort()
        {
            bool bRet = m_comm.Open(G.SYSTEM.LightPort, 9600, 8, Parity.None, StopBits.One);
            m_comm.ReciveDataevent = RecvData;
            m_bufRecv = new List<byte>();

            G.WriteLog($"Light Controller(DAWOO) Open {(bRet ? "Succ." : "Fail.")}. [{m_comm.ConnPort}]");
        }

        public void fn_FinalPort()
        {
            bool bRet = m_comm.Close();
            m_comm.ReciveDataevent = null;
            G.WriteLog($"Light Controller(DAWOO) Close {(bRet ? "Succ." : "Fail.")}. [{m_comm.ConnPort}]");
        }

        /// <summary>
        /// 채널 번호를 컨트롤러 채널 코드로 변환.
        /// </summary>
        /// <param name="ch">0 : 전체, 1~2 : 채널</param>
        /// <returns>채널 코드. 지원하지 않는 채널이면 0.</returns>
        private static byte fn_ChannelCode(int ch)
        {
            if (ch == 0) return CH_ALL;
            if (ch >= 1 && ch <= MAX_CHANNEL) return (byte)(1 << (ch - 1));
            return 0;
        }

        /// <summary>
        /// Header와 Checksum을 붙여 명령 전송.
        /// </summary>
        private bool fn_Send(string strDesc, params byte[] body)
        {
            byte[] buff = new byte[body.Length + 2];
            buff[0] = HEADER;
            byte chk = HEADER;
            for (int i = 0; i < body.Length; i++)
            {
                buff[i + 1] = body[i];
                chk ^= body[i];
            }
            buff[buff.Length - 1] = chk;

            bool bRet = m_comm.WriteValue(buff, false, false);
            if (!bRet)
                G.WriteLog($"DAWOO {strDesc} 전송 실패. 조명 컨트롤러 통신 확인 필요. [{m_comm.ConnPort}]", true);
            return bRet;
        }

        /// <summary>
        /// 밝기 설정.
        /// </summary>
        /// <param name="ch">0 : 전체, 1~2 : 채널. 그 외 채널은 무시.</param>
        /// <param name="value">밝기 [0~3500]. 범위를 벗어나면 경계값으로 제한.</param>
        public void SetLightValue(int ch, int value)
        {
            // 레시피의 3, 4채널 등 컨트롤러에 없는 채널은 무시.
            byte code = fn_ChannelCode(ch);
            if (code == 0)
                return;

            int nValue = Math.Max(0, Math.Min(MAX_VALUE, value));
            if (nValue != value)
                G.WriteLog($"DAWOO 밝기 범위 초과. [{value}] → {nValue} (0~{MAX_VALUE})");

            if (fn_Send("Set", CMD_DIMMING, code, (byte)(nValue >> 8), (byte)(nValue & 0xFF)))
                G.WriteLog($"DAWOO Set : {(ch == 0 ? "All" : ch.ToString())} ch, {nValue}");
        }

        public void LightOn(int ch)
        {
            byte code = fn_ChannelCode(ch);
            if (code == 0)
                return;
            if (fn_Send("Light ON", CMD_ONOFF, code, 0x01))
                G.WriteLog($"DAWOO {(ch == 0 ? "All" : ch.ToString())} ch Light ON");
        }

        public void LightOff(int ch)
        {
            byte code = fn_ChannelCode(ch);
            if (code == 0)
                return;
            if (fn_Send("Light OFF", CMD_ONOFF, code, 0x00))
                G.WriteLog($"DAWOO {(ch == 0 ? "All" : ch.ToString())} ch Light OFF");
        }

        /// <summary>
        /// 조회 명령 전송 후 응답 대기.
        /// </summary>
        /// <returns>응답 수신 여부.</returns>
        private bool fn_Query(byte cmd)
        {
            if (!m_comm.IsConnected)
                return false;

            lock (m_lockQuery)
            {
                m_bQueryError = false;
                m_nWaitCmd = cmd;
                if (!fn_Send($"Read(0x{cmd:X2})", cmd))
                {
                    m_nWaitCmd = 0x00;
                    return false;
                }

                // 전송 스레드 대기 시간을 고려하여 응답 시간에 여유를 둠.
                Stopwatch sw = Stopwatch.StartNew();
                while (m_nWaitCmd != 0x00)
                {
                    Thread.Sleep(20);
                    if (sw.ElapsedMilliseconds > RECV_TIMEOUT + 500)
                    {
                        m_nWaitCmd = 0x00;
                        G.WriteLog($"DAWOO Time-Out. [Read 0x{cmd:X2}]", true);
                        return false;
                    }
                }
                return !m_bQueryError;
            }
        }

        /// <summary>
        /// 밝기 조회.
        /// </summary>
        /// <param name="ch">0 : 1채널 값, 1~2 : 채널</param>
        public int? GetLightValue(int ch)
        {
            int nIdx = ch == 0 ? 0 : ch - 1;
            if (nIdx < 0 || nIdx >= MAX_CHANNEL)
                return null;
            if (!fn_Query(CMD_RD_DIM))
                return null;

            G.WriteLog($"DAWOO Get : ch1 {m_nLightValue[0]}, ch2 {m_nLightValue[1]}");
            return m_nLightValue[nIdx];
        }

        public bool GetLightOnOff()
        {
            return fn_Query(CMD_RD_ONOFF);
        }

        public bool IsLightOn(int ch)
        {
            if (ch == 0)
                return m_bOnoff[0] && m_bOnoff[1];
            if (ch >= 1 && ch <= MAX_CHANNEL)
                return m_bOnoff[ch - 1];
            return false;
        }

        /// <summary>
        /// 응답 길이. (Header, Checksum 포함) 알 수 없는 응답이면 0.
        /// </summary>
        private static int fn_FrameLength(byte second)
        {
            switch (second)
            {
                case CMD_RD_DIM:   return 7;    // FF 11 ch1H ch1L ch2H ch2L chk
                case CMD_RD_ONOFF: return 4;    // FF 12 data chk
            }
            if (second == 0x00 || (second >= 0x80 && second <= 0x85))
                return 3;                       // FF 상태 chk
            return 0;
        }

        private static string fn_ErrorText(byte code)
        {
            switch (code)
            {
                case 0x80: return "LOCAL MODE (컨트롤러를 REMOTE 모드로 전환 필요)";
                case 0x81: return "channel error";
                case 0x82: return "command error";
                case 0x83: return "parameter error";
                case 0x84: return "checksum error";
                case 0x85: return "over time error";
            }
            return "unknown";
        }

        private void RecvData(byte[] buff)
        {
            m_bufRecv.AddRange(buff);

            while (m_bufRecv.Count > 0)
            {
                // Header까지 앞부분 버림.
                int nHead = m_bufRecv.IndexOf(HEADER);
                if (nHead < 0) { m_bufRecv.Clear(); return; }
                if (nHead > 0) m_bufRecv.RemoveRange(0, nHead);
                if (m_bufRecv.Count < 2) return;

                int nLen = fn_FrameLength(m_bufRecv[1]);
                if (nLen == 0) { m_bufRecv.RemoveAt(0); continue; }
                if (m_bufRecv.Count < nLen) return;

                // Checksum 확인. 맞지 않으면 1byte 밀어서 다시 동기화.
                byte chk = 0x00;
                for (int i = 0; i < nLen - 1; i++)
                    chk ^= m_bufRecv[i];
                if (chk != m_bufRecv[nLen - 1]) { m_bufRecv.RemoveAt(0); continue; }

                byte[] frame = m_bufRecv.GetRange(0, nLen).ToArray();
                m_bufRecv.RemoveRange(0, nLen);
                fn_ParseFrame(frame);
            }
        }

        private void fn_ParseFrame(byte[] frame)
        {
            byte second = frame[1];
            switch (second)
            {
                case 0x00:
                    break;  // 정상 수신.
                case CMD_RD_DIM:
                    m_nLightValue[0] = (frame[2] << 8) | frame[3];
                    m_nLightValue[1] = (frame[4] << 8) | frame[5];
                    break;
                case CMD_RD_ONOFF:
                    m_bOnoff[0] = (frame[2] & 0x01) != 0;
                    m_bOnoff[1] = (frame[2] & 0x02) != 0;
                    break;
                default:
                    // 에러 응답. 조회 중이었다면 대기 해제.
                    G.WriteLog($"DAWOO Error Response. [0x{second:X2} {fn_ErrorText(second)}]", true);
                    if (m_nWaitCmd != 0x00)
                    {
                        m_bQueryError = true;
                        m_nWaitCmd = 0x00;
                    }
                    return;
            }

            if (second == m_nWaitCmd)
                m_nWaitCmd = 0x00;
        }
    }
}
