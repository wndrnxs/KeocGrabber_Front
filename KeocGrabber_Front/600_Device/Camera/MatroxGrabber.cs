/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 *******************************************************************************
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Matrox.MatroxImagingLibrary;
using OpenCvSharp;

namespace KeocGrabber_Front
{
    public enum MILBOARD_TYPE
    {
        EN_BT_DEFAULT = 0,
        EN_BT_HOST,
        EN_BT_SOLIOS,
        EN_BT_RADIENT,
        EN_BT_RADIENTCLHS,
        EN_BT_RADIENTEVCL,
        EN_BT_RADIENTCXP,
        EN_BT_RADIENTPRO,
        EN_BT_RAPIXOCXP
    }
    class MatroxGrabber
    {
        Func<Mat, bool> delGrab = null;
        Func<string, bool> delLog = null;
        public Func<Mat, bool> OnGrab { set { delGrab = value; } }
        public Func<string, bool> OnLog { set { delLog = value; } }

        int m_nGrabBufferListSize = 0;

        uint m_nFrameCount = 0;

        Mat m_matGrab;
        byte[] m_databuff;

        int m_nBoardIndex = 0;
        int m_nCameraIndex = 0;

        MIL_ID m_MilApplication = MIL.M_NULL;         // Application identifier.
        MIL_ID m_MilSystem = MIL.M_NULL;              // System identifier.
        MIL_ID m_MilDigitizer = MIL.M_NULL;           // Digitizer identifier.
        MIL_ID m_MilImage = MIL.M_NULL;               // Image buffer identifier.

        MIL_ID[] m_MilBuffer = null;
        
        MIL_ID m_MilImageSizeX;
        MIL_ID m_MilImageSizeY;
        MIL_ID m_MilImageType;
        MIL_ID m_MilImageBand;

        GCHandle hUserData;

        public MIL_ID ApplicationID { get { return m_MilApplication; } }
        public MIL_ID SystemID { get { return m_MilSystem; } }
        public int Width { get { return (int)m_MilImageSizeX; } }
        public int Height { get { return (int)m_MilImageSizeY; } }
        public int Channel { get { return (int)m_MilImageBand; } }

        public int BoardIndex { get { return m_nBoardIndex; } }

        public int BufferCount { get { return m_MilBuffer.Length; } }

        bool m_bIsInit = false;
        bool m_bIsCanSystemFree = false;
        bool m_bIsGrabbing = false;

        public bool IsGrabbing { get { return m_bIsGrabbing; } }

        public bool IsInit { get { return m_bIsInit; } }
        public bool IsCanSystemFree { set { m_bIsCanSystemFree = value; } }
        MIL_DIG_HOOK_FUNCTION_PTR UserHookFunctionDelegate = null;

         public MatroxGrabber()
        {
            // Allocate defaults.
        }

