/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 *******************************************************************************
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using OpenCvSharp;
using OpenCvSharp.Flann;
using OpenCvSharp.WpfExtensions;

namespace KeocGrabber_Front
{
    class ImageManager
    {
        Mat[] m_matCamImgage;

        int[] m_nAttachCount;

        bool[] m_bIsImageComplate;

        // 무효 그랩의 프레임 폐기 로그를 회차당 1회만 남기기 위한 플래그.
        bool[] m_bIsDiscardLogged;

        int m_nGrabWidth = 0;
        int m_nGrabHeight = 0;
        int m_nGrabChannel = 0;
        int m_nCamCount = 0;

        public bool[] IsImageCompalte { get { return m_bIsImageComplate; } }
        public Mat[] CamImage { get { return m_matCamImgage; } }

        public void fn_Init(int nCamCount, int nGrabWidth, int nGrabHeight, int nGrabChannel)
        {
            fn_CreateBuffer(nCamCount, nGrabWidth, nGrabHeight, nGrabChannel);
            G.WriteLog($"Init.");
        }

        public void SetTestImage(Mat matimg, int index)
        {
            m_bIsImageComplate[index] = true;
            m_matCamImgage[index] = matimg.Clone();
        }

        public void SetCompl(int index)
        {
            if (!m_matCamImgage[index].Empty())
            {
                m_bIsImageComplate[index] = !m_bIsImageComplate[index];
            }
        }

        public void fn_CreateBuffer(int nCamCount, int nGrabWidth, int nGrabHeight, int nGrabChannel)
        {
            m_nCamCount = nCamCount;
            m_nGrabWidth = nGrabWidth;
            m_nGrabHeight = nGrabHeight;
            m_nGrabChannel = nGrabChannel;

            m_nAttachCount = new int[m_nCamCount];
            m_bIsImageComplate = new bool[m_nCamCount];
            m_bIsDiscardLogged = new bool[m_nCamCount];

            if (m_nGrabChannel != 1 && m_nGrabChannel != 3)
            {
                G.WriteLog($"Invalid Channel Type. {m_nGrabChannel}.", true);
                return;
            }

            if (m_nGrabHeight <= 0 || m_nGrabWidth <= 0)
            {
                G.WriteLog($"Invalid Image Size. W : {m_nGrabWidth} | H : {m_nGrabHeight}.", true);
                return;
            }

            if (m_matCamImgage != null)
            {
                for (int i = 0; i < m_matCamImgage.Length; i++)
                {
                    m_matCamImgage[i].Release();
                }
                m_matCamImgage = null;
                m_nAttachCount = null;
            }

            MatType type = MatType.CV_8UC1;

            switch (m_nGrabChannel)
            {
                case 1: type = MatType.CV_8UC1; break;
                case 3: type = MatType.CV_8UC3; break;
            }

            m_matCamImgage = new Mat[m_nCamCount];
            for (int i = 0; i < nCamCount; i++)
            {
                m_matCamImgage[i] = Mat.Zeros(new Size(m_nGrabWidth, m_nGrabHeight), MatType.CV_8UC1);
            }


            G.WriteLog($"Create Buffer Complate.");
        }


        public void fn_Final()
        {
            if (m_matCamImgage != null)
            {
                for (int i = 0; i < m_matCamImgage.Length; i++)
                {
                    m_matCamImgage[i].Release();
                }
            }
            G.WriteLog($"Final.");
        }

        public void InitAttachCount(int idx)
        {
            if (m_nAttachCount != null)
                m_nAttachCount[idx] = 0;

            if (m_bIsDiscardLogged != null)
                m_bIsDiscardLogged[idx] = false;
        }

        public void InitComplFlag(int idx)
        {
            m_bIsImageComplate[idx] = false;
        }
        public void AttachImage(int idx, Mat img)
        {
            //! 타임아웃으로 무효화된 그랩의 지각 프레임은 폐기.  2026/08/11
            //  - 조명이 꺼진 뒤 스캔된 라인이 붙어 이미지가 '완료'로 처리되면,
            //    하단이 어두운 불량 이미지가 정상 결과로 저장/전송되기 때문.
            //  - 로그와 조명 OFF는 회차당 1회만 수행.
            if (G.IsGrabInvalid)
            {
                if (!m_bIsDiscardLogged[idx])
                {
                    m_bIsDiscardLogged[idx] = true;
                    G.WriteLog($"Grab Timeout 이후 도착한 프레임 폐기. 이미지 무효 처리. [idx : {idx}]", true);
                    G.LIGHT.fn_LightOffAll();
                }
                return;
            }

            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                // dcf에서 한번에 그랩 가능 할 때.
                //m_matCamImgage[idx] = img.Clone();
                //ComplateImage(idx);
                //~ dcf에서 한번에 그랩 가능 할 때.

                // dcf에서 일정 크기 만큼 그랩 할 때.
                int dstStartY = m_nAttachCount[idx] * img.Rows;
                int dstEndY = (m_nAttachCount[idx] + 1) * img.Rows;
                int grabEndY = dstEndY; // 프레임 종료 확인용 변수.

                int srcheight = m_matCamImgage[idx].Rows - dstStartY;
                if (srcheight > img.Rows)
                    srcheight = img.Rows;
                else
                {
                    dstEndY = dstStartY + srcheight;
                }

                m_matCamImgage[idx][dstStartY, dstEndY, 0, img.Cols] = img[0, srcheight, 0, img.Cols].Clone();
                //m_matCamImgage[idx].ToBytes(".bmp")
                sw.Stop();
                G.WriteLog($"Attach Image [idx : {idx}] {sw.ElapsedTicks * 1000 / (double)Stopwatch.Frequency:F3} ms {m_nAttachCount[idx]}");

                m_nAttachCount[idx]++;

                // Grab 완료.
                if (m_nGrabHeight <= grabEndY)
                {
                    //! 조명 OFF를 그랩 종료 처리보다 먼저 수행.  2026/07/27
                    //  - ComplateImage() 내부의 MIL Grab Stop에서 예외가 발생하면 조명이 켜진 채로 남고,
                    //    이미 GRABSTATE가 End로 바뀌어 G.GrabStop()의 조명 OFF도 동작하지 않기 때문.
                    G.fn_StopGrabTimer();
                    G.LIGHT.fn_LightOffAll();

                    ComplateImage(idx);
                }
                //~dcf에서 일정 크기 만큼 그랩 할 때.
            }
            catch (Exception ex)
            {
                G.WriteLog($"[Exception] {ex.Message}", true);
            }
        }

        private void ComplateImage(int index)
        {
            m_bIsImageComplate[index] = true;

            G.WriteLog($"{index} Grab Complate.");

            if (G.GRABBER.IsGrabbing)
                G.GRABSTATE = EN_GRABSTATE.End;

            G.GRABBER.fn_GrabStop(index);
        }

    }
}
