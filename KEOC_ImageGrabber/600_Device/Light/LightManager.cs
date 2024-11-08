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
    class LightManager
    {
        //VitLight mc_VitLight = new VitLight();
        //DawooLight mc_DawooLight = new DawooLight();
        PLSLight mc_PlsLight = new PLSLight();

        public LightManager()
        {

        }

        public bool fn_IsConnected()
        {
            return mc_PlsLight.IsConnected;
        }

        public void fn_Init()
        {
            //mc_VitLight.fn_InitPort();
            //mc_DawooLight.fn_InitPort();
            mc_PlsLight.fn_InitPort();
        }

        public void fn_Final()
        {
            //mc_VitLight.fn_FinalPort();
            //mc_DawooLight.fn_FinalPort();
            mc_PlsLight.fn_FinalPort();
        }

        public void fn_LightOnAll()
        {
            //for (int i = 0; i < mc_DawooLight.CtrlCount; i++)
            //{
            //    mc_DawooLight.LightOn(i);
            //}
            //mc_VitLight.LightOn(-1);
            mc_PlsLight.LightOn(0);
        }

        public void fn_LightOffAll()
        {
            //for (int i = 0; i < mc_DawooLight.CtrlCount; i++)
            //{
            //    mc_DawooLight.LightOff(i);
            //}
            //mc_VitLight.LightOff(-1);
            mc_PlsLight.LightOff(0);
        }

        public void fn_LightOn(int idx)
        {
            mc_PlsLight.LightOn(idx);
            //if (idx >= 0 && idx < Define.LIGHT_CTRL_CH_COUNT)
            //{
            //    if (idx < mc_DawooLight.CtrlCount)
            //    {
            //        mc_DawooLight.LightOn(idx);
            //    }
            //    else
            //    {
            //        mc_VitLight.LightOn(idx - mc_DawooLight.CtrlCount);
            //    }
            //}
        }

        public void fn_LightOff(int idx)
        {
            mc_PlsLight.LightOff(idx);
            //if (idx >= 0 && idx < Define.LIGHT_CTRL_CH_COUNT)
            //{
            //    if (idx < mc_DawooLight.CtrlCount)
            //    {
            //        mc_DawooLight.LightOff(idx);
            //    }
            //    else
            //    {
            //        mc_VitLight.LightOff(idx - mc_DawooLight.CtrlCount);
            //    }
            //}
        }

        public void fn_SetLightValue(int idx, int value)
        {
            mc_PlsLight.SetLightValue(idx, value);
            //if (idx >= 0 && idx < Define.LIGHT_CTRL_CH_COUNT)
            //{
            //    if (idx < mc_DawooLight.CtrlCount)
            //    {
            //        mc_DawooLight.SetLightValue(idx, value);
            //    }
            //    else
            //    {
            //        mc_VitLight.SetLightValue(idx - mc_DawooLight.CtrlCount + 1, value);
            //    }
            //}
        }

        public int? fn_GetLightValue(int idx)
        {
            int? nRtn = 0;
            nRtn = mc_PlsLight.GetLightValue(idx);
            //if (idx >= 0 && idx < Define.LIGHT_CTRL_CH_COUNT)
            //{
            //    if (idx < mc_DawooLight.CtrlCount)
            //    {
            //        nRtn = mc_DawooLight.GetLightValue(idx, 0);
            //    }
            //    else
            //    {
            //        nRtn = mc_VitLight.GetLightValue(idx - mc_DawooLight.CtrlCount + 1);
            //    }
            //}

            return nRtn;
        }

        public bool fn_IsLightOn(int idx)
        {
            bool bRet = false;
            bRet = mc_PlsLight.IsLightOn(idx);
            //if (idx >= 0 && idx < Define.LIGHT_CTRL_CH_COUNT)
            //{
            //    if (idx < mc_DawooLight.CtrlCount)
            //    {
            //        bRet = mc_DawooLight.IsLightOn(idx);
            //    }
            //    else
            //    {
            //        bRet = mc_VitLight.IsLightOn(idx - mc_DawooLight.CtrlCount);
            //    }
            //}

            return bRet;
        }

        public bool fn_GetOnOff(int idx)
        {
            bool bRet = false;
            bRet = mc_PlsLight.GetLightOnOff();
            //if (idx >= 0 && idx < Define.LIGHT_CTRL_CH_COUNT)
            //{
            //    if (idx < mc_DawooLight.CtrlCount)
            //    {
            //        mc_DawooLight.GetLightOnOff(idx);
            //    }
            //    else
            //    {
            //        mc_VitLight.GetLightOnOff();
            //    }
            //}

            return bRet;
        }
    }
}
