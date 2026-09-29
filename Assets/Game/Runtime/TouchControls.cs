using UnityEngine;
using UnityEngine.EventSystems;

namespace Nubik
{
    public sealed class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        public Vector2 Value { get; private set; }
        /// <summary>Double tap and hold: running until the finger is lifted.</summary>
        public bool Sprint { get; private set; }
        public RectTransform knob;
        private float lastUp = -10;
        public RectTransform baseRect;
        private Vector2 origin, home;
        private int pointer = int.MinValue;
        public void SetHome() { home = baseRect.localPosition; ResetInput(); }
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId;
            Sprint = Time.unscaledTime - lastUp < .3f;
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out origin);
            baseRect.localPosition = ClampBase(origin);
            Value = Vector2.zero;
        }
        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var point);
            float radius = baseRect.rect.width * .32f;
            var travel = point - origin;
            // Follow an overreaching thumb so changing direction does not require a long return swipe.
            if (travel.magnitude > radius) origin = point - travel.normalized * radius;
            baseRect.localPosition = ClampBase(origin);
            var raw = Vector2.ClampMagnitude((point - origin) / radius, 1);
            float amount = Mathf.InverseLerp(.10f, 1, raw.magnitude);
            Value = raw.normalized * amount;
            knob.anchoredPosition = raw * baseRect.rect.width * .25f;
        }
        private Vector2 ClampBase(Vector2 point)
        {
            var bounds = ((RectTransform)transform).rect;
            float inset = baseRect.rect.width * .5f;
            return new Vector2(Mathf.Clamp(point.x, bounds.xMin + inset, bounds.xMax - inset),
                Mathf.Clamp(point.y, bounds.yMin + inset, bounds.yMax - inset));
        }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) { ResetInput(); lastUp = Time.unscaledTime; } }
        public void ResetInput() { pointer = int.MinValue; Value = Vector2.zero; Sprint = false; if (knob) knob.anchoredPosition = Vector2.zero; if (baseRect) baseRect.localPosition = home; }
        private void OnDisable() => ResetInput();
    }

    /// <summary>Drag anywhere on this area to turn the camera. Collects the delta until read.</summary>
    public sealed class TouchLook : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        private int pointer = int.MinValue;
        private Vector2 pending;
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void AddDelta(Vector2 delta) => pending += delta;
        public void OnPointerDown(PointerEventData e) { if (pointer == int.MinValue) pointer = e.pointerId; }
        public void OnDrag(PointerEventData e) { if (e.pointerId == pointer) pending += e.delta; }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) pointer = int.MinValue; }
        /// <summary>Screen pixels dragged since the last call.</summary>
        public Vector2 Consume() { var value = pending; pending = Vector2.zero; return value; }
        public void ResetInput() { pointer = int.MinValue; pending = Vector2.zero; }
        private void OnDisable() => ResetInput();
    }

    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IDragHandler, IInitializePotentialDragHandler
    {
        public bool Held { get; private set; }
        public TouchLook look;
        private int pointer = int.MinValue;
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId; Held = true;
        }
        public void OnDrag(PointerEventData e) { if (e.pointerId == pointer && look) look.AddDelta(e.delta); }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) ResetInput(); }
        // The press belongs to this finger until release, including outside the visible circle.
        public void OnPointerExit(PointerEventData e) { }
        public void ResetInput() { pointer = int.MinValue; Held = false; }
        private void OnDisable() => ResetInput();
    }
}
