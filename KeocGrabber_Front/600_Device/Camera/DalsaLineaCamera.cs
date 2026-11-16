/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 *******************************************************************************
 */
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KeocGrabber_Front
{
    //! Teledyne DALSA Linea CL 라인스캔 카메라. (LA-CM-04K08A-00-R)  2026/11/16
    //  - Camera Link 시리얼(그래버 COM 포트)로 ASCII 3글자 명령(TLC)을 사용.
    //  - 포트 : 9600, 8, None, 1, 흐름제어 없음. 명령 끝은 CR(0x0D), 응답 끝은 "USER>" 프롬프트.
    //  - 전원 투입 후 기본은 GenCP 모드이므로 ESC(0x1B)를 보내야 ASCII 모드로 전환됨.
    //    (카메라에 DEK 1이 설정되어 있으면 바로 ASCII 모드. 이 경우 ESC는 무시됨.)
    //  - 값 조회는 gcp(Display Camera Configuration) 출력을 파싱.
    //  - 참고 : Linea CL Series Camera User's Manual, Appendix B: ASCII Commands.
    class DalsaLineaCamera : ILineCamera
    {
        Func<string, bool> delWriteLog = null;
        public Func<string, bool> WriteLog { set { delWriteLog = value; } }

        SerialComm m_comm = new SerialComm();

        public string Comport { get { return m_comm.ConnPort; } }
        public bool IsConnected { get { return m_comm != null ? m_comm.IsConnected : false; } }

        const int BAUDRATE = 9600;
        const string PROMPT = ">";                  // "USER>" 프롬프트 끝 문자.

        // 모델 사양. (4k, 80kHz)
        const int EXPOSURE_MIN_NS = 4000;
        const int EXPOSURE_MAX_NS = 3332000;
        const float GAIN_MIN = 1.0f;
        const float GAIN_MAX = 10.0f;

        int m_nRecieveTimeout = 5000;               // gcp 출력이 길어 9600bps에서 1초 이상 걸림.

        readonly object m_lockCmd = new object();   // 조회 명령 동시 실행 방지.
        string m_strRecvBuff = "";
        volatile string m_strExpect = null;                  // 조회 중인 명령의 응답 판별 문자열.
        volatile string m_strQueryResponse = null;

        //! ASCII 모드 진입 실패 원인 확인용 수신 원문.  2026/11/16
        //  - 무응답(보레이트/배선 문제)과 응답 형식 차이를 로그로 구분하기 위해 진입 확인 중에만 기록.
        readonly StringBuilder m_sbRaw = new StringBuilder();
        volatile bool m_bRecordRaw = false;
        const int RAW_LOG_MAX = 300;

        public void fn_Init(string comport)
        {
            bool bRet = m_comm.Open(comport, BAUDRATE, 8, Parity.None, StopBits.One);
            m_comm.ReciveDataevent = RecvData;
            G.WriteLog($"Dalsa Linea Open {(bRet ? "Succ." : "Fail.")} [{comport}]");

            if (bRet)
            {
                fn_EnterAsciiMode();
            }
        }

        public void fn_Final()
        {
            m_comm.Close();
            m_comm.ReciveDataevent = null;
        }

        /// <summary>
        /// GenCP 모드에서 ASCII 명령 모드로 전환.
        /// </summary>
        private void fn_EnterAsciiMode()
        {
            lock (m_sbRaw) m_sbRaw.Clear();
            m_bRecordRaw = true;
            m_comm.WriteValue(new byte[] { 0x1B }, true, false);    // ESC + CR

            // 모델 확인으로 ASCII 모드 진입 여부 판단. 시작 화면이 멈추지 않도록 별도 스레드에서 확인.
            Task.Run(() =>
            {
                string strResp = fn_Query("gcp", "Model");
                m_bRecordRaw = false;
                Match m = strResp != null ? Regex.Match(strResp, @"Model\s+(\S+)") : Match.Empty;
                if (m.Success)
                {
                    G.WriteLog($"Dalsa Linea ASCII Mode. Model : {m.Groups[1].Value}");
                }
                else
                {
                    G.WriteLog($"Dalsa Linea ASCII Mode 진입 실패. 카메라 전원/포트 확인 필요. [{Comport}]", true);
                    G.WriteLog($"Dalsa Linea 수신 데이터 : {fn_RawText()}", true);
                }
            });
        }

        /// <summary>
        /// 기록한 수신 원문을 로그용 문자열로 변환. 제어 문자는 &lt;0xNN&gt;으로 표기.
        /// </summary>
        private string fn_RawText()
        {
            string strRaw;
            lock (m_sbRaw) strRaw = m_sbRaw.ToString();
            if (strRaw.Length == 0)
                return "없음 (카메라 무응답. 보레이트/케이블/카메라 전원 확인)";

            StringBuilder sb = new StringBuilder();
            foreach (char c in strRaw)
            {
                if (sb.Length >= RAW_LOG_MAX) { sb.Append("..."); break; }
                if (c == '\r') sb.Append("<CR>");
                else if (c == '\n') sb.Append("<LF>");
                else if (c < 0x20 || c > 0x7E) sb.Append($"<0x{(int)c:X2}>");
                else sb.Append(c);
            }
            return $"[{strRaw.Length} byte] {sb}";
        }

        private void RecvData(byte[] buff)
        {
            if (m_bRecordRaw)
            {
                lock (m_sbRaw)
                {
                    foreach (byte b in buff) m_sbRaw.Append((char)b);
                }
            }

            m_strRecvBuff += Encoding.ASCII.GetString(buff);

            // 프롬프트 이전까지가 한 명령의 응답. 한 번에 여러 응답이 올 수 있으므로 반복.
            int idx;
            while ((idx = m_strRecvBuff.IndexOf(PROMPT)) >= 0)
            {
                string strResp = m_strRecvBuff.Substring(0, idx);
                m_strRecvBuff = m_strRecvBuff.Remove(0, idx + PROMPT.Length);

                // 에러 응답 기록. (에러 코드는 GenICam과 동일. 예: 0x8002 Invalid Parameter)
                if (strResp.IndexOf("rror", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    strResp.IndexOf("0x", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    G.WriteLog($"Dalsa Linea Error Response. [{strResp.Trim()}]", true);
                }

                // 설정 명령의 응답(프롬프트)과 섞이지 않도록 조회 응답은 판별 문자열로 구분.
                string strExpect = m_strExpect;
                if (strExpect != null && strResp.IndexOf(strExpect, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    m_strQueryResponse = strResp;
                }
            }
        }

        /// <summary>
        /// 설정 명령 전송. 응답을 기다리지 않음.
        /// </summary>
        private void fn_WriteValue(string msg)
        {
            m_comm.WriteValue(msg, true, false);
            delWriteLog?.Invoke(msg);
        }

        /// <summary>
        /// 조회 명령 전송 후 프롬프트까지의 응답을 반환.
        /// </summary>
        /// <param name="msg">조회 명령</param>
        /// <param name="expect">응답에 포함되어야 하는 문자열</param>
        /// <returns>응답 문자열. 타임아웃 시 null.</returns>
        private string fn_Query(string msg, string expect)
        {
            lock (m_lockCmd)
            {
                m_strQueryResponse = null;
                m_strExpect = expect;
                fn_WriteValue(msg);

                Stopwatch sw = Stopwatch.StartNew();
                string strResp;
                while ((strResp = m_strQueryResponse) == null)
                {
                    Thread.Sleep(50);
                    if (sw.ElapsedMilliseconds > m_nRecieveTimeout)
                    {
                        delWriteLog?.Invoke("Time-Out");
                        break;
                    }
                }
                m_strExpect = null;
                return strResp;
            }
        }

        /// <summary>
        /// gcp 출력에서 항목 값을 추출. (예: "Exp. Time        44444 [ns]")
        /// </summary>
        private static string fn_ParseGcp(string gcp, string item)
        {
            if (gcp == null)
                return null;
            Match m = Regex.Match(gcp, Regex.Escape(item) + @"\s+([-+]?[0-9]*\.?[0-9]+)");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>
        /// 노출 시간 설정. [us]
        /// 내부 노출 모드(SEM 0)에서만 적용됨. 라인 주기 > 노출 시간 + 1us 이어야 함.
        /// </summary>
        public void fn_SetExposureTime(float value)
        {
            int nNs = (int)Math.Round(value * 1000.0);
            if (nNs < EXPOSURE_MIN_NS || nNs > EXPOSURE_MAX_NS)
            {
                G.WriteLog($"Dalsa Linea Exposure 범위 초과. [{value} us] ({EXPOSURE_MIN_NS / 1000.0}~{EXPOSURE_MAX_NS / 1000.0} us)", true);
                return;
            }
            fn_WriteValue($"set {nNs}");
        }

        public float? fn_GetExposureTime()
        {
            string strValue = fn_ParseGcp(fn_Query("gcp", "Exp. Time"), "Exp. Time");
            float fNs;
            if (strValue != null && float.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out fNs))
                return fNs / 1000.0f;
            return null;
        }

        /// <summary>
        /// 시스템 게인 설정. [1.0~10.0 배]
        /// </summary>
        public void fn_SetDigitalGain(float value)
        {
            if (value < GAIN_MIN || value > GAIN_MAX)
            {
                G.WriteLog($"Dalsa Linea Gain 범위 초과. [{value}] ({GAIN_MIN}~{GAIN_MAX})", true);
                return;
            }
            fn_WriteValue($"ssg 0 f{value.ToString("0.00", CultureInfo.InvariantCulture)}");
        }

        public float? fn_GetDigitalGain()
        {
            string strValue = fn_ParseGcp(fn_Query("gcp", "System Gain"), "System Gain");
            float fGain;
            if (strValue != null && float.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out fGain))
                return fGain;
            return null;
        }
    }
}
