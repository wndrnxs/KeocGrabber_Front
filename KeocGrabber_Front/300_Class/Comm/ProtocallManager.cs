/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 *******************************************************************************
 */
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Markup;

namespace KeocGrabber_Front
{
    // Header 10byte, data.  2024/08/14  12:44
    //                          Header                             | Data                      |
    //  1  |  2  |  3  |  4  |  5  |  6    |  7  |  8  |  9  |  10 |  1  |  2  |  -  |  Length |
    // SOH | Length    | CMD       |ACK/NCK| Dir | COM | ENQ | STX | ... | ... | ... | NUL     |
    public enum COMM_PROTOCALL : byte
    { 
        START   = 0x66, // Grab 시작
        END     = 0x67, // Grab 완료
        STATUS  = 0x68, // Grab 완료
        ACK     = 0x06, // 긍정 회신
        NAK     = 0x15, // 부정 회신
        NUL     = 0x00, // 송신 상황 시
        VTO     = 0x01, // Vision to Optic
        OTV     = 0x10, // Optic to Vision
        SEND    = 0x01, // 송신
        RECV    = 0x10, // 회신
        SOH     = 0x01, // 헤더 시작
        STX     = 0x02, // 헤더 종료
        ENQ_REQ    = 0x05, // 응답 요구 (REQ)
        ENQ_NULL    = 0x00  // 응답 필요 없음 (NULL)
        //NUL     = 0x00 // 데이터 끝
    }

    public class ProtocallManager
    {
        TCPIPServer commserver = new TCPIPServer();

        bool bIsConnected = false;
        public bool IsConnected { get { return commserver.ClientCount > 0; } }

        public void fn_Init(int port)
        {
            commserver.MsgAnalyzer = MessageAnalyzer;
            commserver.Init(port);

        }

        public void fn_Final()
        {

            commserver.MsgAnalyzer = null;
            commserver.Final();
        }

        public void SendReady(string cellid1, string cellid2)
        {
            byte[] msg;

            string strdata = $"{cellid1},{cellid2}";
            byte[] data = Encoding.ASCII.GetBytes(strdata);
            Array.Resize(ref data, data.Length + 1);
            data[data.Length - 1] = 0x00;

            byte[] header = new byte[10];
            header[0] = (byte)COMM_PROTOCALL.SOH;
            var length = BitConverter.GetBytes(data.Length - 1);
            header[1] = length[0];
            header[2] = length[1];
            var cmd = BitConverter.GetBytes((ushort)COMM_PROTOCALL.END);
            header[3] = cmd[0];
            header[4] = cmd[1];
            header[5] = (byte)COMM_PROTOCALL.NUL;
            header[6] = (byte)COMM_PROTOCALL.OTV;
            header[7] = (byte)COMM_PROTOCALL.SEND;
            header[8] = (byte)COMM_PROTOCALL.ENQ_REQ;
            header[9] = (byte)COMM_PROTOCALL.STX;

            msg = new byte[header.Length + data.Length];
            Array.Copy(header, 0, msg, 0, header.Length);
            Array.Copy(data, 0, msg, header.Length, data.Length);
            commserver?.SendMessage(msg);
            G.COMM.WriteRecvMsg(msg);
        }

        /// <summary>
        /// 소켓 연결 확인용 더미 메시지.
        /// 전송 시간?
        /// </summary>
        public void SendDummy()
        {
            byte[] msg = new byte[11];
            //! length는 null을 뺀 크기.  2024/08/26  14:13
            //var length = BitConverter.GetBytes(0);
            //var cmd = BitConverter.GetBytes((ushort)COMM_PROTOCALL.END); // CMD STATUS로 변경.
            msg[0] = (byte)COMM_PROTOCALL.SOH;
            
            msg[1] = 0x00; // length
            msg[2] = 0x00;
            msg[3] = (byte)COMM_PROTOCALL.STATUS;
            msg[4] = 0x00;
            msg[5] = (byte)COMM_PROTOCALL.NUL;
            msg[6] = (byte)COMM_PROTOCALL.OTV;
            msg[7] = (byte)COMM_PROTOCALL.SEND;
            msg[8] = (byte)COMM_PROTOCALL.ENQ_NULL;
            msg[9] = (byte)COMM_PROTOCALL.STX;

            msg[10] = 0x00;
            commserver?.SendMessage(msg);

            // 5초 주기로 반복되므로 화면 로그에는 표시하지 않고 파일에만 기록.
            WriteRecvMsg(msg, false);
        }

        public void SendAck(byte[] msg)
        {
            var length = BitConverter.GetBytes(1);
            msg[1] = 0x00;
            msg[2] = 0x00;
            
            // eqpstate
            if (G.CAMERA[0].IsConnected && G.LIGHT.fn_IsConnected())
            {
                msg[5] = (byte)COMM_PROTOCALL.ACK;
            }
            else
            {
                msg[5] = (byte)COMM_PROTOCALL.NAK;
            }

            msg[6] = (byte)COMM_PROTOCALL.OTV;
            msg[7] = (byte)COMM_PROTOCALL.RECV;
            msg[8] = (byte)COMM_PROTOCALL.ENQ_NULL;
            msg[9] = (byte)COMM_PROTOCALL.STX;

            msg[10] = 0x00;
            Array.Resize(ref msg, 11);
            commserver?.SendMessage(msg);

            //! 회신 메시지도 로그 기록.  2026/11/16
            WriteRecvMsg(msg);
        }


