using UnityEngine;

namespace DumbFrog.Logging
{
    // 모든 씬 위에 최근 테스트 로그를 띄웁니다. F1: 표시/숨기기, F2: 위치 변경, F3: 줄 수 변경, F4: 분류(A~H) 필터
    public sealed class GameLogOverlay : MonoBehaviour
    {
        public static bool Visible = true;
        private static readonly int[] lineOptions = { 8, 14, 4 };
        private int corner, lineOption, filter;
        private GUIStyle lineStyle, headerStyle;
        private Font styledFont;
        private Texture2D backdrop;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (FindObjectOfType<GameLogOverlay>() != null) return;
            GameObject obj = new GameObject("GameLogOverlay");
            DontDestroyOnLoad(obj);
            obj.AddComponent<GameLogOverlay>();
        }

        private void Awake()
        {
            backdrop = new Texture2D(1, 1);
            backdrop.SetPixel(0, 0, new Color(0.02f, 0.03f, 0.07f, 0.62f));
            backdrop.Apply();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) Visible = !Visible;
            if (Input.GetKeyDown(KeyCode.F2)) corner = (corner + 1) % 4;
            if (Input.GetKeyDown(KeyCode.F3)) lineOption = (lineOption + 1) % lineOptions.Length;
            if (Input.GetKeyDown(KeyCode.F4)) filter = (filter + 1) % (GameLog.CategoryNames.Length + 1);
        }

        private void BuildStyles()
        {
            styledFont = GameLog.Font;
            lineStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15, wordWrap = false, clipping = TextClipping.Clip };
            lineStyle.normal.textColor = Color.white;
            lineStyle.padding = new RectOffset(0, 0, 0, 0);
            headerStyle = new GUIStyle(lineStyle) { fontSize = 12 };
            headerStyle.normal.textColor = new Color(0.7f, 0.76f, 0.86f);
            if (styledFont != null) lineStyle.font = headerStyle.font = styledFont;
        }

        private void OnGUI()
        {
            if (!Visible) return;
            if (lineStyle == null || styledFont != GameLog.Font) BuildStyles();
            // 1280x720 기준으로 그린 뒤 화면 크기에 맞춰 확대합니다.
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = 560f, lineHeight = 19f, refWidth = Screen.width / scale;
            int lines = lineOptions[lineOption];
            float height = 26f + lines * lineHeight;
            float x = corner == 1 || corner == 2 ? refWidth - width - 10f : 10f;
            float y = corner >= 2 ? 720f - height - 10f : 150f;
            Rect box = new Rect(x, y, width, height);
            GUI.DrawTexture(box, backdrop);
            char category = filter == 0 ? '*' : (char)('A' + filter - 1);
            string filterName = filter == 0 ? "전체" : category + ". " + GameLog.CategoryNames[filter - 1];
            GUI.Label(new Rect(x + 8f, y + 4f, width - 16f, 18f),
                "로그 " + GameLog.Count + "건 | " + filterName + "    (F1 숨김 | F2 위치 | F3 줄 수 | F4 분류)", headerStyle);
            // 필터에 맞는 최근 로그를 아래에서부터 채웁니다.
            int row = lines - 1;
            for (int i = GameLog.Entries.Count - 1; i >= 0 && row >= 0; i--)
            {
                GameLog.Entry e = GameLog.Entries[i];
                if (filter != 0 && GameLog.Category(e.tc) != category) continue;
                string text = "<color=#8f9bb0>" + GameLog.FormatTime(e.time) + "</color>  <color=#ffd34d>[" + e.tc + "]</color>  " + e.message;
                GUI.Label(new Rect(x + 8f, y + 24f + row * lineHeight, width - 16f, lineHeight), text, lineStyle);
                row--;
            }
            GUI.matrix = Matrix4x4.identity;
        }

        private void OnDestroy()
        {
            if (backdrop != null) Destroy(backdrop);
        }
    }
}
