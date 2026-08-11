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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KEOC_ImageGrabber
{
    public enum EN_GRABSTATE
    {
        None = 0,
        Grab,
        End
    }

    public enum EN_READYSTATE
    {
        None = 0,
        Ready,
        Alarm
    }

    static class G
    {
        static public MainWindow MAIN = null;
        //static public 
        static public GrabberManager GRABBER = new GrabberManager();
        static public LightManager LIGHT = new LightManager();
        static public VieworksCamera[] CAMERA = new VieworksCamera[Define.CAM_COUNT];

        static public ProtocallManager COMM = new ProtocallManager();
        static public ImageManager IMAGEMANAGER = new ImageManager();
        static public MessageManager MSGPROC = new MessageManager();

        static public SystemParam SYSTEM = new SystemParam();
        static public Logger LOGGER = new Logger();
        static public DiskManager DISKMANAGER = new DiskManager();

        static public RecipeParam CURRRECIPE = new RecipeParam();

        // Member
        static public EN_AUTHORITY USERLEVEL = EN_AUTHORITY.EN_OPERATOR;
        static public EN_GRABSTATE GRABSTATE = EN_GRABSTATE.None;
        static public EN_READYSTATE READYSTATE = EN_READYSTATE.None;
        // ~Member

        //! 그랩 타임아웃 감시 타이머.  Taeroo - 2026/07/27
        //  - 트리거 누락 등으로 그랩 완료 시점이 오지 않으면 조명이 계속 켜져 있으므로,
        //    설정 시간(G.SYSTEM.GrabTimeout) 경과 시 강제로 조명을 끄고 그랩을 종료함.
        //  - GrabStart는 통신 스레드, 그랩 완료는 MIL 콜백 스레드에서 호출되므로 lock으로 보호.
        static private readonly System.Timers.Timer TIMER_GRAB = new System.Timers.Timer() { AutoReset = false };
        static private readonly object LOCK_GRABTIMER = new object();

        //! 타임아웃으로 강제 종료된 그랩 표시.  Taeroo - 2026/08/11
        //  - 센서 트리거 이후 스캔 도중에 타임아웃이 만료되면 조명이 꺼진 상태로 남은 라인이
        //    스캔되어 하단이 어두운 불량 이미지가 만들어짐.
        //  - MIL은 M_STOP 이후에도 이미 버퍼에 채워진 프레임을 콜백으로 전달하므로,
        //    그 지각 프레임까지 붙으면 이미지가 '완료'로 처리되어 정상 결과로 저장/전송됨.
        //  - 이를 막기 위해 무효 표시 후 지각 프레임을 폐기함.
        static private volatile bool m_bGrabInvalid = false;
        static public bool IsGrabInvalid { get { return m_bGrabInvalid; } }

        static G()
        {
            TIMER_GRAB.Elapsed += fn_OnGrabTimeout;
        }

        #region Program Version
        static public DateTime fn_GetBuildDate()
        {
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            int day = version.Build;
            System.DateTime dtBuild = (new System.DateTime(2000, 1, 1)).AddDays(day);

            int intSeconds = version.Revision;
            intSeconds = intSeconds * 2;
            dtBuild = dtBuild.AddSeconds(intSeconds);

            System.Globalization.DaylightTime daylingTime = System.TimeZone.CurrentTimeZone.GetDaylightChanges(dtBuild.Year);
            if (System.TimeZone.IsDaylightSavingTime(dtBuild, daylingTime))
                dtBuild = dtBuild.Add(daylingTime.Delta);

            return dtBuild;
        }

        static public string fn_GetVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        static public string fn_GetVer()
        {
            return $"{G.fn_GetBuildDate():Ver.yyyyMMdd_HH}H";
        }
        #endregion

        #region Log
        /// <summary>
        /// Log 작성.
        /// </summary>
        /// <param name="strMsg">로그 메시지</param>
        static public void WriteLog(string strMsg, bool bError = false)
        {
            var st = new StackTrace();
            var sf = st.GetFrame(1);
            var sm = sf.GetMethod();
            strMsg = strMsg.Insert(0, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{G.MSGPROC.CellID},");

            if (G.SYSTEM.CallStack)
            {
                strMsg = strMsg.Insert(strMsg.Length - 1, $",{sm.DeclaringType.Name},{sm.Name}");
            }
            Console.WriteLine( strMsg );
            MAIN?.fn_WriteLog(strMsg, bError);
            LOGGER?.fn_PushMsg(strMsg, sm.DeclaringType.Name, sm.Name, bError);
        }
        #endregion


        /// <summary>
        /// Device Init
        /// </summary>
        static public void Init()
        {
            DISKMANAGER.DstImgPath = G.SYSTEM.ImagePath;
            DISKMANAGER.DstLogPath = G.SYSTEM.LogPath;
            DISKMANAGER.DstSharedPath = G.SYSTEM.SharedPath;
            DISKMANAGER.DstImgExt = G.SYSTEM.ImageExt;
            DISKMANAGER.DstLogExt = G.SYSTEM.LogExt;
            DISKMANAGER.MaintenaceDay = G.SYSTEM.MaintenanceDays;

            DISKMANAGER.fn_StartThread();
            LOGGER.fn_Init(G.SYSTEM.LogPath);
            G.WriteLog($"Init Start.");

            MSGPROC.fn_Init();

            //DISKMANAGER.ImageSavePath = G.SYSTEM.ImagePath;

            //MessageAnalyzer("ANGLEVIEW.ReceiveReady:A123123412341234:0:2:2:3/");

            //G.SYSTEM.Camera1DCF = "PatternGenerator_1024_100fps_Mono.sdcf";
            GRABBER.fn_Init(Define.CAM_COUNT, new List<string>() {G.SYSTEM.Camera1DCF});
            //GRABBER.fn_Init(Define.CAM_COUNT, new List<string>() {G.SYSTEM.Camera1DCF, G.SYSTEM.Camera2DCF, G.SYSTEM.Camera3DCF, G.SYSTEM.Camera4DCF });

            //IMAGEMANAGER.fn_Init(Define.CAM_COUNT, GRABBER.ImageWidth, G.SYSTEM.GrabHeight, 1);
            IMAGEMANAGER.fn_Init(Define.CAM_COUNT, 16384, G.SYSTEM.GrabHeight, 1);

            LIGHT.fn_Init();

            for (int i = 0; i < Define.CAM_COUNT; i++)
            {
                CAMERA[i] = new VieworksCamera();
            }
            
            CAMERA[0].fn_Init(G.SYSTEM.CameraPort1);
            //CAMERA[1].fn_Init(G.SYSTEM.CameraPort2);
            //CAMERA[2].fn_Init(G.SYSTEM.CameraPort3);
            //CAMERA[3].fn_Init(G.SYSTEM.CameraPort4);

            //COMM.fn_Init(G.SYSTEM.MasterIP, G.SYSTEM.MasterPort);
            COMM.fn_Init(G.SYSTEM.MasterPort);
            G.WriteLog($"Init End.");
        }

        static public void Final()
        {
            G.WriteLog($"Final Start.");
            DISKMANAGER.fn_StopThread();

            MSGPROC.fn_Final();

            //! 종료 시 조명 강제 OFF.  Taeroo - 2026/08/11
            //  - GrabStop()은 GRABSTATE가 Grab일 때만 조명을 끄므로, Setup 화면에서
            //    수동으로 켠 조명은 켜진 채 프로그램이 종료됨.
            //  - 명령의 실제 전송은 SerialComm.Close()의 송신 큐 flush가 보장.
            LIGHT.fn_LightOffAll();
            LIGHT.fn_Final();

            COMM.fn_Final();

            IMAGEMANAGER.fn_Final();
//#if !DEBUG
            GRABBER.fn_Final();
//#endif

            for (int i = 0; i < Define.CAM_COUNT; i++)
            {
                CAMERA[i].fn_Final();
            }

            G.WriteLog($"Final End.");
            G.WriteLog($"Program End.");
            LOGGER.fn_Final();
        }

        static public void SetGrabHeight(int height)
        {
            G.SYSTEM.GrabHeight = height;
            IMAGEMANAGER.fn_CreateBuffer(Define.CAM_COUNT, G.GRABBER.ImageWidth, G.SYSTEM.GrabHeight, G.GRABBER.ImageChannel);
        }

        static public void GrabStart()
        {
            if (GRABSTATE != EN_GRABSTATE.Grab)
            {
                m_bGrabInvalid = false;
                IMAGEMANAGER.InitAttachCount(0);
                //IMAGEMANAGER.InitAttachCount(1);
                //IMAGEMANAGER.InitAttachCount(2);
                //IMAGEMANAGER.InitAttachCount(3);
                SetCurrRecipe();
                Thread.Sleep(100);
                LIGHT.fn_LightOnAll();
                Thread.Sleep(100);

                //! 상태 전이와 타임아웃 무장을 그랩 시작보다 먼저 수행.  Taeroo - 2026/08/11
                //  - 그랩이 매우 빨리 완료되면 MIL 콜백의 fn_StopGrabTimer()가 타이머 무장보다
                //    먼저 실행되어, 이미 끝난 그랩에 타이머가 걸린 채 GRABSTATE가 Grab으로
                //    덮어써짐. 그 동안 신규 START가 모두 무시되므로 순서를 반대로 둠.
                GRABSTATE = EN_GRABSTATE.Grab;
                fn_StartGrabTimer();
                GRABBER.fn_GrabStart();
            }
        }

        #region Grab Timeout
        /// <summary>
        /// 그랩 타임아웃 감시 시작.
        /// 감시 시간은 시스템 파라메터(ImageGrabber.xml/GrabTimeout)에서 받아옴.
        /// </summary>
        static public void fn_StartGrabTimer()
        {
            lock (LOCK_GRABTIMER)
            {
                TIMER_GRAB.Stop();

                if (SYSTEM.GrabTimeout <= 0)
                {
                    G.WriteLog($"Grab Timeout Disabled. [{SYSTEM.GrabTimeout}]");
                    return;
                }

                TIMER_GRAB.Interval = SYSTEM.GrabTimeout;
                TIMER_GRAB.Start();
            }
        }

        /// <summary>
        /// 그랩 타임아웃 감시 종료.
        /// 그랩 완료 또는 그랩 정지 시 호출.
        /// </summary>
        static public void fn_StopGrabTimer()
        {
            lock (LOCK_GRABTIMER)
            {
                TIMER_GRAB.Stop();
            }
        }

        /// <summary>
        /// 감시 시간 내에 그랩이 완료되지 않았을 때 호출.
        /// 조명을 강제로 끄고 그랩을 종료하여 다음 그랩이 가능한 상태로 복구.
        /// </summary>
        static private void fn_OnGrabTimeout(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (LOCK_GRABTIMER)
            {
                // 타이머 만료와 그랩 완료가 겹친 경우, 이미 종료 처리됨.
                if (GRABSTATE != EN_GRABSTATE.Grab)
                    return;

                //! 상태 전이를 먼저 확정하고 lock을 해제.  Taeroo - 2026/08/11
                //  - 중복 진입은 위 조건으로 차단됨.
                GRABSTATE = EN_GRABSTATE.End;

                // 이 회차 그랩은 무효. 이후 도착하는 지각 프레임은 폐기됨.
                m_bGrabInvalid = true;
            }

            //! 장비 제어는 반드시 lock 밖에서 수행.  Taeroo - 2026/08/11
            //  - MIL MdigProcess(M_STOP)은 진행 중인 그랩 콜백이 끝날 때까지 대기하는데,
            //    그 콜백 스레드는 ImageManager에서 fn_StopGrabTimer()의 lock을 기다림.
            //    lock을 쥔 채로 fn_GrabStop()을 호출하면 서로를 기다려 교착 상태가 됨.
            G.WriteLog($"Grab Timeout. Light Off. [{SYSTEM.GrabTimeout} ms]", true);

            try
            {
                LIGHT.fn_LightOffAll();
                GRABBER.fn_GrabStop();
            }
            catch (Exception ex)
            {
                G.WriteLog($"[Exception] {ex.Message}", true);
            }
        }
        #endregion

        static public void SetCurrRecipe()
        {
            LIGHT.fn_SetLightValue(1, CURRRECIPE.LightCh1);
            LIGHT.fn_SetLightValue(2, CURRRECIPE.LightCh2);
            LIGHT.fn_SetLightValue(3, CURRRECIPE.LightCh3);
            LIGHT.fn_SetLightValue(4, CURRRECIPE.LightCh4);

            CAMERA[0].fn_SetDigitalGain(CURRRECIPE.CamGain1);
            //CAMERA[1].fn_SetDigitalGain(CURRRECIPE.CamGain2);
            //CAMERA[2].fn_SetDigitalGain(CURRRECIPE.CamGain3);
            //CAMERA[3].fn_SetDigitalGain(CURRRECIPE.CamGain4);

            CAMERA[0].fn_SetExposureTime(CURRRECIPE.CamExposure1);
            //CAMERA[1].fn_SetExposureTime(CURRRECIPE.CamExposure2);
            //CAMERA[2].fn_SetExposureTime(CURRRECIPE.CamExposure3);
            //CAMERA[3].fn_SetExposureTime(CURRRECIPE.CamExposure4);
        }

        static public void GrabStop()
        {
            if (GRABSTATE == EN_GRABSTATE.Grab)
            {
                fn_StopGrabTimer();
                LIGHT.fn_LightOffAll();
                GRABBER.fn_GrabStop();
                GRABSTATE = EN_GRABSTATE.End;
            }
        }
    }

    static public class Define
    {
        //public const int CAM_COUNT = 4;
        public const int CAM_COUNT = 1;
        public const int LIGHT_COUNT = 1;
        public const int LIGHT_CTRL_CH_COUNT = LIGHT_COUNT + 6;

        public const int MESSAGEPOP_TIMEOUT = 3000;

        public const string PASSWORD_OP = "";
        public const string PASSWORD_MA = "2";
        public const string PASSWORD_EN = "3";
    }
}
