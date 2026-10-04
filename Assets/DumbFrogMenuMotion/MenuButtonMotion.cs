using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DumbFrog.MenuMotion
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonMotion : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        private Button button;
        private Vector3 originalScale;
        private float currentScale = 1f;
        private float hoverScale = 1.045f;
        private float pressedScale = 0.97f;
        private float responseSpeed = 18f;
        private float submitRemaining;
        private bool hovered;
        private bool pressed;
        private bool keyboardSelected;

        private void Awake()
        {
            button = GetComponent<Button>();
            originalScale = transform.localScale;
        }

        public void Configure(float hover, float press, float speed)
        {
            hoverScale = hover;
            pressedScale = press;
            responseSpeed = speed;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            submitRemaining = Mathf.Max(0f, submitRemaining - dt);
            bool interactable = button != null && button.IsActive() && button.IsInteractable();
            if (!interactable) { hovered = false; pressed = false; keyboardSelected = false; }
            float target = !interactable ? 1f : (pressed || submitRemaining > 0f) ? pressedScale :
                (hovered || keyboardSelected) ? hoverScale : 1f;
            currentScale = Mathf.Lerp(currentScale, target, 1f - Mathf.Exp(-responseSpeed * dt));
            transform.localScale = new Vector3(originalScale.x * currentScale,
                originalScale.y * currentScale, originalScale.z);
        }

        public void OnPointerEnter(PointerEventData data) { hovered = true; }
        public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) { pressed = true; keyboardSelected = false; }
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) pressed = false;
        }
        public void OnSelect(BaseEventData data)
        {
            // 마우스 클릭으로 선택된 버튼이 계속 확대된 상태로 남지 않게 합니다.
            keyboardSelected = !(data is PointerEventData);
        }
        public void OnDeselect(BaseEventData data) { keyboardSelected = false; pressed = false; }
        public void OnSubmit(BaseEventData data) { submitRemaining = 0.12f; }

        private void OnDisable()
        {
            hovered = pressed = keyboardSelected = false;
            submitRemaining = 0f;
            currentScale = 1f;
            transform.localScale = originalScale;
        }
    }
}
