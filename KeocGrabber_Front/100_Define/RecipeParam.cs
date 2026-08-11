/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 *******************************************************************************
 */
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeocGrabber_Front
{
    public class RecipeParam : MVVMBase.IPropertyChanged
    {
        string strRecipeName = "";
        string strRecipeWriteTime = "";
        public string RecipeName { get { return strRecipeName; } set { strRecipeName = value; OnPropertyChanged(nameof(RecipeName)); } }
        public string RecipeWriteTime { get { return strRecipeWriteTime; } set { strRecipeWriteTime = value; OnPropertyChanged(nameof(RecipeWriteTime)); } }

        int nCamExposure1 = 0;

        int nCamGain1 = 0;

        int nLightCh1 = 0;
        int nLightCh2 = 0;
        int nLightCh3 = 0;
        int nLightCh4 = 0;


        public int CamExposure1 { get { return nCamExposure1; } set { nCamExposure1 = value; OnPropertyChanged(nameof(CamExposure1)); } }

        public int CamGain1 { get { return nCamGain1; } set { nCamGain1 = value; OnPropertyChanged(nameof(CamGain1)); } }

        public int LightCh1 { get { return nLightCh1; } set { nLightCh1 = value; OnPropertyChanged(nameof(LightCh1)); } }
        public int LightCh2 { get { return nLightCh2; } set { nLightCh2 = value; OnPropertyChanged(nameof(LightCh2)); } }
        public int LightCh3 { get { return nLightCh3; } set { nLightCh3 = value; OnPropertyChanged(nameof(LightCh3)); } }
        public int LightCh4 { get { return nLightCh4; } set { nLightCh4 = value; OnPropertyChanged(nameof(LightCh4)); } }


        DataTable dtCropROI = new DataTable();
        public DataTable CropROI { get { return dtCropROI; } set { dtCropROI = value; OnPropertyChanged(nameof(CropROI)); } }
    }
}
