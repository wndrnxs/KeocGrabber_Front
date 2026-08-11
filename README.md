> 해당 소스는 현장 유지보수 목적 외 사용을 금합니다.

# KeocGrabber_Front — 정면 시야각(Front Angle View) 이미지 그랩 시스템

한국전광(KEOC) 미러 글라스 **정면 시야각** 검사 라인의 이미지 그랩 PC 소프트웨어입니다. Master PC로부터 TCP/IP로 그랩 요청을 받아 Matrox 프레임그래버 + Vieworks 라인스캔 카메라로 이미지를 촬영하고, 레시피에 정의된 셀(Cell) 2개의 ROI로 크롭해 저장한 뒤 완료를 Master에 통보합니다.

## 기술 스택

- C# / WPF, .NET Framework 4.7.2 (WinExe)
- 프레임그래버: Matrox Imaging Library — Coaxlink 보드
- 카메라: Vieworks 라인스캔 카메라 (GenICam SDK가 아닌 전용 시리얼 ASCII 커맨드로 직접 제어)
- 조명 컨트롤러: PLS 시리얼 프로토콜
- 이미지 처리: OpenCvSharp4 4.5.3
- 통신: TCP/IP(Master 연동, 자체 바이너리 프레임 프로토콜), RS-232(카메라/조명)

## 빌드