        public void fn_Init(MILBOARD_TYPE boardtype, int boardindex, int cameraindex, string dcfpath, MIL_ID systemid)
        {
            MIL_ID rtn = MIL.M_NULL;
            if (hUserData.IsAllocated) hUserData.Free();
            hUserData = GCHandle.Alloc(this);
            try
            {
                m_nBoardIndex = boardindex;
                m_nCameraIndex = cameraindex;
                //MIL.MappAlloc(MIL.M_DEFAULT, ref m_MilApplication);
                if (UserHookFunctionDelegate == null)
                    UserHookFunctionDelegate = new MIL_DIG_HOOK_FUNCTION_PTR(OnGrabbedImage);

                string board = MIL.M_SYSTEM_DEFAULT;
                switch (boardtype)
                {
                    case MILBOARD_TYPE.EN_BT_DEFAULT: board = MIL.M_SYSTEM_DEFAULT; break;
                    case MILBOARD_TYPE.EN_BT_HOST: board = MIL.M_SYSTEM_HOST; break;
                    case MILBOARD_TYPE.EN_BT_SOLIOS: board = MIL.M_SYSTEM_SOLIOS; break;
                    case MILBOARD_TYPE.EN_BT_RADIENT: board = MIL.M_SYSTEM_RADIENT; break;
                    case MILBOARD_TYPE.EN_BT_RADIENTCLHS: board = MIL.M_SYSTEM_RADIENTCLHS; break;
                    case MILBOARD_TYPE.EN_BT_RADIENTEVCL: board = MIL.M_SYSTEM_RADIENTEVCL; break;
                    case MILBOARD_TYPE.EN_BT_RADIENTCXP: board = MIL.M_SYSTEM_RADIENTCXP; break;
                    case MILBOARD_TYPE.EN_BT_RADIENTPRO: board = MIL.M_SYSTEM_RADIENTPRO; break;
                    case MILBOARD_TYPE.EN_BT_RAPIXOCXP: board = MIL.M_SYSTEM_RADIENTCXP; break;
                }
//#if DEBUG
                //board = MIL.M_SYSTEM_DEFAULT;
//#endif
                //if (systemid == MIL.M_NULL)
                    MIL.MsysAlloc(board, MIL.M_DEV0 + m_nBoardIndex, MIL.M_DEFAULT, ref m_MilSystem);
                    //MIL.MsysAlloc(MIL.M_DEFAULT, board, MIL.M_DEV0 + m_nBoardIndex, MIL.M_DEFAULT, ref m_MilSystem);
                //else
                //    m_MilSystem = systemid;

                rtn = MIL.MdigAlloc(m_MilSystem, MIL.M_DEV0 + m_nCameraIndex, dcfpath, MIL.M_DEFAULT, ref m_MilDigitizer);

                if (rtn == MIL.M_NULL)
                {
                    G.WriteLog("Digitizer Alloc Fail.", true);
                    return;
                }

                MIL.MdigInquire(m_MilDigitizer, MIL.M_SOURCE_SIZE_X, ref m_MilImageSizeX); // size 받음.
                MIL.MdigInquire(m_MilDigitizer, MIL.M_SOURCE_SIZE_Y, ref m_MilImageSizeY);
                MIL.MdigInquire(m_MilDigitizer, MIL.M_TYPE, ref m_MilImageType);
                MIL.MdigInquire(m_MilDigitizer, MIL.M_SIZE_BAND, ref m_MilImageBand);


                CreateBuffer();

                //MbufClear(m_MilImage, 0);   // 버퍼 초기화

                MatType matType = new MatType();
                matType = MatType.CV_8UC1;
                switch (m_MilImageType)
                {
                    case 3: matType = MatType.CV_8UC3; break;
                }
                // Digitizer 설정
                MIL.MdigControl(m_MilDigitizer, MIL.M_GRAB_TIMEOUT, MIL.M_INFINITE);
                MIL.MdigControl(m_MilDigitizer, MIL.M_GRAB_MODE, MIL.M_ASYNCHRONOUS);

                // 센서 트리거 지연 설정.
                fn_SetGrabTriggerDelay(G.SYSTEM.GrabTriggerDelay);

                delLog?.Invoke($"MIL Init.({BoardIndex})");

                m_matGrab = Mat.Zeros(new Size(m_MilImageSizeX, m_MilImageSizeY), matType);

                m_databuff = new byte[m_MilImageSizeX * m_MilImageSizeY * m_MilImageType];

                delLog?.Invoke($"Buffer Init.({BoardIndex}) [{m_MilImageSizeX} x {m_MilImageSizeY} x {m_MilImageType}]");
                m_bIsInit = true;
            }
            catch (Exception ex)
            {
                G.WriteLog(ex.Message, true);
            }
        }

        public void fn_Final()
        {
            m_databuff = null;
            if (m_matGrab != null && !m_matGrab.Empty()) m_matGrab.Release();
            
            delLog?.Invoke($"Buffer Final.({BoardIndex})");

            //for (m_nGrabBufferListSize = 0; m_nGrabBufferListSize < BUFFERING_SIZE_MAX; m_nGrabBufferListSize++)
            for (m_nGrabBufferListSize = 0; m_nGrabBufferListSize < m_MilBuffer?.Length; m_nGrabBufferListSize++)
            {
                if (m_MilBuffer[m_nGrabBufferListSize] != MIL.M_NULL)
                {
                    MIL.MbufFree(m_MilBuffer[m_nGrabBufferListSize]);
                    m_MilBuffer[m_nGrabBufferListSize] = MIL.M_NULL;
                }
            }

            if (m_MilImage != MIL.M_NULL)
            {
                MIL.MbufFree(m_MilImage); // Display Buffer 해제
                m_MilImage = MIL.M_NULL;
            }
            if (m_MilDigitizer != MIL.M_NULL)
            {
                MIL.MdigFree(m_MilDigitizer); // Digitizer 해제
                m_MilDigitizer = MIL.M_NULL;
            }
            if (m_bIsCanSystemFree && m_MilSystem != MIL.M_NULL)
            {
                MIL.MsysFree(m_MilSystem); // System 해제
                m_MilSystem = MIL.M_NULL;
            }
            UserHookFunctionDelegate = null;
                                                                      //if (m_MilApplication != MIL.M_NULL) MIL.MappFree(m_MilApplication); // Application 해제
            if (hUserData.IsAllocated) hUserData.Free();
            delLog?.Invoke($"MIL Final.({BoardIndex})");
            m_bIsInit = false;
        }

