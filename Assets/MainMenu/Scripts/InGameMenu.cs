using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DumbFrog.Menu
{
    public sealed class InGameMenu : MonoBehaviour
    {
        public Button returnButton;
        public string menuSceneName = "MainMenu";
        private bool returning;

        private void Awake()
        {
            returnButton.onClick.AddListener(ReturnToMenu);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) ReturnToMenu();
        }

        public void ReturnToMenu()
        {
            if (returning) return;
            if (!Application.CanStreamedLevelBeLoaded(menuSceneName))
            {
                Debug.LogError("Build Settings에 MainMenu 씬을 등록하세요.");
                return;
            }
            returning = true;
            returnButton.interactable = false;
            Time.timeScale = 1f;
            SceneManager.LoadSceneAsync(menuSceneName, LoadSceneMode.Single);
        }
    }
}