- Visual Studio 2022, `KeocGrabber_Front.sln`
- 외부 참조 DLL: `Lib/Coaxlink_NetApi.dll`, `Lib/Matrox.MatroxImagingLibrary.dll` (레포 루트 `Lib/` 기준 상대경로 — Matrox SDK 설치 환경 필요)
- NuGet: `packages.config` 복원 필요 (OpenCvSharp4 계열 등)
- 플랫폼: x64 전용. 솔루션 구성상 `Debug|Any CPU`, `Release|Any CPU`가 모두 실제로는 x64 구성(`Debug|x64` / `Release|x64`)에 매핑됩니다.
- 실행 파일 출력 경로: `KeocGrabber_Front\bin\x64\Debug\`, `KeocGrabber_Front\bin\x64\Release\` (프로젝트 폴더 기준). `csproj`에 남아있는 최상위 `..\binD\`, `..\bin\` 경로는 AnyCPU 전용 설정이라 실제로는 사용되지 않습니다.

## 폴더 구조

숫자 접두사로 레이어를 구분합니다.

| 폴더 | 역할 |
|---|---|
| `010_Common` | MVVM 베이스, XML 직렬화(`XmlManager`), 다국어(`Localization`) |
| `020_UserControl` | 커스텀 WPF 컨트롤 (ImageViewer, UserButton/Combo/Param/Progress/Slider) |
| `100_Define` | 전역 상태(`Global.cs`), 레시피/시스템 파라미터, 컨버터 |
| `300_Class` | 비즈니스 로직 — `Comm/`(통신), `Image/`(이미지 처리), `DiskManager`, `Logger` |
| `400_SubPage` | 화면 — `401_SubContent`(공용 서브페이지), `410_MainPage`, `420_SetupPage`, 인증/Vieworks 팝업 |
| `600_Device` | 장비 드라이버 — `Camera/`(Matrox/Vieworks), `Light/`(PLS 등) |

## 동작 흐름

1. 시작 시 뮤텍스(`KeocGrabber_Front`)로 중복 실행을 차단합니다.
2. `MainWindow` 로드 시 시스템 설정(`ImageGrabber.xml`)과 현재 레시피 XML을 로드합니다.
3. `G.Init()`에서 DiskManager/Logger/메시지 처리 스레드를 시작하고, Matrox 그래버·카메라·조명을 초기화한 뒤 TCP 서버를 Master 대기 포트로 엽니다.
4. Master가 `START` 프레임(셀 ID 2개)을 보내면 조명 ON → 트리거 대기 → 라인스캔 그랩 → 목표 높이 도달 시 조명 OFF·그랩 종료 순으로 진행합니다.
5. 완성된 이미지는 레시피에 정의된 ROI 2개로 크롭되어 저장되고, Master에 `END`(완료) 프레임으로 응답합니다.
6. 그랩이 설정된 타임아웃(기본 20초) 내에 끝나지 않으면 워치독이 강제로 조명을 끄고 그랩을 중단하여, 조명이 켜진 채로 그랩이 계속되어 하단이 어두운 불량 이미지가 만들어지는 것을 방지합니다.

## 통신 프로토콜 (Master ↔ Grab PC)

이 앱은 현재 **TCP 서버**로만 동작합니다 (`SYSTEM.MasterPort`, 기본 20000). 프로젝트에는 `TCPIPClient.cs`도 포함되어 있지만 실제로는 어디서도 사용되지 않는 미사용 코드입니다.

고정 10바이트 헤더 + payload + trailing NUL 구조입니다.

| Byte | 1 | 2-3 | 4-5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|
| 필드 | SOH | Length | CMD | ACK/NCK | Dir | COM | ENQ | STX |

- `CMD`: `START(0x66)` 그랩 요청, `END(0x67)` 완료 응답, `STATUS(0x68)` 하트비트
- `Dir`: `VTO(0x01)` Master→장비, `OTV(0x10)` 장비→Master
- Payload: ASCII, `cellid1,cellid2`

시퀀스:

- Master → `START` + `cellid1,cellid2` → 장비가 카메라/조명 준비 상태를 ACK/NAK로 즉시 회신한 뒤 그랩을 시작합니다.
- 두 셀 이미지 저장이 끝나면 장비 → `END` + `cellid1,cellid2` (가장 최근 연결된 클라이언트로 전송).
- 클라이언트가 5초 이상 응답이 없으면 `STATUS` 하트비트 프레임을 전송합니다.

> 이전 문서의 "LotID / Phase1·2 Ready" 개념은 현재 코드와 일치하지 않습니다. 현재는 셀 ID 2개 단위의 START/END 요청-응답 구조입니다.

## 카메라 / 그랩 제어

- **프레임그래버**: Matrox Coaxlink(MIL). `GrabberManager` → `MatroxGrabber`가 DCF 파일(`SYSTEM.Camera1DCF`)로 보드를 초기화합니다. 라인스캔 특성상 여러 청크로 나눠 그랩한 뒤 `ImageManager`에서 이어붙입니다.
- **카메라**: Vieworks 라인스캔 카메라를 RS-232 ASCII 커맨드(115200-8-N-1)로 제어합니다(`VieworksCamera.cs`). 노출시간·게인·트리거 모드·데이터 비트 등을 `s**`/`g**` 커맨드 쌍으로 설정·조회합니다.
- **동시 카메라 수**: 1대(`Define.CAM_COUNT = 1`)로 고정되어 있습니다. 코드에는 4채널 구조가 남아 있지만 2~4번 카메라 관련 로직은 모두 주석 처리된 상태입니다.
- **그랩 타임아웃 워치독**(`Global.cs`): 설정된 시간(기본 20000ms) 경과 시 강제 종료합니다. 락(lock) 내부에서는 상태 전이만 처리하고, 조명 OFF·그랩 정지 같은 실제 장비 제어는 락 밖에서 수행합니다 — 그랩 정지 호출이 진행 중인 그랩 콜백의 종료를 기다리는 동안, 그 콜백이 같은 락을 기다리며 서로 교착되는 문제를 피하기 위한 구조입니다.

## 조명 제어

`LightManager`가 조명 컨트롤러를 감싸며, 현재 실제로 연결되는 것은 **PLS 컨트롤러**(`PLSLight.cs`, 115200-8-N-1, `*W{ch}{value}$` / `*O{ch}{on/off}$` 형식의 ASCII 명령) 하나뿐입니다. `DawooLight.cs`, `VitLight.cs` 드라이버도 프로젝트에 포함되어 있지만 `LightManager` 내부의 인스턴스화 코드가 모두 주석 처리되어 있어 현재는 미사용 상태입니다.

## 이미지 저장

- 저장 경로: `{SYSTEM.ImagePath}/yyyy/MM/dd/{CellID}/{CellID}.{ImageExt}` (기본 확장자 `bmp`). 동일 파일이 이미 있으면 `_1`, `_2`... 순으로 자동 넘버링합니다.
- 저장 직후 `{SYSTEM.SharedPath}` 폴더로 추가 복사되며(공유 폴더 백업), 이 복사가 실패해도 원본 저장에는 영향을 주지 않습니다.
- 보관 정책: 로그(csv)는 설정된 보관일수(기본 30일) 경과 시 삭제됩니다. **이미지는 날짜 기준이 아니라 저장 드라이브 사용률이 90%를 넘으면 오래된 것부터 드라이브 전체 용량의 1%씩 정리**하는 방식입니다.
- 이전 문서의 "SSD 1TB + HDD 14TB×2 듀얼 드라이브 라우팅"은 현재 코드에는 구현되어 있지 않습니다. 관련 설정 필드만 있고 실제 분기 로직은 없어, 다중 드라이브 저장이 필요하면 추가 구현이 필요합니다.

## 로그

`Logger.cs`가 비동기 큐와 전용 스레드로 로그를 기록합니다.

- 경로: `{SYSTEM.LogPath}/yyyy/MM/dd/*.csv` (기본 `D:/Angleview/`)
- 파일 분류: 에러 → `Error.csv`, 통신 → `Communication.csv`, 그 외(그래버/조명/카메라/디스크/전역) → `Trace.csv`
- 보관 정책은 이미지/로그 정리를 담당하는 DiskManager의 보관일수 정책을 그대로 따릅니다.

## 설정 / 레시피

XML 직렬화는 리플렉션 기반 커스텀 시리얼라이저(`XmlManager`)를 사용합니다.

- **시스템 설정**(`SystemParam`, `ImageGrabber.xml`): Master 포트(기본 20000), 이미지/로그 경로 및 확장자, 보관일수, 카메라/조명 COM 포트, DCF 파일, 그랩 높이(기본 32768), 그랩 타임아웃(기본 20000ms), 요청 큐 만료시간(기본 300000ms = 5분) 등을 담고 있습니다.
- **레시피**(`RecipeParam`, 별도 XML 파일, Setup 화면에서 로드/저장): 노출시간, 게인, 조명 채널 1~4 값, 셀 1/2의 크롭 ROI(X, Y, Width, Height)를 담고 있습니다.

## 화면 구성

- **Main**: 운영자 메인 화면. 셀 ID/큐 상태, 그랩 완료 표시, 로그 리스트, 이미지 뷰어를 표시합니다. Engineer 권한에서는 수동 그랩/저장 포맷 벤치마크 등 테스트 버튼이 노출됩니다.
- **Process**: 상태 표시등(Ready/Grab/End/Alarm).
- **Authority**: 현재 권한 표시 및 로그인 창(Operator/Maintenance/Engineer 전환) 호출.
- **Communication**: Master 연결, 그래버/카메라/조명 연결 상태 표시.
- **SystemInfo**: CPU/RAM/드라이브 사용량 모니터링.
- **Setup**: 레시피 로드/저장, 라이브 프리뷰, 조명/노출/게인 조정, ROI 편집(드래그 정렬 포함), 수동 그랩, 카메라 레지스터 직접 제어 창 호출, 프로토콜 테스트용 가상 START 프레임 발생 버튼을 제공합니다. Engineer 권한에서만 전체 기능이 노출됩니다.

## 권한

Operator / Maintenance / Engineer 3단계이며, 로그인 창에서 비밀번호로 전환합니다(Operator는 비밀번호 없음).

## 알려진 레거시 요소

이관받은 코드 기준으로, 정리 여부를 판단해야 할 미사용/불완전 구현이 남아 있습니다.

- `TCPIPClient.cs`: 미사용 (서버 전용 구조로 이미 전환됨)
- `DiskManager`의 비동기 저장 큐 경로: 미사용 (실제 저장은 동기 호출로만 이루어짐)
- `LightManager`의 Dawoo/Vit 조명 드라이버: 코드는 남아있으나 비활성화 상태
- `SystemParam.SaveDrive`: 필드만 있고 실제 분기 로직 없음 (듀얼 드라이브 저장 미구현)
