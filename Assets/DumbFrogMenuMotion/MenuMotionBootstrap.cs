using UnityEngine;
using UnityEngine.SceneManagement;

namespace DumbFrog.MenuMotion
{
    // MainMenu 씬에만 효과를 추가합니다. 기존 씬/게임 시작 코드는 수정하지 않습니다.
    public static class MenuMotionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // Enter Play Mode Options에서 Domain Reload를 꺼도 중복 등록되지 않습니다.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MainMenu") return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.transform.Find("Background") == null ||
                        canvas.transform.Find("MenuButtons") == null) continue;
                    if (canvas.GetComponent<MainMenuMotion>() == null)
                        canvas.gameObject.AddComponent<MainMenuMotion>();
                    return;
                }
            }
            Debug.LogWarning("메뉴 효과: MainMenu의 Canvas 아래 Background와 MenuButtons를 확인하세요.");
        }
    }
}
