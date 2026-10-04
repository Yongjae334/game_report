using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DumbFrog.MenuMotion.Editor
{
    public static class MenuMotionSetup
    {
        [MenuItem("Dumb Frog/시작화면 효과 설정")]
        private static void Setup()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("시작화면 효과", "Play를 멈춘 뒤 다시 선택해 주세요.", "확인");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "MainMenu")
            {
                EditorUtility.DisplayDialog("시작화면 효과", "Assets > Scenes > MainMenu 씬을 먼저 열어 주세요.", "확인");
                return;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.transform.Find("Background") == null ||
                        canvas.transform.Find("MenuButtons") == null) continue;
                    MainMenuMotion motion = canvas.GetComponent<MainMenuMotion>();
                    if (motion == null) motion = Undo.AddComponent<MainMenuMotion>(canvas.gameObject);
                    Selection.activeGameObject = canvas.gameObject;
                    EditorSceneManager.MarkSceneDirty(scene);
                    Debug.Log("Canvas의 Main Menu Motion에서 강도를 조절하고 Ctrl+S로 저장하세요. 효과는 Play에서 보입니다.");
                    return;
                }
            }
            EditorUtility.DisplayDialog("시작화면 효과", "Background와 MenuButtons가 있는 Canvas를 찾지 못했습니다.", "확인");
        }
    }
}
