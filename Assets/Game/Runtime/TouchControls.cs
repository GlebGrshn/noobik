using UnityEngine;
using UnityEngine.EventSystems;

namespace Nubik
{
    public sealed class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Vector2 Value { get; private set; }
        public RectTransform knob;
        private int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId;
            OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var point);
            Value = Vector2.ClampMagnitude(point / (rect.rect.width * 0.32f), 1);
            knob.anchoredPosition = Value * rect.rect.width * 0.25f;
        }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) ResetInput(); }
        public void ResetInput() { pointer = int.MinValue; Value = Vector2.zero; if (knob) knob.anchoredPosition = Vector2.zero; }
        private void OnDisable() => ResetInput();
    }

    public sealed class HoldToDig : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }
        public void OnPointerDown(PointerEventData e) => Held = true;
        public void OnPointerUp(PointerEventData e) => Held = false;
        public void OnPointerExit(PointerEventData e) => Held = false;
        public void ResetInput() => Held = false;
        private void OnDisable() => ResetInput();
    }
}
