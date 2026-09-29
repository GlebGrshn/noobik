using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nubik.Tests
{
    public class TouchControlsTests
    {
        private GameObject events, area;
        private EventSystem system;

        [SetUp] public void Setup()
        {
            events = new GameObject("Input test events", typeof(EventSystem));
            system = events.GetComponent<EventSystem>();
            area = new GameObject("Touch test area", typeof(RectTransform));
            ((RectTransform)area.transform).sizeDelta = new Vector2(600, 400);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(area); Object.DestroyImmediate(events); }
        private PointerEventData Finger(int id, Vector2 position, Vector2 delta = default) =>
            new PointerEventData(system) { pointerId = id, position = position, delta = delta };

        [Test] public void DigKeepsItsFingerOutsideButtonAndCanTurnWhileHeld()
        {
            var look = area.AddComponent<TouchLook>();
            var hold = area.AddComponent<HoldButton>(); hold.look = look;
            var finger = Finger(4, Vector2.zero, new Vector2(120, -35));
            hold.OnPointerDown(finger);
            hold.OnPointerExit(finger);
            hold.OnDrag(finger);
            Assert.IsTrue(hold.Held, "Drifting off the visible button must not interrupt digging.");
            Assert.AreEqual(finger.delta, look.Consume());
            hold.OnPointerDown(Finger(9, Vector2.zero));
            hold.OnPointerUp(Finger(9, Vector2.zero));
            hold.OnDrag(Finger(9, Vector2.zero, Vector2.one));
            Assert.IsTrue(hold.Held, "Another finger cannot release or take over the action.");
            Assert.AreEqual(Vector2.zero, look.Consume());
            hold.OnPointerUp(finger);
            hold.OnDrag(finger);
            Assert.IsFalse(hold.Held);
            Assert.AreEqual(Vector2.zero, look.Consume());
        }

        [Test] public void LeftDigLeavesRightFingerFreeAndIndependentButtonsReleaseSeparately()
        {
            var look = area.AddComponent<TouchLook>();
            var left = area.AddComponent<HoldButton>();
            var right = area.AddComponent<HoldButton>(); right.look = look;
            var a = Finger(1, Vector2.zero, new Vector2(30, 5));
            var b = Finger(2, Vector2.zero, new Vector2(40, 10));
            left.OnPointerDown(a); left.OnDrag(a);
            Assert.AreEqual(Vector2.zero, look.Consume());
            look.OnPointerDown(b); look.OnDrag(b);
            Assert.IsTrue(left.Held); Assert.AreEqual(b.delta, look.Consume());
            right.OnPointerDown(b); left.OnPointerUp(a);
            Assert.IsFalse(left.Held); Assert.IsTrue(right.Held);
            right.ResetInput();
            Assert.IsFalse(right.Held, "Menu/focus reset cancels captured input.");
            look.ResetInput(); look.OnDrag(b);
            Assert.AreEqual(Vector2.zero, look.Consume());
        }

        [Test] public void FloatingStickStartsNeutralFollowsThumbAndReversesWithoutLongReturn()
        {
            var stick = area.AddComponent<TouchStick>();
            stick.baseRect = new GameObject("Base", typeof(RectTransform)).GetComponent<RectTransform>();
            stick.baseRect.SetParent(area.transform, false); stick.baseRect.sizeDelta = new Vector2(160, 160);
            stick.knob = new GameObject("Knob", typeof(RectTransform)).GetComponent<RectTransform>();
            stick.knob.SetParent(stick.baseRect, false); stick.SetHome();
            // A press near the corner still starts neutral even though the visible base is kept on screen.
            stick.OnPointerDown(Finger(3, new Vector2(-290, -190)));
            Assert.AreEqual(Vector2.zero, stick.Value);
            stick.OnDrag(Finger(3, new Vector2(-289, -189)));
            Assert.AreEqual(Vector2.zero, stick.Value, "Tiny thumb jitter is inside the dead zone.");
            stick.OnDrag(Finger(3, new Vector2(200, -190)));
            Assert.Greater(stick.Value.x, .99f);
            stick.OnPointerUp(Finger(8, Vector2.zero));
            Assert.Greater(stick.Value.x, .99f);
            stick.OnDrag(Finger(3, new Vector2(135, -190)));
            Assert.Less(stick.Value.x, 0, "The stick origin follows a long swipe.");
            stick.OnPointerUp(Finger(3, Vector2.zero));
            Assert.AreEqual(Vector2.zero, stick.Value);
            Assert.AreEqual(Vector3.zero, stick.baseRect.localPosition);
            Assert.AreEqual(Vector2.zero, stick.knob.anchoredPosition);
        }
    }
}
