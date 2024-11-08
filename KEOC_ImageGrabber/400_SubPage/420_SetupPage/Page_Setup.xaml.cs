/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 * - 주식회사 태루 -
 *******************************************************************************
 */
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
using FalconWpf;
using Microsoft.Win32;
using OpenCvSharp;

namespace KEOC_ImageGrabber
{
    public class Page_SetupDataContext : MVVMBase.IPropertyChanged
    {
        Func<float, bool> delUpdateExposure = null;
        Func<float, bool> delUpdateGain = null;
        Func<int, int, bool> delUpdateLight = null;
        Action<int> delUpdateCamIndex = null;

        public Func<float, bool> DelUpdateExposure { set { delUpdateExposure = value; } }
        public Func<float, bool> DelUpdateGain { set { delUpdateGain = value; } }
        public Func<int, int, bool> DelUpdateLight { set { delUpdateLight = value; } }
        public Action<int> DelUpdateCamIndex { set { delUpdateCamIndex = value; } }

        bool bLightOn = false;
        public bool IsLightEnabled { get { return !bLightOn; } set { bLightOn = value; OnPropertyChanged(); } }
        public bool IsLightDisabled { get { return bLightOn; } }

        string strRecipePath = "";
        string strRecipeWriteTime = "";
        public string RecipePath { get { return strRecipePath; } set { strRecipePath = value; IsRecipeLoaded = strRecipePath != "" ? true : false; OnPropertyChanged(); } }
        public string RecipeWriteTime { get { return strRecipeWriteTime; } set { strRecipeWriteTime = value; OnPropertyChanged(); } }

        float fCamExposure1 = 0;
        
        float fCamGain1 = 0;

        int nLightValue = 0;
        int nLightValueCh1 = 0;
        int nLightValueCh2 = 0;
        int nLightValueCh3 = 0;
        int nLightValueCh4 = 0;

        int nCamIndex = 0;


        double dTestCtrlWidth = 120;

        bool bIsGrabbing = false;

        string strTopLightString = "On";
        //string strBotLightString = "On";
        bool bIsRecipeLoaded = false;
        

        public float CamExposure1 { get { return fCamExposure1; } set { fCamExposure1 = value; delUpdateExposure?.Invoke(value); OnPropertyChanged(nameof(CamExposure1)); } }

        public float CamGain1 { get { return fCamGain1; } set { fCamGain1 = value; delUpdateGain?.Invoke(value);  OnPropertyChanged(nameof(CamGain1)); } }

        public int LightValue { get { return nLightValue; } set { nLightValue = value; delUpdateLight?.Invoke(0, value); OnPropertyChanged(nameof(LightValue)); } }
        public int LightCh1 { get { return nLightValueCh1; } set { nLightValueCh1 = value; delUpdateLight?.Invoke(1, value); OnPropertyChanged(nameof(LightCh1)); } }
        public int LightCh2 { get { return nLightValueCh2; } set { nLightValueCh2 = value; delUpdateLight?.Invoke(2, value); OnPropertyChanged(nameof(LightCh2)); } }
        public int LightCh3 { get { return nLightValueCh3; } set { nLightValueCh3 = value; delUpdateLight?.Invoke(3, value); OnPropertyChanged(nameof(LightCh3)); } }
        public int LightCh4 { get { return nLightValueCh4; } set { nLightValueCh4 = value; delUpdateLight?.Invoke(4, value); OnPropertyChanged(nameof(LightCh4)); } }


        public int CamIndex { get { return nCamIndex; } set { nCamIndex = value; delUpdateCamIndex?.Invoke(value); OnPropertyChanged(nameof(CamIndex)); } }

        bool bIsAdmin = false;
        public bool IsAdmin { get { return bIsAdmin; } set { bIsAdmin = value; TestCtrlWidth = value ? 120 : 0; OnPropertyChanged(); } }

        public bool IsRecipeLoaded { get { return bIsRecipeLoaded; } set { bIsRecipeLoaded = value; OnPropertyChanged(); } }

        public double TestCtrlWidth { get { return dTestCtrlWidth; } set { dTestCtrlWidth = value; OnPropertyChanged(); } }

        public bool IsGrabable { get { return !bIsGrabbing; } }
        public bool IsGrabbing { get { return bIsGrabbing; } set { bIsGrabbing = value; OnPropertyChanged(); } }

        public string TopLightString { get { return strTopLightString; } set { strTopLightString = value; OnPropertyChanged(); } }
        //public string BotLightString { get { return strBotLightString; } set { strBotLightString = value; OnPropertyChanged(); } }

        int nSelectedROI = -1;
        public int SelectedROI { get { return nSelectedROI; } set { if (nSelectedROI != value) { nSelectedROI = value; OnPropertyChanged(); } } }

