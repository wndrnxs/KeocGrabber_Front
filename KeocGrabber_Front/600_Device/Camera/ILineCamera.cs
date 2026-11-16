/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 *******************************************************************************
 */
using System;

namespace KeocGrabber_Front
{
    //! 라인스캔 카메라 공통 인터페이스.  2026/11/16
    //  - 현장별 카메라(Vieworks, Dalsa Linea)를 시스템 파라메터(CameraType)로 선택하기 위해 추가.
    //  - 레시피/Setup 화면에서 사용하는 노출, 게인만 공통으로 정의.
    //  - 노출 시간 단위는 카메라와 무관하게 [us]로 통일.
    interface ILineCamera
    {
        Func<string, bool> WriteLog { set; }

        string Comport { get; }
        bool IsConnected { get; }

        void fn_Init(string comport);
        void fn_Final();

        /// <summary>노출 시간 설정. [us]</summary>
        void fn_SetExposureTime(float value);
        /// <summary>노출 시간 조회. [us] 실패 시 null.</summary>
        float? fn_GetExposureTime();

        void fn_SetDigitalGain(float value);
        float? fn_GetDigitalGain();
    }
}
