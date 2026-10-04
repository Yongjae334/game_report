using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DumbFrog.MenuMotion
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("Dumb Frog/Main Menu Motion")]
    public sealed class MainMenuMotion : MonoBehaviour
    {
        [Header("불씨 — 왼쪽 주황 / 오른쪽 보라")]
        [Range(0, 160)] public int emberCount = 72;
        [Range(0.1f, 2f)] public float riseSpeed = 0.8f;
        [Range(0f, 1f)] public float emberOpacity = 0.7f;
        [Header("버튼 — 올리면 확대 / 누르면 축소")]
        [Range(1f, 1.12f)] public float hoverScale = 1.045f;
        [Range(0.9f, 1f)] public float pressedScale = 0.97f;
        [Range(5f, 30f)] public float responseSpeed = 18f;

        private GameObject effectLayer;
        private MenuEmberGraphic embers;
        private readonly List<MenuButtonMotion> ownedButtons = new List<MenuButtonMotion>();

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            BuildIfNeeded();
            effectLayer.SetActive(true);
            foreach (MenuButtonMotion motion in ownedButtons)
                if (motion != null) motion.enabled = true;
            ApplySettings();
        }

        private void BuildIfNeeded()
        {
            if (effectLayer == null)
            {
                effectLayer = new GameObject("MenuEmbers_Runtime", typeof(RectTransform),
                    typeof(Canvas), typeof(CanvasRenderer), typeof(MenuEmberGraphic));
                RectTransform layer = effectLayer.GetComponent<RectTransform>();
                layer.SetParent(transform, false);
                RectTransform background = transform.Find("Background") as RectTransform;
                if (background != null)
                {
                    layer.anchorMin = background.anchorMin;
                    layer.anchorMax = background.anchorMax;
                    layer.pivot = background.pivot;
                    layer.sizeDelta = background.sizeDelta;
                    layer.anchoredPosition = background.anchoredPosition;
                    layer.localRotation = background.localRotation;
                    layer.localScale = background.localScale;
                    layer.SetSiblingIndex(background.GetSiblingIndex() + 1);
                }
                else
                {
                    layer.anchorMin = Vector2.zero;
                    layer.anchorMax = Vector2.one;
                    layer.offsetMin = layer.offsetMax = Vector2.zero;
                    layer.SetAsFirstSibling();
                }
                // 별도 Canvas로 입자 갱신을 분리하되 UI 순서는 부모를 따릅니다.
                Canvas effectCanvas = effectLayer.GetComponent<Canvas>();
                effectCanvas.overrideSorting = false;
                embers = effectLayer.GetComponent<MenuEmberGraphic>();
                embers.raycastTarget = false;
            }

            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<MenuButtonMotion>() != null) continue;
                MenuButtonMotion motion = button.gameObject.AddComponent<MenuButtonMotion>();
                ownedButtons.Add(motion);
            }
        }

        private void ApplySettings()
        {
            if (embers != null) embers.Configure(emberCount, riseSpeed, emberOpacity);
            foreach (MenuButtonMotion motion in ownedButtons)
                if (motion != null) motion.Configure(hoverScale, pressedScale, responseSpeed);
        }

        private void OnValidate()
        {
            if (Application.isPlaying) ApplySettings();
        }

        private void OnDisable()
        {
            if (effectLayer != null) effectLayer.SetActive(false);
            foreach (MenuButtonMotion motion in ownedButtons)
                if (motion != null) motion.enabled = false;
        }

        private void OnDestroy()
        {
            if (effectLayer != null) Destroy(effectLayer);
            foreach (MenuButtonMotion motion in ownedButtons)
                if (motion != null) Destroy(motion);
        }
    }
}