        DataTable dtCropROI = new DataTable();
        public DataTable DTCropROI { get { return dtCropROI; } set { dtCropROI = value; OnPropertyChanged(); } }

    }

    /// <summary>
    /// Page_Setup.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Page_Setup : Page
    {
        RecipeParam mc_loadedRecipe = new RecipeParam();
        public Page_Setup()
        {
            InitializeComponent();

            datacontext.DelUpdateExposure = cb_UpdateExposure;
            datacontext.DelUpdateGain = cb_UpdateGain;
            datacontext.DelUpdateLight = cb_UpdateLight;
            datacontext.DelUpdateCamIndex = cb_UpdateCamIndex;
            datacontext.IsGrabbing = G.GRABBER.fn_IsGrabbing(datacontext.CamIndex);

            fnInitTable();
        }

        public void fnInitTable()
        {
            datacontext.DTCropROI.Columns.Add("CellID");
            datacontext.DTCropROI.Columns.Add("X");
            datacontext.DTCropROI.Columns.Add("Y");
            datacontext.DTCropROI.Columns.Add("Width");
            datacontext.DTCropROI.Columns.Add("Height");

            datacontext.DTCropROI.Rows.Add("Cell 1", 0, 0, 0, 0);
            datacontext.DTCropROI.Rows.Add("Cell 2", 0, 0, 0, 0);

            datacontext.DTCropROI.ColumnChanged += DTCropROI_ColumnChanged;

            imgview.delUpdateRect = cb_UpdateROIRect;
            //imgview.SetROIRect();


        }

        private void DTCropROI_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            //int[] value = new int[datacontext.DTCropROI.Columns.Count];
            //for (int i = 1; i < value.Length; i++)
            //{
            //    try
            //    {
            //        value[i] = Convert.ToInt32(e.Row.ItemArray[i]);
            //    }
            //    catch { }
            //}
            //imgview.SetROIRect(value[1], value[2], value[3], value[4]);
            fnUpdateROIRect(datacontext.SelectedROI);
        }

        private void fnUpdateROIRect(int index)
        {
            if (index < 0) return;
            int[] value = new int[datacontext.DTCropROI.Columns.Count];
            for (int i = 1; i < value.Length; i++)
            {
                try
                {
                    //value[i] = Convert.ToInt32(e.Row.ItemArray[i]);
                    value[i] = Convert.ToInt32(datacontext.DTCropROI.Rows[index][i]);
                }
                catch { }
            }
            imgview.SetROIRect(value[1], value[2], value[3], value[4]);
        }

        private bool cb_UpdateROIRect(int idx, double x, double y, double w, double h)
        {
            datacontext.DTCropROI.Rows[idx]["X"] = (int)x;
            datacontext.DTCropROI.Rows[idx]["Y"] = (int)y;
            datacontext.DTCropROI.Rows[idx]["Width"] = (int)w;
            datacontext.DTCropROI.Rows[idx]["Height"] = (int)h;

            return false;
        }

        private void bn_ImageTest_Click(object sender, RoutedEventArgs e)
        {
            Mat matSrc = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToMat(imgview.fn_GetImageStream());
            G.IMAGEMANAGER.SetTestImage(matSrc, 0);
        }

        private void bn_Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "RecipeFile|*.xml";