        public void CreateBuffer()
        {
            for (m_nGrabBufferListSize = 0; m_nGrabBufferListSize < m_MilBuffer?.Length; m_nGrabBufferListSize++)
            {
                if (m_MilBuffer[m_nGrabBufferListSize] != MIL.M_NULL)
                {
                    MIL.MbufFree(m_MilBuffer[m_nGrabBufferListSize]);
                    m_MilBuffer[m_nGrabBufferListSize] = MIL.M_NULL;
                }
            }

            int GrabBufferCount = (int)Math.Ceiling(G.SYSTEM.GrabHeight / (double)((int)m_MilImageSizeY));
            m_MilBuffer = new MIL_ID[GrabBufferCount];

            for (m_nGrabBufferListSize = 0; m_nGrabBufferListSize < m_MilBuffer.Length; m_nGrabBufferListSize++)
            {
                if (m_MilImageType == 3)
                {
                    MIL.MbufAllocColor(m_MilSystem, m_MilImageBand, m_MilImageSizeX, m_MilImageSizeY, m_MilImageType, MIL.M_IMAGE + MIL.M_GRAB, ref m_MilBuffer[m_nGrabBufferListSize]);
                }
                else
                {
                    MIL.MbufAlloc2d(m_MilSystem, m_MilImageSizeX, m_MilImageSizeY, m_MilImageType, MIL.M_IMAGE + MIL.M_GRAB, ref m_MilBuffer[m_nGrabBufferListSize]);
                }
                MIL.MbufClear(m_MilBuffer[m_nGrabBufferListSize], 0);
            }
        }

        public void fn_GrabStart(bool bSetup = false)
        {
            m_nFrameCount = 0;
            if (bSetup)
            {
                MIL.MdigControl(m_MilDigitizer, MIL.M_GRAB_TRIGGER_STATE, MIL.M_DISABLE); // sw trigger?
                MIL.MdigProcess(m_MilDigitizer, m_MilBuffer, m_MilBuffer.Length, MIL.M_START, MIL.M_ASYNCHRONOUS, UserHookFunctionDelegate, GCHandle.ToIntPtr(hUserData));
            }
            else
            {
                MIL.MdigControl(m_MilDigitizer, MIL.M_GRAB_TRIGGER_STATE, MIL.M_ENABLE);
                MIL.MdigProcess(m_MilDigitizer, m_MilBuffer, m_MilBuffer.Length, MIL.M_SEQUENCE, MIL.M_ASYNCHRONOUS + MIL.M_TRIGGER_FOR_FIRST_GRAB, UserHookFunctionDelegate, GCHandle.ToIntPtr(hUserData));
            }

            m_bIsGrabbing = true;
            delLog?.Invoke($"Grab Start.({BoardIndex})");
        }

        public void fn_GrabStop()
        {
            MIL.MdigProcess(m_MilDigitizer, m_MilBuffer, m_MilBuffer.Length, MIL.M_STOP, MIL.M_DEFAULT, UserHookFunctionDelegate, GCHandle.ToIntPtr(hUserData));
            m_bIsGrabbing = false;
            delLog?.Invoke($"Grab Stop.({BoardIndex})");
        }

