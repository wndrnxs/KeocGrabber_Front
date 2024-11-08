using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics;

namespace KEOC_ImageGrabber
{
    internal class TCPIPServer
    {
        struct ST_CLIENT
        {
            public Thread thread;
            public Socket tcpclient;
            public List<byte> msgbuffer;
            public ST_CLIENT(bool binit = true)
            {
                thread = null;
                tcpclient = null;
                msgbuffer = new List<byte>();
            }
        }

        Action<byte[]> delMsgAnaly = null;
        public Action<byte[]> MsgAnalyzer { set { delMsgAnaly = value; } }

        //TcpListener tcpListener;
        Socket tcpServerSocket;
        Thread tcpThread;
        List<ST_CLIENT> listclient = new List<ST_CLIENT>();
        bool bListener = false;

        int nPort = 21000;
        const int MAX_BUFFER_SIZE = 1024;
        const int MAX_CLIENT_COUNT = 10;

        public int ClientCount {  get { return listclient.Count; } }

        public void Init(int port)
        {
            nPort = port;
            tcpThread = new Thread(TcpServerRun);
            tcpThread.Start();
        }

        public void Final()
        {
            tcpThread.Abort();
            foreach (var client in listclient)
            {
                client.thread.Abort();
                client.tcpclient.Close();
            }
            tcpServerSocket.Close();
        }

        /// <summary>
        /// 클라이언트 Accept Thread
        /// </summary>
        private void TcpServerRun()
        {
            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, nPort);
            tcpServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            SetKeepAlive(ref tcpServerSocket);
            tcpServerSocket.Bind(localEP);
            tcpServerSocket.Listen(5);
            bListener = true;
            while (bListener)
            {
                try
                {
                    ST_CLIENT stClient = new ST_CLIENT(true);

                    stClient.tcpclient = tcpServerSocket.Accept();

                    stClient.thread = new Thread(new ParameterizedThreadStart(ClientRecive));
                    listclient.Add(stClient);
                    G.MAIN.fn_AddClient(stClient.tcpclient.RemoteEndPoint.ToString());
                    G.WriteLog($"{stClient.tcpclient.RemoteEndPoint} 연결 되었습니다.");
                    listclient[listclient.Count - 1].thread.Start(stClient);
                    if (listclient.Count > MAX_CLIENT_COUNT)
                    {
                        listclient[0].thread.Abort();
                        G.MAIN.fn_RemoveClient(listclient[0].tcpclient.RemoteEndPoint.ToString());
                        listclient[0].tcpclient.Close();
                        listclient.RemoveAt(0);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(ex.Message);
                }

            }
        }

        private void SetKeepAlive(ref Socket sock)
        {
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            //TcpKeepAliveTime
            sock.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)3, 1);
            //TcpKeepAliveInterval
            sock.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)17, 3);
            //TcpKeepAliveRetryCount
            sock.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)16, 10);
        }

        /// <summary>
        /// Client Data Recive Thread
        /// 접속된 클라이언트 수 만큼 표기.
        /// </summary>
        /// <param name="client">TCP Client Type</param>
        private void ClientRecive(object client)
        {
            ST_CLIENT stClient = (ST_CLIENT)client;
            byte[] message = new byte[MAX_BUFFER_SIZE];
            int recvcnt = 0;
            int parsingidx = 0;
            byte[] data = new byte[MAX_BUFFER_SIZE];

            const int DUMMY_INTERVAL = 5 * 1000;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
                while (stClient.tcpclient.Connected)
                {
                    Thread.Sleep(10);

                    if (stClient.tcpclient.Available > 0)
                    {
                        recvcnt = stClient.tcpclient.Receive(message);
                        Array.Resize(ref message, recvcnt);
                        stClient.msgbuffer.AddRange(message);
                    }
                    else
                    {
                        if (sw.ElapsedMilliseconds > DUMMY_INTERVAL)
                        {
                            G.COMM.SendDummy();
                            sw.Restart();
                        }
                    }
                    
                    if (stClient.msgbuffer.Count > 10)
                    {
                        parsingidx = stClient.msgbuffer.FindIndex(x => x == (byte)COMM_PROTOCALL.STX) + 1;

                        byte[] header = new byte[10];
                        Array.Copy(stClient.msgbuffer.ToArray(), parsingidx - 10, header, 0, header.Length);

                        int datatotallen = header[1] + header.Length;
                        data = new byte[datatotallen];
                        Array.Copy(stClient.msgbuffer.ToArray(), parsingidx - 10, data, 0, data.Length);
                        stClient.msgbuffer.RemoveRange(0, header[1] + parsingidx);
                        //stClient.msgbuffer = stClient.msgbuffer.Substring(parsingidx + 1);
                        delMsgAnaly?.Invoke(data);
                    }
                }
            }
            catch (ThreadAbortException ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                G.WriteLog($"{stClient.tcpclient.RemoteEndPoint} 연결이 종료 되었습니다.");
                G.MAIN.fn_RemoveClient(stClient.tcpclient.RemoteEndPoint.ToString());
                stClient.tcpclient.Close();
            }
            listclient.Remove(stClient);
        }

        public void SendMessage(byte[] msg)
        {
            try
            {
                if (listclient.Count > 0)
                {
                    if (listclient[listclient.Count - 1].tcpclient.Connected)
                    {
                        listclient[listclient.Count - 1].tcpclient.Send(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