            if (dlg.ShowDialog() == true)
            {
                LoadRecipe(dlg.FileName);
            }
        }

        public void LoadRecipe(string recipename)
        {
            if (XmlManager.LoadXml(recipename, mc_loadedRecipe))
            {
                fn_LoadRecipeFromObj();
                datacontext.DTCropROI = mc_loadedRecipe.CropROI.Copy();
                datacontext.RecipePath = recipename;
                datacontext.RecipeWriteTime = mc_loadedRecipe.RecipeWriteTime;

                imgview.SelectedObject = 0;
                fnUpdateROIRect(0);
                imgview.SelectedObject = 1;
                fnUpdateROIRect(1);
            }
        }

        private void bn_Save_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "RecipeFile|*.xml";

            if (dlg.ShowDialog() == true)
            {
                fn_SaveRecipeToObj();
                mc_loadedRecipe.RecipeName = dlg.SafeFileName;
                mc_loadedRecipe.RecipeWriteTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
                mc_loadedRecipe.CropROI = datacontext.DTCropROI.Copy();
                XmlManager.SaveXml(dlg.FileName, mc_loadedRecipe);
                datacontext.RecipePath = dlg.FileName;
                datacontext.RecipeWriteTime = mc_loadedRecipe.RecipeWriteTime;
            }
        }

        /// <summary>
        /// 로드된 레피시 클래스에서 UI로 값 업데이트.
        /// </summary>
        private void fn_LoadRecipeFromObj()
        {
            //datacontext
            List<PropertyInfo> listObj = new List<PropertyInfo>();
            listObj.AddRange(mc_loadedRecipe.GetType().GetProperties());

            List<PropertyInfo> listDatacontext = new List<PropertyInfo>();
            listDatacontext.AddRange(datacontext.GetType().GetProperties());
            for (int i = 0; i < listObj.Count; i++)
            {
                for (int j = 0; j < listDatacontext.Count; j++)
                {
                    if (listObj[i].Name == listDatacontext[j].Name)
                    {
                        listDatacontext[j].SetValue(datacontext, listObj[i].GetValue(mc_loadedRecipe));
                        break;
                    }

                }
            }
        }

        /// <summary>
        /// 저장 전 UI에서 레시피 클래스로 값 업데이트.
        /// </summary>
        private void fn_SaveRecipeToObj()
        {
            List<PropertyInfo> listObj = new List<PropertyInfo>();
            listObj.AddRange(mc_loadedRecipe.GetType().GetProperties());

            List<PropertyInfo> listDatacontext = new List<PropertyInfo>();
            listDatacontext.AddRange(datacontext.GetType().GetProperties());
            for (int j = 0; j < listDatacontext.Count; j++)
            {
                for (int i = 0; i < listObj.Count; i++)
                {
                    if (listObj[i].Name == listDatacontext[j].Name)
                    {
                        var tempValue = Convert.ChangeType(listDatacontext[j].GetValue(datacontext), listObj[i].PropertyType);
                        listObj[i].SetValue(mc_loadedRecipe, tempValue);
                        break;
                    }
                }
            }
        }

        private void bn_CamConfig_Click(object sender, RoutedEventArgs e)
        {
            Win_Vieworks cam = new Win_Vieworks();
            cam.Owner = Application.Current.MainWindow;
            cam.Show();
        }

        private void bn_Stop_Click(object sender, RoutedEventArgs e)
        {
            G.GRABBER.fn_GrabStop(datacontext.CamIndex);
            datacontext.IsGrabbing = G.GRABBER.fn_IsGrabbing(datacontext.CamIndex);
        }

        private void bn_Grab_Click(object sender, RoutedEventArgs e)
        {
            G.GRABBER.fn_GrabStart(datacontext.CamIndex, true);
            datacontext.IsGrabbing = G.GRABBER.fn_IsGrabbing(datacontext.CamIndex);
        }

        private bool cb_UpdateExposure(float fExposure)
        {
            G.CAMERA[datacontext.CamIndex]?.fn_SetExposureTime(fExposure);
            return false;
        }

        private bool cb_UpdateGain(float fGain)
        {
            G.CAMERA[datacontext.CamIndex]?.fn_SetDigitalGain(fGain);
            return false;
        }
        private bool cb_UpdateLight(int index, int nLight)
        {
            //datacontext.LightValue = nLight;
            G.LIGHT.fn_SetLightValue(index, nLight);
            return false;
        }

        private void cb_UpdateCamIndex(int index)
        {
            //G.GRABBER.fn_GrabStop();
            imgview.ClearCanvas();
            datacontext.IsGrabbing = G.GRABBER.fn_IsGrabbing(datacontext.CamIndex);
        }

        private void bn_LightOn_Click(object sender, RoutedEventArgs e)
        {
            datacontext.IsLightEnabled = true;
            G.LIGHT.fn_LightOnAll();
        }

        private void bn_LightOff_Click(object sender, RoutedEventArgs e)
        {
            datacontext.IsLightEnabled = false;
            G.LIGHT.fn_LightOffAll();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            fn_UpdateAutority();
            LoadRecipe(G.SYSTEM.CurrRecipePath);
        }

        public void fn_UpdateAutority()
        {
            datacontext.IsAdmin = G.USERLEVEL == EN_AUTHORITY.EN_ENGINEER;
        }

        private void fn_GetCamValue()
        {
            float? fValue = null;

            if (G.CAMERA.Length == Define.CAM_COUNT)
            {
                if (G.CAMERA[0] != null && G.CAMERA[0].IsConnected) { fValue = G.CAMERA[0].fn_GetExposureTime(); if (fValue != null) datacontext.CamExposure1 = (float)fValue; }

                if (G.CAMERA[0] != null && G.CAMERA[0].IsConnected) { fValue = G.CAMERA[0].fn_GetDigitalGain(); if (fValue != null) datacontext.CamGain1 = (float)fValue; }
            }
        }

        private void fn_GetLightValue()
        {
            int? nValue = null;
            if (G.LIGHT != null)
            {
                nValue = G.LIGHT.fn_GetLightValue(0); 
                if (nValue != null) datacontext.LightValue = (int)nValue;
            }
        }

        private void bn_SetRecipe_Click(object sender, RoutedEventArgs e)
        {
            CopyRecipe(mc_loadedRecipe, ref G.CURRRECIPE);
            G.MAIN.datacontext.CurrRecipe = G.CURRRECIPE.RecipeName;
            G.SYSTEM.CurrRecipePath = datacontext.RecipePath;
            G.WriteLog($"{G.MAIN.datacontext.CurrRecipe} Recipe Set Succ.[{G.SYSTEM.CurrRecipePath}]");
            MessageBox.Show($"{G.MAIN.datacontext.CurrRecipe} Recipe Set Succ.", "Recipe Set", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CopyRecipe(RecipeParam src, ref RecipeParam dst)
        {
            List<PropertyInfo> listSrc = new List<PropertyInfo>();
            listSrc.AddRange(src.GetType().GetProperties());

            List<PropertyInfo> listDst = new List<PropertyInfo>();
            listDst.AddRange(dst.GetType().GetProperties());
            for (int i = 0; i < listSrc.Count; i++)
            {
                for (int j = 0; j < listDst.Count; j++)
                {
                    if (listSrc[i].Name == listDst[j].Name)
                    {
                        var tempValue = Convert.ChangeType(listSrc[j].GetValue(src), listSrc[i].PropertyType);
                        listDst[i].SetValue(dst, tempValue);
                        break;
                    }
                }
            }
        }

        private void bn_PushImage_Click(object sender, RoutedEventArgs e)
        {
            WriteableBitmap wb = imgview.fn_GetImageStream();
            if (wb != null)
            {
                Mat mattemp = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToMat(wb);
                G.IMAGEMANAGER.SetTestImage(mattemp, 0);
            }
        }

        private void bn_LightOnAll_Click(object sender, RoutedEventArgs e)
        {
            UserButton ub = sender as UserButton;
            if (ub != null)
            {
                string strName = ub.Content as string;
                if (strName != null)
                {
                    if (strName == "Light Off All")
                    {
                        G.LIGHT.fn_LightOff(0);
                        ub.Content = "Light On All";
                    }
                    else
                    {
                        G.LIGHT.fn_LightOn(0);
                        ub.Content = "Light Off All";
                    }
                }
            }
        }

        private void bn_Ch_Click(object sender, RoutedEventArgs e)
        {
            UserButton ub = sender as UserButton;
            if (ub != null )
            {
                int idx = Convert.ToInt32(ub.Tag);
                string strNow = ub.Content.ToString();
                if (strNow == "On")
                {
                    G.LIGHT.fn_LightOn(idx);
                    ub.Content = "Off";
                }
                else
                {
                    G.LIGHT.fn_LightOff(idx);
                    ub.Content = "On";
                }
            }
        }

        private void DataGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            imgview.SelectedObject = datacontext.SelectedROI;
            //datacontext.SelectedROI
        }

        private void bn_ROIAlign_Click(object sender, RoutedEventArgs e)
        {
            Button ub = sender as Button;

            if (ub != null )
            {
                int nMode = 0;
                int.TryParse(ub.Tag.ToString(), out nMode);
                //! 1 : left, 2 : center, 3 : right, 4 : top, 5 : middle, 6 : bottom, 7 : vertical, 8 : horizontal.  Taeroo-kgseon - 2024/08/26  18:48
                imgview.AlignObject(nMode);
            }

        }

        private void bn_MessageTest_Click(object sender, RoutedEventArgs e)
        {
            byte[] testmsg = new byte[10];
            testmsg[0] = (byte)COMM_PROTOCALL.SOH;
            testmsg[1] = (byte)COMM_PROTOCALL.SOH; // Size
            testmsg[2] = 0x00;
            testmsg[3] = (byte)COMM_PROTOCALL.START;
            testmsg[4] = 0x00;
            testmsg[5] = (byte)COMM_PROTOCALL.NUL;
            testmsg[6] = (byte)COMM_PROTOCALL.VTO;
            testmsg[7] = (byte)COMM_PROTOCALL.SEND;
            testmsg[8] = (byte)COMM_PROTOCALL.ENQ_REQ;
            testmsg[9] = (byte)COMM_PROTOCALL.STX;

            string strcellid = "CellID_0,CellID_1";
            var cellid = Encoding.ASCII.GetBytes(strcellid);
            Array.Resize(ref testmsg, 10 + cellid.Length + 1);
            Array.Copy(cellid, 0, testmsg, 10, cellid.Length);
            testmsg[testmsg.Length - 1] = (byte)COMM_PROTOCALL.NUL;
            testmsg[1] = (byte)strcellid.Length;
            ProtocallManager.MessageAnalyzer(testmsg);
        }
    }
}
