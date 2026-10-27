using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DumbFrog.Arcade;
using DumbFrog.Logging;

namespace DumbFrog.Menu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("시작할 게임 씬 (Build Settings에도 등록해야 합니다)")]
        public string gameSceneName = "ArcadeGame";
        public Button startButton;
        public Button guideButton;
        public Button exitButton;
        public Button closeGuideButton;
        public GameObject guidePanel;
        public Text startLabel;
        public Text statusLabel;
        [Header("2인 대전 버튼 (게임 시작 버튼을 복제해 옆에 만듭니다)")]
        public string cpuModeText = "CPU 대전";
        public string pvpModeText = "2인 대전";
        [Tooltip("두 버튼 중심 사이 거리의 절반입니다.")]
        public float modeButtonOffset = 136f;
        [Tooltip("배경 캐릭터를 가리지 않도록 두 버튼 폭을 이만큼 줄입니다.")]
        public float modeButtonShrink = 40f;
        [Header("제목 타이틀")]
        public string titleText = "DUMB_FROG";
        [Range(48, 160)] public int titleFontSize = 104;
        [Tooltip("화면 중앙 기준 Y 위치(1280x720 기준)입니다.")]
        public float titleY = 290f;
        [Tooltip("위아래로 둥실거리는 높이(픽셀)입니다. 0이면 고정됩니다.")]
        [Range(0f, 12f)] public float titleBob = 4f;
        private RectTransform title;
        private Button pvpButton;
        private Text pvpLabel;
        private bool loading;
        private int guideOpenCount;
        private static bool launched;

        private void Awake()
        {
            Time.timeScale = 1f;
            guidePanel.SetActive(false);
            statusLabel.text = "";
            CreatePvpButton();
            CreateTitle();
            startButton.onClick.AddListener(StartGame);
            pvpButton.onClick.AddListener(StartPvpGame);
            guideButton.onClick.AddListener(OpenGuide);
            exitButton.onClick.AddListener(QuitGame);
            closeGuideButton.onClick.AddListener(CloseGuide);
            GameLog.Font = statusLabel.font;
            if (!launched) GameLog.Write("TC-001", "게임 실행 → 메인 화면 표시 (" + Application.platform + ", " + Screen.width + "x" + Screen.height + ")");
            else GameLog.Write("INFO", "메인 화면 복귀");
            launched = true;
            GameLog.Write("TC-002", "메뉴 버튼 표시: " + cpuModeText + ", " + pvpModeText + ", 설명서, 종료");
        }

        private void Update()
        {
            if (title != null)
            {
                float t = Time.unscaledTime;
                title.anchoredPosition = new Vector2(0f, titleY + Mathf.Sin(t * 1.6f) * titleBob);
                title.localScale = Vector3.one * (1f + Mathf.Sin(t * 3.2f) * 0.008f);
            }
            if (loading) return;
            if (Input.GetKeyDown(KeyCode.Escape) && guidePanel.activeSelf) CloseGuide("TC-009");
            // 마우스 없이도 방향키를 누르면 첫 버튼부터 선택할 수 있습니다.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null &&
                (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow)))
            {
                EventSystem.current.SetSelectedGameObject(guidePanel.activeSelf ? closeGuideButton.gameObject : startButton.gameObject);
                if (guidePanel.activeSelf) GameLog.Write("TC-015", "방향키 입력 → 설명서 닫기 버튼 선택");
                else GameLog.Write("TC-014", "방향키 입력 → " + cpuModeText + " 버튼 선택");
            }
        }

        // 씬 파일을 고치지 않고 기존 버튼 디자인 그대로 '2인 대전' 버튼을 추가합니다.
        private void CreatePvpButton()
        {
            RectTransform start = startButton.GetComponent<RectTransform>();
            GameObject copy = Instantiate(startButton.gameObject, start.parent, false);
            copy.name = "PvpButton";
            RectTransform rect = copy.GetComponent<RectTransform>();
            rect.SetSiblingIndex(start.GetSiblingIndex() + 1);
            Vector2 position = start.anchoredPosition;
            start.anchoredPosition = new Vector2(position.x - modeButtonOffset, position.y);
            rect.anchoredPosition = new Vector2(position.x + modeButtonOffset, position.y);
            Narrow(start, modeButtonShrink); Narrow(rect, modeButtonShrink);
            pvpButton = copy.GetComponent<Button>();
            pvpButton.onClick.RemoveAllListeners();
            pvpLabel = copy.GetComponentInChildren<Text>(true);
            if (startLabel != null) startLabel.text = cpuModeText;
            if (pvpLabel != null) pvpLabel.text = pvpModeText;
        }

        // 시작화면 위쪽에 'DUMB_FROG' 제목을 만듭니다. 왼쪽은 플레이어(주황), 오른쪽은 도플갱어(보라) 색입니다.
        private void CreateTitle()
        {
            GameObject obj = new GameObject("TitleText", typeof(RectTransform));
            title = obj.GetComponent<RectTransform>();
            title.SetParent(startButton.transform.parent, false);
            title.SetAsLastSibling();
            title.anchorMin = title.anchorMax = new Vector2(0.5f, 0.5f);
            title.sizeDelta = new Vector2(1000f, 150f);
            title.anchoredPosition = new Vector2(0f, titleY);
            Text label = obj.AddComponent<Text>();
            label.font = statusLabel.font;
            label.fontSize = titleFontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = true;
            label.text = ColoredTitle(titleText);
            // 두꺼운 외곽선 + 아래 그림자로 배경 위에서도 또렷하게 보이게 합니다.
            Color ink = new Color(0.09f, 0.04f, 0.12f, 1f);
            Outline outer = obj.AddComponent<Outline>(); outer.effectColor = ink; outer.effectDistance = new Vector2(4f, -4f);
            Outline inner = obj.AddComponent<Outline>(); inner.effectColor = ink; inner.effectDistance = new Vector2(2f, 2f);
            Shadow drop = obj.AddComponent<Shadow>(); drop.effectColor = new Color(0f, 0f, 0f, 0.55f); drop.effectDistance = new Vector2(0f, -9f);
        }

        // '_' 앞은 주황, 뒤는 보라, '_'는 흰색으로 칠합니다.
        private static string ColoredTitle(string text)
        {
            int split = text.IndexOf('_');
            if (split < 0) return "<color=#FFB029>" + text + "</color>";
            return "<color=#FFB029>" + text.Substring(0, split) + "</color><color=#FFFFFF>_</color><color=#C985FF>" +
                text.Substring(split + 1) + "</color>";
        }

        // 버튼 장식(베벨/면/라벨)은 폭을 줄이고, 모서리 리벳은 안쪽으로 옮깁니다.
        private static void Narrow(RectTransform button, float amount)
        {
            button.sizeDelta -= new Vector2(amount, 0f);
            foreach (RectTransform child in button)
            {
                Vector2 p = child.anchoredPosition;
                if (Mathf.Abs(p.x) > 1f) child.anchoredPosition = new Vector2(p.x - Mathf.Sign(p.x) * amount * 0.5f, p.y);
                else child.sizeDelta -= new Vector2(amount, 0f);
            }
        }

        public void StartGame() { StartMatch(false); }

        public void StartPvpGame() { StartMatch(true); }

        private void StartMatch(bool twoPlayer)
        {
            string mode = twoPlayer ? pvpModeText : cpuModeText;
            if (loading) { GameLog.Write("TC-004", mode + " 중복 클릭 → 씬 전환 중복 실행 안 함"); return; }
            if (guidePanel.activeSelf) { GameLog.Write("TC-010", "설명서가 열린 동안 " + mode + " 시도 → 시작 안 함"); return; }
            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                GameLog.Write("TC-006", "게임 씬(" + gameSceneName + ") 미등록 → 오류 안내 표시");
                statusLabel.text = "게임 씬이 없습니다. File > Build Settings를 확인해 주세요.";
                Debug.LogError("Build Settings에 게임 씬을 등록하세요: " + gameSceneName);
                return;
            }
            MatchSettings.TwoPlayer = twoPlayer;
            GameLog.Write("TC-003", mode + " 클릭 → " + gameSceneName + " 씬으로 전환");
            StartCoroutine(LoadGame(twoPlayer ? pvpLabel : startLabel));
        }

        private IEnumerator LoadGame(Text label)
        {
            loading = true;
            SetButtons(false);
            if (label != null) label.text = "불러오는 중…";
            GameLog.Write("TC-005", "'불러오는 중...' 문구 표시");
            // 버튼의 로딩 표시를 먼저 그린 뒤 씬을 전환합니다.
            yield return null;
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);
        }

        public void OpenGuide()
        {
            if (loading) return;
            SetButtons(false);
            guidePanel.SetActive(true);
            guideOpenCount++;
            GameLog.Write(guideOpenCount > 1 ? "TC-011" : "TC-007", "설명서 패널 표시" + (guideOpenCount > 1 ? " (" + guideOpenCount + "번째)" : ""));
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeGuideButton.gameObject);
        }

        public void CloseGuide() { CloseGuide("TC-008"); }

        private void CloseGuide(string tc)
        {
            if (!guidePanel.activeSelf) return;
            GameLog.Write(tc, (tc == "TC-009" ? "ESC" : "닫기 버튼") + " → 설명서 패널 닫힘");
            guidePanel.SetActive(false);
            SetButtons(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(guideButton.gameObject);
        }

        private void SetButtons(bool enabled)
        {
            startButton.interactable = enabled;
            if (pvpButton != null) pvpButton.interactable = enabled;
            guideButton.interactable = enabled;
            exitButton.interactable = enabled;
        }

        public void QuitGame()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GameLog.Write("TC-013", "웹 종료 버튼 → 탭을 닫으라는 안내 표시");
#else
            GameLog.Write("TC-012", "종료 버튼 → 프로그램 종료");
#endif
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            statusLabel.text = "웹에서는 이 탭을 닫으면 게임이 종료됩니다.";
#else
            Application.Quit();
#endif
        }
    }
}
