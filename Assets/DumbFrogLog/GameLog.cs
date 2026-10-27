using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DumbFrog.Logging
{
    // 테스트 케이스(TC) 번호를 붙여 게임 이벤트를 기록합니다.
    // 화면 오버레이 · Unity 콘솔 · CSV 파일(WebGL 제외) 세 곳에 동시에 남깁니다.
    public static class GameLog
    {
        public struct Entry
        {
            public float time;
            public string tc;
            public string message;
        }

        public const int MaxEntries = 300;
        public static bool ConsoleOutput = true;
        public static Font Font;
        public static int Count { get; private set; }
        public static string FilePath { get; private set; }
        public static readonly List<Entry> Entries = new List<Entry>();
        private static readonly Dictionary<string, float> lastWrite = new Dictionary<string, float>();
        private static StreamWriter writer;
        private static bool fileFailed;

        // minInterval > 0이면 같은 TC가 그 시간 안에 반복될 때 건너뜁니다(연속 충돌 등 반복 로그 방지).
        public static void Write(string tc, string message, float minInterval = 0f)
        {
            float now = Time.unscaledTime;
            if (minInterval > 0f)
            {
                float last;
                if (lastWrite.TryGetValue(tc, out last) && now - last < minInterval) return;
            }
            lastWrite[tc] = now;
            Entries.Add(new Entry { time = now, tc = tc, message = message });
            if (Entries.Count > MaxEntries) Entries.RemoveAt(0);
            Count++;
            if (ConsoleOutput) Debug.Log("[" + tc + "] " + message);
            WriteFile(now, tc, message);
        }

        public static void Clear()
        {
            Entries.Clear();
            lastWrite.Clear();
        }

        // 테스트 케이스 문서의 분류(A~H)입니다. TC 번호가 아니면 '-'
        public static readonly string[] CategoryNames = { "실행 및 메인 화면", "플레이어 이동 및 점프", "슬라이딩 및 스파이크",
            "공 물리 및 충돌", "CPU 인공지능", "득점 및 경기 진행", "일시정지 및 화면 전환", "UI, 애니메이션 및 설정" };
        private static readonly int[] categoryLast = { 15, 30, 50, 65, 80, 95, 105, 120 };

        public static char Category(string tc)
        {
            int number;
            if (tc == null || !tc.StartsWith("TC-") || !int.TryParse(tc.Substring(3), out number)) return '-';
            for (int i = 0; i < categoryLast.Length; i++) if (number <= categoryLast[i]) return (char)('A' + i);
            return '-';
        }

        public static string FormatTime(float seconds)
        {
            int minutes = (int)(seconds / 60f);
            return minutes.ToString("00") + ":" + (seconds - minutes * 60).ToString("00.00");
        }

        private static void WriteFile(float now, string tc, string message)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (fileFailed) return;
            try
            {
                if (writer == null)
                {
                    string folder = Path.Combine(Application.persistentDataPath, "TestLogs");
                    Directory.CreateDirectory(folder);
                    FilePath = Path.Combine(folder, "log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
                    // BOM을 넣어 엑셀에서 한글이 깨지지 않게 합니다.
                    writer = new StreamWriter(FilePath, false, new UTF8Encoding(true)) { AutoFlush = true };
                    writer.WriteLine("시각,경과(초),씬,분류,TC,내용");
                    Application.quitting += CloseFile;
                    Debug.Log("테스트 로그 파일: " + FilePath);
                }
                writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "," + now.ToString("0.000") + "," +
                    SceneManager.GetActiveScene().name + "," + Category(tc) + "," + tc + "," + Csv(message));
            }
            catch (Exception e)
            {
                fileFailed = true;
                Debug.LogWarning("테스트 로그 파일을 쓸 수 없습니다: " + e.Message);
            }
#endif
        }

        private static string Csv(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\n' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static void CloseFile()
        {
            if (writer == null) return;
            writer.Dispose(); writer = null;
        }
    }
}
