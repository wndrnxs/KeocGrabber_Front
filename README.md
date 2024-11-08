 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 * - 주식회사 태루 -
 *******************************************************************************
 
# 한국 전광 Image Grabber Project.
# 정면 시야각
## Spec
    1. 개발언어 : c#, WPF
    2. 통신 : tcpip, Light Serial
    2.1. TCP/IP
       - LotID 수신
       - Data 요청 수신 (Phase 1, Phase 2)
      - Data 완료 발신 (Phase 1 Ready, Phase 2 Ready)
    2.2. 이미지 전송
      - 파일 저장.
    2.3. Light Controller (Serial)
      - 1개 프로토콜 (조명컨트롤러 1개)
      - PLS Controller
    2.4. Camera (Serial, Grabber Board)
      - Vieworks Cam Control SDK 및 예제
      - Vieworks DCF 파일. (H/W 트리거 및 Grab Start IO 설정 파일 필요)
    
    3. 저장
    3.1. 이미지 저장
      - 기간 : 30일
      - 저장 확장자 : ?
     - 저장 경로 : YYYY\MM\DD\LotID.bmp
     - 저장 드라이브 : ssd 1tb, hdd 14tb * 2
    3.2. 로그 저장
      - 기간 : 30일
    
    4. 시퀀스
    4.1. Master -> Grab PC
      - LotID : 시점이 언제인지?
      - Request Image (ex. Phase 1, Phase 2)
    4.2. Grab PC -> Master
      - Grab 완료 (ex . Phase 1 Ready, Phase 2 Ready)