        static private MIL_INT OnGrabbedImage(MIL_INT HookType, MIL_ID HookId, IntPtr HookDataPtr)
        {
            MatroxGrabber pMain = GCHandle.FromIntPtr(HookDataPtr).Target as MatroxGrabber;

            MIL_ID MilGrabBufID = MIL.M_NULL;
            MIL_INT MilIndex = MIL.M_NULL;

            MIL.MdigGetHookInfo(HookId, MIL.M_MODIFIED_BUFFER + MIL.M_BUFFER_ID, ref MilGrabBufID);
            MIL.MdigGetHookInfo(HookId, MIL.M_MODIFIED_BUFFER + MIL.M_BUFFER_INDEX, ref MilIndex);

            // 버퍼가 할당되어 있다면.
            if (pMain.m_MilBuffer[MilIndex] != MIL.M_NULL)
            //if (MilGrabBufID != MIL.M_NULL)
            {
                if (pMain.m_MilImageType == 3)
                    MIL.MbufGetColor2d(MilGrabBufID, MIL.M_PACKED + MIL.M_BGR24, MIL.M_ALL_BANDS, 0, 0, pMain.m_MilImageSizeX, pMain.m_MilImageSizeY, pMain.m_databuff);
                else
                    MIL.MbufGet2d(MilGrabBufID, 0, 0, pMain.m_MilImageSizeX, pMain.m_MilImageSizeY, pMain.m_databuff);

                pMain.m_matGrab.SetArray<byte>(pMain.m_databuff);
                
                //Cv2.ImShow("Test", pMain.m_matGrab);
                pMain.m_nFrameCount++;

                // 콜백 실행.
                pMain.delGrab?.Invoke(pMain.m_matGrab);

                Thread.Sleep(1);
            }
            return 0;
        }
        
        //! 센서 트리거 후 촬상 시작 지연.  2026/11/03
        //  - DCF에 설정된 그랩 트리거(센서 입력)를 보드 타이머의 트리거로 옮기고,
        //    타이머 출력을 그랩 트리거로 사용하여 '센서 신호 → 지연 → 그랩' 순서로 동작시킴.
        //  - 센서 입력 핀(AUX IO)과 극성은 DCF 설정을 그대로 읽어 사용하므로 별도 설정 불필요.
        //  - DCF에서 노출 제어 등에 사용 중인 타이머는 건드리지 않도록 비어 있는 타이머를 선택.
        //  - 타이머 설정이 실패하면 그랩 트리거를 바꾸지 않아 기존(지연 없음) 동작을 유지.
        static readonly MIL_INT[] TIMER_LIST = { MIL.M_TIMER1, MIL.M_TIMER2, MIL.M_TIMER3, MIL.M_TIMER4 };
        static readonly MIL_INT[] AUXIO_LIST =
        {
            MIL.M_AUX_IO0,  MIL.M_AUX_IO1,  MIL.M_AUX_IO2,  MIL.M_AUX_IO3,
            MIL.M_AUX_IO4,  MIL.M_AUX_IO5,  MIL.M_AUX_IO6,  MIL.M_AUX_IO7,
            MIL.M_AUX_IO8,  MIL.M_AUX_IO9,  MIL.M_AUX_IO10, MIL.M_AUX_IO11,
            MIL.M_AUX_IO12, MIL.M_AUX_IO13, MIL.M_AUX_IO14, MIL.M_AUX_IO15
        };

