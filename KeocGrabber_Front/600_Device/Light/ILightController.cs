/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 *******************************************************************************
 */

namespace KeocGrabber_Front
{
    //! 조명 컨트롤러 공통 인터페이스.  2026/11/16
    //  - 현장별 조명 컨트롤러(PLS, 다우)를 시스템 파라메터(LightType)로 선택하기 위해 추가.
    //  - 채널 번호는 0 : 전체, 1~N : 개별 채널.
    interface ILightController
    {
        bool IsConnected { get; }

        /// <summary>화면/레시피에서 사용하는 채널 수.</summary>
        int ChannelCount { get; }
        /// <summary>밝기 최대값.</summary>
        int MaxValue { get; }

        void fn_InitPort();
        void fn_FinalPort();

        void SetLightValue(int ch, int value);
        int? GetLightValue(int ch);

        void LightOn(int ch);
        void LightOff(int ch);

        /// <summary>ON/OFF 상태 조회 후 내부 상태 갱신. 실패 시 false.</summary>
        bool GetLightOnOff();
        /// <summary>마지막으로 조회한 채널 ON 상태.</summary>
        bool IsLightOn(int ch);
    }
}
