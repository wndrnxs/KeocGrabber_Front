/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 * - 주식회사 태루 -
 *******************************************************************************
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KEOC_ImageGrabber
{
    class SystemParam
    {
        // Comm
        string masterIP = "127.0.0.1";
        int masterPort = 20000;

        // Log
        bool callstack = false;

        string sharedpath = "D:/";
        string imagePath = "D:/";
        string logPath = "D:/Angleview/";
        string imageExt = "bmp";
        string logExt = "csv";
        int maintenanceDays = 30;

        string savedrive = "d";

        // Devices
        string lightPortTop1 = "COM1";

        string cameraPort1 = "COM9";

        string camera1dcf = "cam1.dcf";

        int grabHeight = 32768;

        //! 조명 자동 OFF 감시 시간.  Taeroo - 2026/07/27
        //  - 트리거 누락 등으로 그랩이 완료되지 않을 때 조명이 계속 켜져 있는 현상 방지.
        //  - 단위 : ms. 0 이하로 설정 시 감시 기능 미사용.
        int grabTimeout = 20000;

        //! 이미지 요청 큐 적체 폐기 시간.  Taeroo - 2026/08/11
        //  - 그랩 타임아웃 등으로 이미지가 완성되지 않으면 해당 셀 요청이 큐 뒤로 계속
        //    밀리며 잔류하므로, 일정 시간이 지난 요청은 폐기하여 무한 적체를 막음.
        //  - 단위 : ms. 0 이하로 설정 시 폐기 기능 미사용.
        int requestTimeout = 300000;

        string strCurrRecipeName = "-";
        public string CurrRecipeName { get { return strCurrRecipeName; } set { strCurrRecipeName = value; } }
        string strCurrRecipePath = "-";
        public string CurrRecipePath { get { return strCurrRecipePath; } set { strCurrRecipePath = value; } }

        public string MasterIP { get { return masterIP; } set { masterIP = value; } }
        public int MasterPort { get { return masterPort; } set { masterPort = value; } }

        public bool CallStack { get { return callstack; } set { callstack = value; } }

        public string SharedPath { get { return sharedpath; } set { sharedpath = value; } }
        public string ImagePath { get { return imagePath; } set { imagePath = value; } }
        public string LogPath { get { return logPath; } set { logPath = value; } }
        public string ImageExt { get { return imageExt; } set { imageExt = value; } }
        public string LogExt { get { return logExt; } set { logExt = value; } }
        public int MaintenanceDays { get { return maintenanceDays; } set { maintenanceDays = value; } }

        public string LightPort { get { return lightPortTop1; } set { lightPortTop1 = value; } }

        public string CameraPort1 { get { return cameraPort1; } set { cameraPort1 = value; } }

        public string Camera1DCF { get { return camera1dcf; } set { camera1dcf = value; } }

        public int GrabHeight { get { return grabHeight; } set { grabHeight = value; } }

        /// <summary>
        /// 그랩 시작 후 조명 자동 OFF까지의 감시 시간. [ms]
        /// 0 이하 설정 시 감시 기능 미사용.
        /// </summary>
        public int GrabTimeout { get { return grabTimeout; } set { grabTimeout = value; } }

        /// <summary>
        /// 이미지 요청 수신 후 처리되지 않은 채 큐에 남아있을 수 있는 최대 시간. [ms]
        /// 0 이하 설정 시 폐기 기능 미사용.
        /// </summary>
        public int RequestTimeout { get { return requestTimeout; } set { requestTimeout = value; } }

        public string SaveDrive { get { return savedrive; } set { savedrive = value; } }
    }
}
