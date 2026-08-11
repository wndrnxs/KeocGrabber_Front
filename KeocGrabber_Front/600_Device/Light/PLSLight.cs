/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 *******************************************************************************
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KeocGrabber_Front
{
    internal class PLSLight
    {
        SerialComm serialComm = new SerialComm();

        public bool IsConnected { get { return serialComm.IsConnected; } }

        int m_nRecieveTimeout = 3000;

        bool m_bIsRecv = false;

        const int MAX_CHANNEL = 8;
        bool[] m_bOnoff = new bool[MAX_CHANNEL];
        int[] m_nLightValue = new int[MAX_CHANNEL];

        List<byte> m_bufRecvBuff = new List<byte>();

        public PLSLight()
        {
            //comm.Open
        }

        /// <summary>
        /// 정면 시야각 PLS 조명 컨트롤러 포트 열기.
        /// 포트 정보는 시스템 파라메터에서 받아옴.
        /// </summary>
        public void fn_InitPort()
        {
            bool bRet = false;

            bRet = serialComm.Open(G.SYSTEM.LightPort, 115200, 8, Parity.None, StopBits.One);

            serialComm.ReciveDataevent = RecvData1;

            m_bIsRecv = false;

            m_bufRecvBuff = new List<byte>();

            G.WriteLog($"Light Controller Open {(bRet ? "Succ." : "Fail.")}. Bottom : {serialComm.ConnPort}");
        }

        /// <summary>
        /// Vit 조명 컨트롤러 포트 해제.
        /// </summary>
        public void fn_FinalPort()
        {
            bool bRet = false;
            bRet = serialComm.Close();
            G.WriteLog($"Light Controller Open {(bRet ? "Succ." : "Fail.")}. [{serialComm.ConnPort}]");
        }

        /// <summary>
        /// Vit Light 값 변경
        /// </summary>
        /// <param name="idx">채널 인덱스 [0 : 전체, 1~4 : 채널]</param>
        /// <param name="value">조명 값 [0~255]</param>
        public void SetLightValue(int value)
        {
            // *W000123$
            // * : Start
            // W : 조명값 설정 명령
            // 00 : 채널 ( 0: 전체, 1~8 : 채널)
            // 0123 : 조명 값.
            // $ : End
            
            string strcmd = $"*W00{value:D4}$";
            serialComm.WriteValue(Encoding.ASCII.GetBytes(strcmd), false, false);
            
            G.WriteLog($"PLS Set : {value}");
        }
        /// <summary>
        /// Vit Light 값 변경
        /// </summary>
        /// <param name="ch">컨트롤러 채널 [1~16]</param>
        /// <param name="value">컨트롤러 전체 조명 값 [0~255]</param>
        public void SetLightValue(int ch, int value)
        {
            // *W000123$
            // * : Start
            // W : 조명값 설정 명령
            // 00 : 채널 ( 0: 전체, 1~8 : 채널)
            // 0123 : 조명 값.
            // $ : End
            if (ch > -1 && ch <= MAX_CHANNEL)
            {
                string strcmd = $"*W{ch:X2}{value:D4}$";
                serialComm.WriteValue(Encoding.ASCII.GetBytes(strcmd), false, false);

                G.WriteLog($"PLS Set : {ch}, {value}");
            }
        }

        public int? GetLightValue(int ch)
        {
            int? value = null;
            if (ch > -1 && ch <= MAX_CHANNEL)
            {
                if (!serialComm.IsConnected)
                    return null;
                m_bIsRecv = false;
                // R0DAT\r\0 : 데이터 값 요청.
                // R0ONF\r\0 : ON/OFF 요청.
                // R : 조명값 설정 명령
                // 0 : 채널
                // DAT : 조명 값.
                string strcmd = $"*R01$";
                serialComm.WriteValue(Encoding.ASCII.GetBytes(strcmd),false,false);

                if (fn_CheckTimeOut() == -1)
                {
                    G.WriteLog($"PLS Time-Out");
                }
                else
                {
                    value = m_nLightValue[ch];
                    G.WriteLog($"PLS {ch} Get : {value}");
                }
            }
            return value;
        }

        public bool GetLightOnOff()
        {
            if (!serialComm.IsConnected)
                return false;
            bool bRet = true;
            m_bIsRecv = false;

            string strcmd = $"*C01$";
            serialComm.WriteValue(Encoding.ASCII.GetBytes(strcmd), false, false);
            if (fn_CheckTimeOut() == -1)
            {
                G.WriteLog($"PLS Time-Out");
                bRet = false;
            }

            return bRet;
        }

        /// <summary>
        /// PLS Light On
        /// </summary>
        /// <param name="idx">채널 인덱스 [0~4]</param>
        public void LightOn(int ch = 0)
        {
            if (ch > -1 && ch <= MAX_CHANNEL)
            {
                string strcmd = $"*O{ch:X2}1$";

                if (!fn_SendCommand(strcmd, "Light ON")) return;

                if (ch == 0) G.WriteLog($"PLS All ch Light ON");
                else G.WriteLog($"PLS {ch} ch Light ON");
            }
        }

        /// <summary>
        /// PLS Light Off
        /// </summary>
        /// <param name="ch">조명 채널(-1 : All Channel)</param>
        public void LightOff(int ch = 0)
        {
            if (ch > -1 && ch <= MAX_CHANNEL)
            {
                string strcmd = $"*O{ch:X2}0$";

                if (!fn_SendCommand(strcmd, "Light OFF")) return;

                if (ch == 0) G.WriteLog($"PLS All ch Light OFF");
                else G.WriteLog($"PLS {ch} ch Light OFF");
            }
        }

        //! 명령 전송 실패를 에러 로그로 남김.  2026/08/11
        //  - SerialComm.WriteValue()는 포트가 닫혀 있으면 예외 없이 false만 반환하므로,
        //    조명 OFF 명령이 유실되어 조명이 켜진 채 남아도 로그에 흔적이 없었음.
        /// <summary>
        /// 조명 컨트롤러 명령 전송.
        /// </summary>
        /// <param name="strCmd">전송할 명령 문자열</param>
        /// <param name="strDesc">실패 시 로그에 남길 명령 설명</param>
        /// <returns>전송 큐 적재 성공 여부</returns>
        private bool fn_SendCommand(string strCmd, string strDesc)
        {
            bool bRet = serialComm?.WriteValue(Encoding.ASCII.GetBytes(strCmd), false, false) == true;

            if (!bRet)
                G.WriteLog($"PLS {strDesc} 전송 실패. 조명 컨트롤러 통신 확인 필요. [{serialComm?.ConnPort}]", true);

            return bRet;
        }

        /// <summary>
        /// Get 함수 사용 시 데이터 수신 확인 함수.
        /// </summary>
        /// <param name="idx">컨트롤러 인덱스</param>
        /// <returns>성공시 0, 타임아웃 발생시 -1</returns>
        private int fn_CheckTimeOut()
        {
            int nState = 0;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (!m_bIsRecv)
            {
                Thread.Sleep(50);
                if (sw.ElapsedMilliseconds > m_nRecieveTimeout)
                {
                    nState = -1; // time-out
                    break;
                }
            }

            return nState;
        }

        /// <summary>
        /// 데이터 프로토콜 분석기
        /// </summary>
        /// <param name="buff">확인할 버퍼 데이터</param>
        /// <returns></returns>
        private int CheckData(byte[] buff)
        {
            int nRet = -1;
            string SendCmd = serialComm.LastSendData;
            string strMsg = Encoding.ASCII.GetString(buff);
            string[] strData;
            // chennel on/off
            if (strMsg.Length == 11)
            {
                strMsg = strMsg.Replace("*", "");
                strMsg = strMsg.Replace("$", "");
                strData = strMsg.Split(',');
                for (int i = 0; i < strData.Length; i++)
                {
                    m_bOnoff[i] = Convert.ToBoolean(strData[i]);
                }
                nRet = 1;
            }
            // light value
            if (strMsg.Length == 26)
            {
                strMsg = strMsg.Replace("*", "");
                strMsg = strMsg.Replace("$", "");
                strData = strMsg.Split(',');
                for (int i = 0; i < strData.Length; i++)
                {
                    m_nLightValue[i] = Convert.ToInt32(strData[i]);
                }
                //m_nLightValue[0]
                nRet = 1;
            }

            return nRet;
        }

        /// <summary>
        /// 컨트롤러 1번 콜백.
        /// </summary>
        /// <param name="buff">수신 데이터</param>
        private void RecvData1(byte[] buff)
        {
            m_bufRecvBuff.AddRange(buff);

            int idx = SerialComm.IndexOfByteArray(m_bufRecvBuff, (byte)'$');
            List<byte> data;
            if (idx > 0)
            {
                data = SerialComm.SubBytes(m_bufRecvBuff, 0, idx + 1);
                m_bufRecvBuff = SerialComm.RemoveBytes(m_bufRecvBuff, 0, idx + 1);
            }
            else
                return;

            // Check Protocall
            if (CheckData(data.ToArray()) > 0)
            {
                m_bIsRecv = true;
            }
        }

        public bool IsLightOn(int idx)
        {
            if (idx > -1 && idx <= MAX_CHANNEL)
            {
                return m_bOnoff[idx];
            }
            else
                return false;
        }

    }
}