        /// <summary>
        /// 센서 트리거 후 촬상 시작 지연 설정.
        /// </summary>
        /// <param name="nDelayMs">지연 시간 [ms]. 0 이하면 미사용.</param>
        private void fn_SetGrabTriggerDelay(int nDelayMs)
        {
            if (nDelayMs <= 0)
            {
                delLog?.Invoke($"Grab Trigger Delay Disabled.({BoardIndex})");
                return;
            }

            // DCF에 설정된 센서 입력 확인.
            MIL_INT nTrigSource = 0;
            MIL_INT nTrigActivation = 0;
            MIL.MdigInquire(m_MilDigitizer, MIL.M_GRAB_TRIGGER_SOURCE, ref nTrigSource);
            MIL.MdigInquire(m_MilDigitizer, MIL.M_GRAB_TRIGGER_ACTIVATION, ref nTrigActivation);

            int nAuxIO = Array.IndexOf(AUXIO_LIST, nTrigSource);
            if (nAuxIO < 0)
            {
                G.WriteLog($"Grab Trigger Delay Fail. 그랩 트리거 소스가 AUX IO가 아님. [{nTrigSource}] 지연 없이 동작.", true);
                return;
            }

            // 타이머 트리거는 엣지만 사용하므로 레벨 설정은 같은 극성의 엣지로 변환.
            MIL_INT nTimerActivation = (nTrigActivation == MIL.M_EDGE_FALLING || nTrigActivation == MIL.M_LEVEL_LOW)
                                       ? MIL.M_EDGE_FALLING : MIL.M_EDGE_RISING;

            // 사용 중이지 않은 타이머 선택.
            int nTimerIdx = -1;
            for (int i = 0; i < TIMER_LIST.Length; i++)
            {
                MIL_INT nState = MIL.M_ENABLE;
                MIL.MdigInquire(m_MilDigitizer, MIL.M_TIMER_STATE + TIMER_LIST[i], ref nState);
                if (nState == MIL.M_DISABLE)
                {
                    nTimerIdx = i;
                    break;
                }
            }
            if (nTimerIdx < 0)
            {
                G.WriteLog($"Grab Trigger Delay Fail. 사용 가능한 타이머 없음. 지연 없이 동작.", true);
                return;
            }
            MIL_INT nTimer = TIMER_LIST[nTimerIdx];

            // 타이머 설정 : 센서 입력 → 지연 → 펄스 출력. (단위 : nsec)
            MIL.MdigControl(m_MilDigitizer, MIL.M_TIMER_TRIGGER_SOURCE + nTimer, nTrigSource);
            MIL.MdigControl(m_MilDigitizer, MIL.M_TIMER_TRIGGER_ACTIVATION + nTimer, nTimerActivation);
            MIL.MdigControl(m_MilDigitizer, MIL.M_TIMER_DELAY + nTimer, nDelayMs * 1000000.0);
            MIL.MdigControl(m_MilDigitizer, MIL.M_TIMER_DURATION + nTimer, 1000000.0); // 1ms 펄스.
            MIL.MdigControl(m_MilDigitizer, MIL.M_TIMER_STATE + nTimer, MIL.M_ENABLE);

            // 타이머가 실제로 적용되었는지 확인 후에만 그랩 트리거를 타이머로 전환.
            MIL_INT nState2 = MIL.M_DISABLE;
            double dDelayNs = 0;
            MIL.MdigInquire(m_MilDigitizer, MIL.M_TIMER_STATE + nTimer, ref nState2);
            MIL.MdigInquire(m_MilDigitizer, MIL.M_TIMER_DELAY + nTimer, ref dDelayNs);
            if (nState2 != MIL.M_ENABLE || dDelayNs <= 0)
            {
                MIL.MdigControl(m_MilDigitizer, MIL.M_TIMER_STATE + nTimer, MIL.M_DISABLE);
                G.WriteLog($"Grab Trigger Delay Fail. 타이머 설정 실패. 지연 없이 동작.", true);
                return;
            }

            MIL.MdigControl(m_MilDigitizer, MIL.M_GRAB_TRIGGER_SOURCE, nTimer);
            MIL.MdigControl(m_MilDigitizer, MIL.M_GRAB_TRIGGER_ACTIVATION, MIL.M_EDGE_RISING);

            delLog?.Invoke($"Grab Trigger Delay Set.({BoardIndex}) [AUX_IO{nAuxIO}" +
                           $"({(nTimerActivation == MIL.M_EDGE_FALLING ? "Falling" : "Rising")}) → TIMER{nTimerIdx + 1} → Grab, " +
                           $"{dDelayNs / 1000000.0:F1} ms]");

            if (G.SYSTEM.GrabTimeout > 0 && nDelayMs >= G.SYSTEM.GrabTimeout)
            {
                G.WriteLog($"Grab Trigger Delay({nDelayMs} ms)가 Grab Timeout({G.SYSTEM.GrabTimeout} ms) 이상. 촬상 전에 타임아웃 발생.", true);
            }
        }

        public bool fn_GetIOValue()
        {
            MIL_INT IOState = 0;
            if (m_MilDigitizer != 0)
            {
                MIL.MdigInquire(m_MilDigitizer, MIL.M_IO_STATUS + MIL.M_AUX_IO5, ref IOState);
            }
            else
            {
                G.WriteLog("GetIOValue Error : MilDigitizer is Zero.");
            }
            bool bRet = IOState == 0 ? false : true;
            return bRet;
        }
    }
}
