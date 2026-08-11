/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 * - 주식회사 태루 -
 *******************************************************************************
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KEOC_ImageGrabber
{
    //! 이미지 요청 1건.  Taeroo - 2026/08/11
    //  - 큐 적체 판단을 위해 수신 시각이 필요하여 Tuple에서 클래스로 변경.
    class ImageRequest
    {
        public string CellID1 { get; private set; }
        public string CellID2 { get; private set; }

        /// <summary>요청 수신 시각.</summary>
        public DateTime RecvTime { get; private set; }

        public ImageRequest(string cellid1, string cellid2)
        {
            CellID1 = cellid1;
            CellID2 = cellid2;
            RecvTime = DateTime.Now;
        }
    }

    class MessageManager
    {
        //! GC 회피용 ROI 이미지 객체.  Taeroo-kgseon - 2024/08/27  15:40
        //  - fn_ProcRequest 동작에 지역 변수 였으나, GC 동작 예방을 위해 맴버변수로 변경.
        OpenCvSharp.Mat matROI1, matROI2;
        List<ImageRequest> m_listRequest = new List<ImageRequest>();
        Mutex mutex = new Mutex();
        Thread thread = null;
        public string CellID
        {
            get
            {
                if (m_listRequest.Count > 0)
                {
                    return $"{m_listRequest[0].CellID1},{m_listRequest[0].CellID2}";
                }
                else
                {
                    return "";
                }
            }
        }

        public int CellIDCount
        { 
            get
            {
                return m_listRequest.Count;
            }
        }

        public void fn_Init()
        {
            thread = new Thread(new ThreadStart(THREAD_MESSAGE));
            thread.Start();
        }

        public void fn_Final()
        {
            if (thread.IsAlive)
            {
                thread.Abort();
                thread = null;
            }
        }

        /// <summary>
        /// 이미지 요청 큐 적재.
        /// </summary>
        /// <param name="cellid1">셀 아이디1</param>
        /// <param name="cellid2">셀 아이디2</param>
        public void fn_PushRequest(string cellid1, string cellid2)
        {
            mutex.WaitOne();
            m_listRequest.Add(new ImageRequest(cellid1, cellid2));
            mutex.ReleaseMutex();
        }

        //! 큐에 오래 잔류한 요청 폐기.  Taeroo - 2026/08/11
        //  - 그랩 타임아웃 등으로 이미지가 완성되지 않으면 fn_PopRequest가 해당 요청을
        //    큐 뒤로 계속 밀어내기만 하므로 영구히 잔류함.
        //  - fn_PopRequest와 같은 스레드(THREAD_MESSAGE)에서만 호출하여 인덱스 경합을 피함.
        /// <summary>
        /// 수신 후 SYSTEM.RequestTimeout 이 경과한 요청을 큐에서 제거.
        /// </summary>
        private void fn_RemoveExpiredRequest()
        {
            if (G.SYSTEM.RequestTimeout <= 0 || m_listRequest.Count == 0)
                return;

            DateTime now = DateTime.Now;

            if (!mutex.WaitOne(3000))
                return;

            try
            {
                for (int i = m_listRequest.Count - 1; i >= 0; i--)
                {
                    double dElapsed = (now - m_listRequest[i].RecvTime).TotalMilliseconds;
                    if (dElapsed < G.SYSTEM.RequestTimeout)
                        continue;

                    G.WriteLog($"[ProcREQ] 요청 적체 {dElapsed / 1000:F1}초 경과. 폐기. " +
                               $"[{m_listRequest[i].CellID1} | {m_listRequest[i].CellID2}]", true);
                    m_listRequest.RemoveAt(i);
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Image가 Ready가 안되면 POP하지 않음.
        /// </summary>
        /// <returns>받은 요청.(CellID, ImageIndex, NodeID[].)</returns>
        private ImageRequest fn_PopRequest()
        {
            ImageRequest request = null;

            if (m_listRequest.Count > 0)
            {
                request = m_listRequest[0];
                if (G.IMAGEMANAGER.IsImageCompalte != null)
                {
                    //! 완성된 이미지 먼저 전송 코드.  Taeroo-kgseon - 2024/08/02  13:49
                    if (G.IMAGEMANAGER.IsImageCompalte[0])
                    {
                        if (mutex.WaitOne(3000))
                        {
                            m_listRequest.RemoveAt(0);
                            mutex.ReleaseMutex();
                        }
                        else
                        {
                            request = null;
                        }
                    }
                    else // 앞선 데이터를 제거하고, 가장 뒤로 이동.
                    {
                        if (mutex.WaitOne(3000))
                        {
                            m_listRequest.RemoveAt(0);
                            m_listRequest.Add(request);
                            mutex.ReleaseMutex();
                        }
                        request = null;
                    }

                }
                else
                {
                    request = null;
                }
            }

            return request;
        }

        /// <summary>
        /// 이미지 요청 처리 부분
        /// 만약 Recipe가 잘못 되었다면, 강제로 이미지 절반으로 잘라서 보냄.
        /// </summary>
        /// <param name="req">CellID1, CellID2</param>
        private void fn_ProcRequest(ImageRequest req)
        {
            string cellid1 = req.CellID1;
            string cellid2 = req.CellID2;

            OpenCvSharp.Mat matImg = G.IMAGEMANAGER.CamImage[0];
            int orgwidth = matImg.Cols;
            int orgheight = matImg.Rows;

            if (G.CURRRECIPE.CropROI.Rows.Count  != 2)
            {
                G.WriteLog("Invalied Recipe ROI Infomation.", true);
            }

            OpenCvSharp.Rect rectROI1 = new OpenCvSharp.Rect();
            OpenCvSharp.Rect rectROI2 = new OpenCvSharp.Rect();
            try
            {

                rectROI1.X = Convert.ToInt32(G.CURRRECIPE.CropROI.Rows[0]["X"]);
                rectROI1.Y = Convert.ToInt32(G.CURRRECIPE.CropROI.Rows[0]["Y"]);
                rectROI1.Width = Convert.ToInt32(G.CURRRECIPE.CropROI.Rows[0]["Width"]);
                rectROI1.Height = Convert.ToInt32(G.CURRRECIPE.CropROI.Rows[0]["Height"]);

                rectROI2.X = Convert.ToInt32(G.CURRRECIPE.CropROI.Rows[1]["X"]);
                rectROI2.Y = Convert.ToInt32(G.CURRRECIPE.CropROI.Rows[1]["Y"]);
                rectROI2.Width = Convert.ToInt32(G.CURRRECIPE.CropROI.Rows[1]["Width"]);
                rectROI2.Height = Convert.ToInt32(G.CURRRECIPE.CropROI.Rows[1]["Height"]);
            }
            catch (Exception ex)
            {
                G.WriteLog($"Recipe Error, Split half : {ex.Message}", true);
                rectROI1.X = 0;
                rectROI1.Y = 0;
                rectROI1.Width = matImg.Width;
                rectROI1.Height = (int)(matImg.Height / 2.0);

                rectROI2.X = 0;
                rectROI2.Y = (int)(matImg.Height / 2.0);
                rectROI2.Width = matImg.Width;
                rectROI2.Height = (int)(matImg.Height / 2.0);
            }

            matROI1 = matImg[rectROI1].Clone();
            matROI2 = matImg[rectROI2].Clone();

            // ROI Save.
            // 완료 시점을 위한 동기 저장.
            G.DISKMANAGER.SaveImage(new Tuple<string, OpenCvSharp.Mat>(cellid1, matROI1));
            G.DISKMANAGER.SaveImage(new Tuple<string, OpenCvSharp.Mat>(cellid2, matROI2));

            // Send OK Message.
            G.COMM.SendReady(cellid1, cellid2);

            G.IMAGEMANAGER.InitComplFlag(0);

            string strMsg = $"[ProcREQ] {cellid1} | {cellid2} ";
            G.WriteLog(strMsg);
        }

        private void THREAD_MESSAGE()
        {
            ImageRequest request = null;
            while (true)
            {
                // 오래 잔류한 요청 먼저 정리.
                fn_RemoveExpiredRequest();

                // 요청 들어온 인덱스 플래그(G.IMAGEMANAGER.IsComplate[idx])가 true이면 유효한 데이터 리턴.
                // 아니라면 null데이터를 리턴하여 대기.
                request = fn_PopRequest();
                if (request != null)
                {
                    // proc message;
                    fn_ProcRequest(request);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