        static public void MessageAnalyzer(byte[] msgbuff)
        {
            ushort length = msgbuff[1];
            ushort cmd = msgbuff[3];

            G.COMM.WriteRecvMsg(msgbuff);

            string[] cellid = null;
            if (msgbuff.Length > 10)
            {
                byte[] bytedata = new byte[length];
                Array.Copy(msgbuff, 10, bytedata, 0, length);
                cellid = Encoding.ASCII.GetString(bytedata).Split(',');
            }

            switch (cmd)
            {
                case (ushort)COMM_PROTOCALL.START:
                    if (cellid == null || cellid.Length != 2)
                    {
                        // Parsing error
                        return;
                    }
                    G.MSGPROC.fn_PushRequest(cellid[0], cellid[1]);
                    G.GrabStart();
                    G.COMM.SendAck(msgbuff);
                    break;
                case (ushort)COMM_PROTOCALL.END:
                    break;
            }
        }

        /// <summary>
        /// 정면 시야각 사양.
        /// 송수신 메시지의 바이트 데이터와 변환 데이터를 로그(Communication)에 기록.
        /// </summary>
        /// <param name="msg">송수신 메시지</param>
        /// <param name="bShowUI">화면 로그 표시 여부. false면 파일에만 기록.</param>
        //! 헤더를 위치별 규칙으로 변환.  2026/11/16
        //  - 기존에는 헤더 값을 하나의 enum 이름으로 변환하여, 값이 같은 이름(0x01 : SOH/VTO/SEND,
        //    0x10 : OTV/RECV, 0x00 : NUL/ENQ_NULL)이 위치와 무관하게 출력되고 Length도 이름으로
        //    찍히는 경우가 있었음. 또한 ENQ, STX가 빠지고 사용하지 않는 칸이 NUL로 출력되었음.
        //  - 사양(S/W 개발 간 요청 사항 4) 형식으로 출력.
        //    예) 0x010F0066000001010502CellID1,CellID200 => SOH.15.START.NUL.VTO.SEND.ENQ(REQ).STX.CellID1,CellID2.00
        //        0x0100006600061010000200 => SOH.0.START.ACK.OTV.RECV.ENQ(NUL).STX.NUL
        public void WriteRecvMsg(byte[] msg, bool bShowUI = true)
        {
            if (msg == null || msg.Length < 10)
            {
                G.WriteLog($"Invalid Message. [{(msg == null ? "null" : BitConverter.ToString(msg).Replace("-", ""))}]", true);
                return;
            }

            byte[] header = new byte[10];
            Array.Copy(msg, header, 10);

            // Length는 header[1]만 사용. (NUL 제외 데이터 길이)
            int length = Math.Min((int)msg[1], msg.Length - 10);
            string strCellid = length > 0 ? Encoding.ASCII.GetString(msg, 10, length) : "";

            string[] fields =
            {
                fn_HeaderName(msg[0], 0x01, "SOH"),                                     // 1 : SOH
                msg[1].ToString(),                                                       // 2~3 : Length (10진수)
                fn_CmdName(msg[3]),                                                      // 4~5 : CMD
                fn_HeaderName(msg[5], 0x06, "ACK", 0x15, "NAK", 0x00, "NUL"),            // 6 : ACK/NAK
                fn_HeaderName(msg[6], 0x01, "VTO", 0x10, "OTV"),                         // 7 : Direction
                fn_HeaderName(msg[7], 0x01, "SEND", 0x10, "RECV"),                       // 8 : 송신/회신
                fn_HeaderName(msg[8], 0x05, "ENQ(REQ)", 0x00, "ENQ(NUL)"),               // 9 : ENQ
                fn_HeaderName(msg[9], 0x02, "STX"),                                      // 10 : STX
            };

            // 데이터가 있으면 끝을 00, 없으면 NUL로 표기. (사양 예시 기준)
            string strString = string.Join(".", fields) + (strCellid != "" ? $".{strCellid}.00" : ".NUL");
            string strLog = $"0x{BitConverter.ToString(header).Replace("-", "")}{strCellid}00 => {strString}";

            if (bShowUI)
            {
                G.WriteLog(strLog);
            }
            else
            {
                // 화면 표시 없이 Communication 로그 파일에만 기록. (G.WriteLog와 같은 형식)
                G.LOGGER?.fn_PushMsg($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{G.MSGPROC.CellID},{strLog}",
                                     nameof(ProtocallManager), nameof(WriteRecvMsg));
            }
        }

        /// <summary>
        /// 헤더 위치별 값 → 이름 변환. 정의되지 않은 값은 16진수로 표기.
        /// </summary>
        /// <param name="value">헤더 값</param>
        /// <param name="pairs">(값, 이름) 쌍 목록</param>
        private static string fn_HeaderName(byte value, params object[] pairs)
        {
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                if (Convert.ToByte(pairs[i]) == value)
                    return (string)pairs[i + 1];
            }
            return $"0x{value:X2}";
        }

        private static string fn_CmdName(byte value)
        {
            switch (value)
            {
                case (byte)COMM_PROTOCALL.START:  return "START";
                case (byte)COMM_PROTOCALL.END:    return "END";
                case (byte)COMM_PROTOCALL.STATUS: return "STATUS";
            }
            return $"0x{value:X2}";
        }

    }
}
