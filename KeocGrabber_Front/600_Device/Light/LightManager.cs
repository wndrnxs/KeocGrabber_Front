/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 *******************************************************************************
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeocGrabber_Front
{
    class LightManager
    {
        //! 조명 컨트롤러는 시스템 파라메터(LightType)에 따라 fn_Init에서 생성.  2026/11/16
        //  - 시스템 파라메터 로드 전에 생성되는 객체이므로 생성자에서 만들지 않음.
        //  - 채널 번호는 0 : 전체, 1~N : 개별 채널.
        ILightController m_light = null;

        public LightManager()
        {

        }

        public bool fn_IsConnected()
        {
            return m_light?.IsConnected == true;
        }

        /// <summary>조명 채널 수. 초기화 전에는 4.</summary>
        public int fn_GetChannelCount()
        {
            return m_light != null ? m_light.ChannelCount : 4;
        }

        /// <summary>조명 밝기 최대값. 초기화 전에는 4000.</summary>
        public int fn_GetMaxValue()
        {
            return m_light != null ? m_light.MaxValue : 4000;
        }

        public void fn_Init()
        {
            switch ((G.SYSTEM.LightType ?? "").Trim().ToUpper())
            {
                case "DAWOO":
                    m_light = new DawooLight();
                    break;
                case "PLS":
                    m_light = new PLSLight();
                    break;
                default:
                    G.WriteLog($"알 수 없는 LightType. [{G.SYSTEM.LightType}] PLS로 동작.", true);
                    m_light = new PLSLight();
                    break;
            }
            m_light.fn_InitPort();
        }

        public void fn_Final()
        {
            m_light?.fn_FinalPort();
        }

        public void fn_LightOnAll()
        {
            m_light?.LightOn(0);
        }

        public void fn_LightOffAll()
        {
            m_light?.LightOff(0);
        }

        public void fn_LightOn(int idx)
        {
            m_light?.LightOn(idx);
        }

        public void fn_LightOff(int idx)
        {
            m_light?.LightOff(idx);
        }

        public void fn_SetLightValue(int idx, int value)
        {
            m_light?.SetLightValue(idx, value);
        }

        public int? fn_GetLightValue(int idx)
        {
            return m_light?.GetLightValue(idx);
        }

        public bool fn_IsLightOn(int idx)
        {
            return m_light?.IsLightOn(idx) == true;
        }

        public bool fn_GetOnOff(int idx)
        {
            return m_light?.GetLightOnOff() == true;
        }
    }
}
