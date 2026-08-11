/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 * - 주식회사 태루 -
 *******************************************************************************
 */
using OpenCvSharp.Flann;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace KEOC_ImageGrabber
{
    public class Page_MainDataContext : MVVMBase.IPropertyChanged
    {
        Page_Process pageProcess = new Page_Process();
        public Page_Process PageProcess { get { return pageProcess; } set { pageProcess = value; OnPropertyChanged("PageProcess"); } }

        string strCellID = "";
        public string CellID { get { return strCellID; } set { strCellID = value; OnPropertyChanged(); } }

        string strCam1State = "";

        public string Cam1Grab { get { return strCam1State; } set { strCam1State = value; OnPropertyChanged(); } }

        double dTestBtnHeight = 0;
        public double TestBtnHeight { get { return dTestBtnHeight; } set { dTestBtnHeight = value; OnPropertyChanged(); } }

        //! 가상 요청 주입 버튼 표시 높이.  Taeroo - 2026/08/11
        //  - 큐 적체/자동 폐기 확인용. Operator 권한일 때만 노출.
        double dTestMsgBtnHeight = 0;
        public double TestMsgBtnHeight { get { return dTestMsgBtnHeight; } set { dTestMsgBtnHeight = value; OnPropertyChanged(); } }

        bool bIsGrabStop = true;
        public bool IsGrabStop { get { return bIsGrabStop; } set { bIsGrabStop = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Page_Main.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Page_Main : Page
    {
        public Page_Main()
        {
            InitializeComponent();
        }

        public void fn_ViewTestArea()
        {
            datacontext.TestBtnHeight = G.USERLEVEL == EN_AUTHORITY.EN_ENGINEER ? 20 : 0;

            //! 가상 요청 주입 버튼은 Operator 권한일 때만 노출.  Taeroo - 2026/08/11
            datacontext.TestMsgBtnHeight = G.USERLEVEL == EN_AUTHORITY.EN_OPERATOR ? 30 : 0;
        }

        public void fn_WriteLog(string strMsg, bool bError)
        {
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate () {
                TextBlock tb = new TextBlock();
                tb.Text = strMsg;

                if (bError)
                    tb.Background = Brushes.Tomato;

                listLog.Items.Add(tb);
                if (listLog.Items.Count > 200)
                {
                    listLog.Items.RemoveAt(0);
                }

                listLog.ScrollIntoView(listLog.Items[listLog.Items.Count - 1]);
            }));
        }

        private void bn_testgrab_click(object sender, RoutedEventArgs e)
        {
            G.IMAGEMANAGER.InitAttachCount(0);
            G.IMAGEMANAGER.InitAttachCount(1);
            G.IMAGEMANAGER.InitAttachCount(2);
            G.IMAGEMANAGER.InitAttachCount(3);
            G.GRABBER.fn_GrabStart();
        }

        private void bn_testgrabstop_click(object sender, RoutedEventArgs e)
        {
            G.GRABBER.fn_GrabStop();
        }

        private void bn_testsave_click(object sender, RoutedEventArgs e)
        {
            OpenCvSharp.Mat mattemp = OpenCvSharp.Mat.Zeros(35715, 16384, OpenCvSharp.MatType.CV_8UC1);
            Task.Run(() => { 
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < 10; i++)
            {
                OpenCvSharp.Cv2.ImWrite($"D:\\Test{i}.jpg", mattemp);
            }
            G.WriteLog($"jpg : {sw.ElapsedMilliseconds:F2} ms");
            sw.Reset();
            sw.Start();
            for (int i = 0; i < 10; i++)
            {
                OpenCvSharp.Cv2.ImWrite($"D:\\Test{i}.bmp", mattemp);
            }
            G.WriteLog($"bmp : {sw.ElapsedMilliseconds:F2} ms");
            sw.Reset();
            sw.Start();
            for (int i = 0; i < 10; i++)
            {
                OpenCvSharp.Cv2.ImWrite($"D:\\Test{i}.png", mattemp);
            }
            G.WriteLog($"png : {sw.ElapsedMilliseconds:F2} ms");
            sw.Stop();
            });
        }

        private void GrabState1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (G.USERLEVEL != EN_AUTHORITY.EN_OPERATOR)
            {
                G.IMAGEMANAGER.SetCompl(0);
            }
        }

        private void GrabState2_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (G.USERLEVEL != EN_AUTHORITY.EN_OPERATOR)
            {
                G.IMAGEMANAGER.SetCompl(1);
            }
        }

        private void GrabState3_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (G.USERLEVEL != EN_AUTHORITY.EN_OPERATOR)
            {
                G.IMAGEMANAGER.SetCompl(2);
            }
        }

        private void GrabState4_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (G.USERLEVEL != EN_AUTHORITY.EN_OPERATOR)
            {
                G.IMAGEMANAGER.SetCompl(3);
            }
        }

        //private void bn_Cam4ViewImage_Click(object sender, RoutedEventArgs e)
        //{
        //    if (!G.IMAGEMANAGER.CamImage[3].Empty())
        //    {
        //        imgview4.SetImage(WriteableBitmapConverter.ToWriteableBitmap(G.IMAGEMANAGER.CamImage[3]));
        //    }
        //}

        //private void bn_Cam4ViewClear_Click(object sender, RoutedEventArgs e)
        //{
        //    imgview4.ClearCanvas();
        //}

        //private void bn_Cam3ViewClear_Click(object sender, RoutedEventArgs e)
        //{
        //    imgview3.ClearCanvas();
        //}

        //private void bn_Cam3ViewImage_Click(object sender, RoutedEventArgs e)
        //{
        //    if (!G.IMAGEMANAGER.CamImage[2].Empty())
        //    {
        //        imgview3.SetImage(WriteableBitmapConverter.ToWriteableBitmap(G.IMAGEMANAGER.CamImage[2]));
        //    }
        //}

        //private void bn_Cam2ViewClear_Click(object sender, RoutedEventArgs e)
        //{
        //    imgview2.ClearCanvas();
        //}

        //private void bn_Cam2ViewImage_Click(object sender, RoutedEventArgs e)
        //{
        //    if (!G.IMAGEMANAGER.CamImage[1].Empty())
        //    {
        //        imgview2.SetImage(WriteableBitmapConverter.ToWriteableBitmap(G.IMAGEMANAGER.CamImage[1]));
        //    }
        //}

        private void bn_Cam1ViewClear_Click(object sender, RoutedEventArgs e)
        {
            imgview1.ClearCanvas();
        }

        private void bn_Cam1ViewImage_Click(object sender, RoutedEventArgs e)
        {
            if (!G.IMAGEMANAGER.CamImage[0].Empty())
            {
                imgview1.SetImage(WriteableBitmapConverter.ToWriteableBitmap(G.IMAGEMANAGER.CamImage[0]));
            }
        }

        private void bn_ManualGrab_Click(object sender, RoutedEventArgs e)
        {
            datacontext.IsGrabStop = false;
            G.GrabStart();
        }

        //! 큐 적체 테스트용 가상 요청 주입.  Taeroo - 2026/08/11
        //  - 실제 START 수신 경로(ProtocallManager.MessageAnalyzer)에서 큐 적재 부분만 재현.
        //  - 그랩(G.GrabStart)과 상위 응답(SendAck)은 수행하지 않음.
        //    그랩이 완료되면 요청이 소비되어 큐가 쌓이지 않고, 상위로 가짜 응답이 나가면 안 되기 때문.
        //  - 적재된 요청은 SYSTEM.RequestTimeout 경과 후 자동 폐기되는지 확인하는 용도.
        int m_nTestMsgCount = 0;

        private void bn_TestPushRequest_Click(object sender, RoutedEventArgs e)
        {
            m_nTestMsgCount++;

            string strCellID1 = $"TEST{m_nTestMsgCount:D3}_1";
            string strCellID2 = $"TEST{m_nTestMsgCount:D3}_2";

            G.MSGPROC.fn_PushRequest(strCellID1, strCellID2);

            G.WriteLog($"[TEST] 가상 요청 적재. [{strCellID1} | {strCellID2}] 큐 : {G.MSGPROC.CellIDCount}");
        }
    }
}
