/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 * - 주식회사 태루 -
 *******************************************************************************
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
using FalconWpf;

namespace KEOC_ImageGrabber
{
    public class MainWindowDataContext : MVVMBase.IPropertyChanged
    {
        Page_SystemInfo pageSystem = new Page_SystemInfo();
        Page_Authority pageAuthority = new Page_Authority();
        Page_Communication pageCommunication = new Page_Communication();

        public Page_SystemInfo PageSystem { get { return pageSystem; } set { pageSystem = value; OnPropertyChanged(); } }
        public Page_Authority PageAuthority { get { return pageAuthority; } set { pageAuthority = value; OnPropertyChanged(); } }
        public Page_Communication PageCommunication { get { return pageCommunication; } set { pageCommunication = value; OnPropertyChanged(); } }

        public string CurrRecipe { get { return G.SYSTEM.CurrRecipeName; } set { G.SYSTEM.CurrRecipeName = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public ResourceDictionary resource = Application.Current.Resources;
        Page_Main mc_pageMain = new Page_Main();
        Page_Setup mc_pageSetup = new Page_Setup();

        public ImageViewer MainView1 { get { return mc_pageMain.imgview1; } }
        //public ImageViewer MainView2 { get { return mc_pageMain.imgview2; } }
        //public ImageViewer MainView3 { get { return mc_pageMain.imgview3; } }
        //public ImageViewer MainView4 { get { return mc_pageMain.imgview4; } }
        public ImageViewer SetupView { get { return mc_pageSetup.imgview; } }

        public int SetupCamIndex { get { return mc_pageSetup.datacontext.CamIndex; } }

        DispatcherTimer timer = new DispatcherTimer();

        const string CONFIGFILE = "ImageGrabber.xml";
        public MainWindow()
        {
            InitializeComponent();
            G.MAIN = this;

#if DEBUG
            G.USERLEVEL = EN_AUTHORITY.EN_ENGINEER;
#endif
            //! 시작 시점의 권한으로 테스트 영역 표시 상태를 초기화.  Taeroo - 2026/08/11
            //  - 기존에는 DEBUG 빌드에서만 호출되어, Release에서는 초기 권한(Operator)에
            //    해당하는 버튼이 권한을 한 번 변경하기 전까지 표시되지 않았음.
            mc_pageMain.fn_ViewTestArea();
            // Title에 버전 표시.
            this.Title += $" [{G.fn_GetVer()}]";

            // Timer 실행.
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += timer_Main;
            timer.Start();

            G.WriteLog($"Program Start.");
            // System Parameter Loading.
            fn_LoadSetting();

            if (XmlManager.LoadXml(G.SYSTEM.CurrRecipePath, G.CURRRECIPE))
            {
                G.MAIN.datacontext.CurrRecipe = G.CURRRECIPE.RecipeName;
                G.WriteLog($"Recipe Loaded. [{G.CURRRECIPE.RecipeName}]");
            }
            else
            {
                G.MAIN.datacontext.CurrRecipe = "-";
                G.WriteLog($"Recipe Load Fail. [{G.SYSTEM.CurrRecipePath}]", true);
            }

            // Initialize
            G.Init();
            fn_SelMenu(0);
        }

        public void fn_LoadSetting()
        {
            if (!File.Exists(CONFIGFILE))
            {
                XmlManager.SaveXml(CONFIGFILE, G.SYSTEM);
                G.WriteLog($"Config File Create : [{CONFIGFILE}]");
            }

            if (XmlManager.LoadXml(CONFIGFILE, G.SYSTEM))
                G.WriteLog($"Config File Load Success.");
            else
                G.WriteLog($"Config File Load Fail.", true);
        }

        private void bn_Menu_Click(object sender, RoutedEventArgs e)
        {
            Button bn = sender as Button;
            if (bn != null)
            {
                G.WriteLog($"{System.Reflection.MethodBase.GetCurrentMethod().Name} {bn.Content} Clicked");
                int idx = 0;
                int.TryParse(bn.Tag.ToString(), out idx);
                fn_SelMenu(idx);
            }
        }

        private void fn_SelMenu(int idx)
        {
            bnMain.Background = (SolidColorBrush)resource["ButtonUnSel"];
            bnSetup.Background = (SolidColorBrush)resource["ButtonUnSel"];
            switch(idx)
            {
                case 0:
                    frame.Content = mc_pageMain;
                    bnMain.Background = (SolidColorBrush)resource["ButtonSel"];
                    break;
                case 1:
                    if (G.USERLEVEL == EN_AUTHORITY.EN_OPERATOR)
                    {
                        datacontext.PageAuthority.fn_ChangeAuthority();
                    }
                    if (G.USERLEVEL > EN_AUTHORITY.EN_OPERATOR)
                    {
                        frame.Content = mc_pageSetup;
                        bnSetup.Background = (SolidColorBrush)resource["ButtonSel"];
                    }
                    else
                    {
                        fn_SelMenu(0);
                    }
                    break;
            }
        }

        public void fn_WriteLog(string strMsg, bool bError)
        {
            mc_pageMain.fn_WriteLog(strMsg, bError);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (G.USERLEVEL == EN_AUTHORITY.EN_OPERATOR)
            {
                datacontext.PageAuthority.fn_ChangeAuthority(false);
                if (G.USERLEVEL == EN_AUTHORITY.EN_OPERATOR)
                {
                    e.Cancel = true;
                    return;
                }
            }
            G.GrabStop();
            timer.Stop();
            G.Final();
            XmlManager.SaveXml(CONFIGFILE, G.SYSTEM);
        }

        private void timer_Main(object sender, EventArgs e)
        {
            datacontext.PageAuthority.datacontext.Auth = G.USERLEVEL;

            fn_UpdateState();

            // Ready State가 Ready가 되면 그랩 시작.
//             if (G.READYSTATE == EN_READYSTATE.Ready && G.USERLEVEL == EN_AUTHORITY.EN_OPERATOR)    G.GrabStart();
//             else                                        G.GrabStop();
        }

        private void fn_UpdateState()
        {
            bool bRet = true;

            // Check State
            bRet &= G.COMM.IsConnected;

            for (int i = 0; i < Define.CAM_COUNT; i++)
            {
                bRet &= G.CAMERA[i] != null ? G.CAMERA[i].IsConnected : false;
            }

            bRet &= G.LIGHT.fn_IsConnected();

            bRet &= G.GRABBER.IsInited;
            
            G.READYSTATE = bRet ? EN_READYSTATE.Ready : EN_READYSTATE.Alarm;
            mc_pageMain.datacontext.IsGrabStop = G.GRABSTATE != EN_GRABSTATE.Grab;

            mc_pageMain.datacontext.CellID = $"{G.MSGPROC.CellID} [{G.MSGPROC.CellIDCount}]";

            if (G.IMAGEMANAGER.IsImageCompalte != null)
            {
                mc_pageMain.datacontext.Cam1Grab = G.IMAGEMANAGER.IsImageCompalte[0] ? "Compl" : "Wait";
            }
        }

        public void fn_SetupUpdateAutority()
        {
            mc_pageSetup.fn_UpdateAutority();
            mc_pageMain.fn_ViewTestArea();
        }

        public void fn_AddClient(string strClient)
        {
            mc_pageSetup.listbox_client.Dispatcher.Invoke(new Action(() =>
            {
                mc_pageSetup.listbox_client.Items.Add(strClient);
            }));
        }

        public void fn_RemoveClient(string strClient)
        {
            mc_pageSetup.listbox_client.Dispatcher.Invoke(new Action(() =>
            {
                mc_pageSetup.listbox_client.Items.Remove(strClient);
            }));
        }
    }
}
