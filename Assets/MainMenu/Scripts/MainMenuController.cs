using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        private bool loading;

        private void Awake()
        {
            Time.timeScale = 1f;
            guidePanel.SetActive(false);
            statusLabel.text = "";
            startButton.onClick.AddListener(StartGame);
            guideButton.onClick.AddListener(OpenGuide);
            exitButton.onClick.AddListener(QuitGame);
            closeGuideButton.onClick.AddListener(CloseGuide);
        }

        private void Update()
        {
            if (loading) return;
            if (Input.GetKeyDown(KeyCode.Escape) && guidePanel.activeSelf) CloseGuide();
            // 마우스 없이도 방향키를 누르면 첫 버튼부터 선택할 수 있습니다.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null &&
                (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow)))
                EventSystem.current.SetSelectedGameObject(guidePanel.activeSelf ? closeGuideButton.gameObject : startButton.gameObject);
        }

        public void StartGame()
        {
            if (loading || guidePanel.activeSelf) return;
            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                statusLabel.text = "게임 씬이 없습니다. File > Build Settings를 확인해 주세요.";
                Debug.LogError("Build Settings에 게임 씬을 등록하세요: " + gameSceneName);
                return;
            }
            StartCoroutine(LoadGame());
        }

        private IEnumerator LoadGame()
        {
            loading = true;
            SetButtons(false);
            startLabel.text = "불러오는 중…";
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
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeGuideButton.gameObject);
        }

        public void CloseGuide()
        {
            guidePanel.SetActive(false);
            SetButtons(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(guideButton.gameObject);
        }

        private void SetButtons(bool enabled)
        {
            startButton.interactable = enabled;
            guideButton.interactable = enabled;
            exitButton.interactable = enabled;
        }

        public void QuitGame()
        {
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
