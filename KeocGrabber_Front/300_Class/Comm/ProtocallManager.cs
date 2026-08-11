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
        /// 수신한 바이트 데이터를 로그에 기록.
        /// 수신한 변환 데이터를 로그에 기록.
        /// </summary>
        /// <param name="msg">수신 받은 메시지</param>
        public void WriteRecvMsg(byte[] msg) 
        {
            byte[] header = new byte[10];
            Array.Copy(msg, header, 10);
            string strLog;
            string strString = "";
            string strCellid = "";
            ushort length = msg[1];
            COMM_PROTOCALL[] protocall = new COMM_PROTOCALL[8];
            protocall[0] = (COMM_PROTOCALL)msg[0];
            protocall[1] = (COMM_PROTOCALL)msg[1];
            protocall[2] = (COMM_PROTOCALL)msg[3];
            for (int i = 5; i < 8; i++)
            {
                protocall[i] = (COMM_PROTOCALL)msg[i];
            }

            for (int i = 0; i < 8; i++)
            {
                strString += $"{protocall[i].ToString()}.";
            }

            if (length > 0)
            {
                byte[] bytedata = new byte[length];
                Array.Copy(msg, 10, bytedata, 0, length);
                strCellid = Encoding.ASCII.GetString(bytedata);
            }
            strString += $"{strCellid}.00";
            strLog = $"0x{BitConverter.ToString(header).Replace("-", "")}{strCellid}00 => {strString}";
            G.WriteLog(strLog);
        }

    }
}
