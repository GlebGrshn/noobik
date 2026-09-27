using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nubik
{
    public sealed class MineHud : MonoBehaviour
    {
        public TouchStick Stick { get; private set; }
        public HoldToDig Dig { get; private set; }
        public bool ModalOpen => confirmation != null && confirmation.activeSelf;
        private MineGame game;
        private Font font;
        private RectTransform root;
        private Text stats, hint, toast, baseInfo, upgradeText;
        private GameObject basePanel, controls, confirmation;
        private Button upgrade, sell;
        private float toastUntil;
        private static readonly Color Ink = new Color(0.055f, 0.11f, 0.15f, 0.96f);
        private static readonly Color Amber = new Color(1f, 0.73f, 0.25f);
        private static readonly Color Mint = new Color(0.22f, 0.83f, 0.73f);

        public void Setup(MineGame owner)
        {
            game = owner;
            font = Resources.Load<Font>("Fonts/NotoSans");
            var canvas = new GameObject("Game UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = Panel("Safe area", canvas.transform, Color.clear, Vector2.zero, Vector2.one);
            root.GetComponent<Image>().raycastTarget = false;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var header = Panel("Header", root, Ink, new Vector2(0, 1), Vector2.one, new Vector2(14, -126), new Vector2(-14, -14));
            Label(header, "НУБИК ШАХТЁР", 26, Amber, new Vector2(0, 0.55f), Vector2.one);
            stats = Label(header, "", 21, Color.white, Vector2.zero, new Vector2(1, 0.55f));
            hint = Label(root, "", 19, Color.white, new Vector2(0, 1), Vector2.one, new Vector2(18, -188), new Vector2(-18, -132));
            hint.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -2);
            controls = Panel("Controls", root, Color.clear, Vector2.zero, Vector2.one).gameObject;
            controls.GetComponent<Image>().raycastTarget = false;
            var pad = Panel("Movement stick", controls.transform, Ink, Vector2.zero, Vector2.zero, new Vector2(24, 34), new Vector2(178, 188));
            Stick = pad.gameObject.AddComponent<TouchStick>();
            Stick.knob = Panel("Stick knob", pad, Mint, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-27, -27), new Vector2(27, 27));
            Stick.knob.GetComponent<Image>().raycastTarget = false;
            Label(pad, "ДВИЖЕНИЕ", 15, Color.white, Vector2.zero, new Vector2(1, 0.20f));
            var dig = Button(controls.transform, "КОПАТЬ", Amber, game.DigTap, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-188, 34), new Vector2(-24, 144));
            Dig = dig.gameObject.AddComponent<HoldToDig>();
            Button(controls.transform, "Наверх", Mint, game.RequestReturn, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-188, 160), new Vector2(-24, 220));
            Label(controls.transform, "WASD / стрелки · ЛКМ / пробел · R — наверх", 16, Color.white, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 232), new Vector2(-12, 272));

            basePanel = Panel("Base menu", root, Ink, new Vector2(0.06f, 0.27f), new Vector2(0.94f, 0.77f)).gameObject;
            Label(basePanel.transform, "ПРИВАЛ НА ПОВЕРХНОСТИ", 24, Amber, new Vector2(0.03f, 0.82f), new Vector2(0.97f, 0.98f));
            baseInfo = Label(basePanel.transform, "", 20, Color.white, new Vector2(0.06f, 0.61f), new Vector2(0.94f, 0.82f));
            sell = Button(basePanel.transform, "Продать находки", Mint, game.Sell, new Vector2(0.07f, 0.45f), new Vector2(0.93f, 0.59f));
            upgrade = Button(basePanel.transform, "Улучшить кирку", Amber, game.Upgrade, new Vector2(0.07f, 0.27f), new Vector2(0.93f, 0.41f));
            upgradeText = upgrade.GetComponentInChildren<Text>();
            Button(basePanel.transform, "Спуститься", Mint, game.Descend, new Vector2(0.07f, 0.09f), new Vector2(0.93f, 0.23f));
            basePanel.SetActive(false);
            confirmation = Panel("Confirm return", root, Ink, new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.66f)).gameObject;
            Label(confirmation.transform, "Закончить копание и вернуться?", 23, Color.white, new Vector2(0.04f, 0.57f), new Vector2(0.96f, 0.94f));
            Button(confirmation.transform, "Наверх", Amber, () => { confirmation.SetActive(false); game.ReturnToBase(); }, new Vector2(0.06f, 0.13f), new Vector2(0.47f, 0.46f));
            Button(confirmation.transform, "Остаться", Mint, () => { confirmation.SetActive(false); controls.SetActive(true); game.SetMenuOpen(false); }, new Vector2(0.53f, 0.13f), new Vector2(0.94f, 0.46f));
            confirmation.SetActive(false);
            toast = Label(root, "", 23, Amber, new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.37f));
            toast.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -2);
        }

        private void Update()
        {
            if (game == null || game.Progress == null) return;
            Rect safe = Screen.safeArea;
            root.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            var data = game.Progress;
            stats.text = data.coins + " монет   •   Рюкзак: " + data.backpack + "\n" + game.Depth + " / 30 м   •   Рекорд: " + data.maxDepth + " м";
            hint.text = game.AtBase ? ProgressStore.Status : game.Hint;
            toast.enabled = Time.unscaledTime < toastUntil;
            if (!game.AtBase) return;
            int next = data.pickTier + 1;
            baseInfo.text = "Кирка: " + game.config.pickNames[data.pickTier] + "\n" + (data.foundHelmet ? "Коллекция: старая каска" : "В шахте спрятана старая каска");
            sell.interactable = data.backpack > 0;
            upgrade.interactable = next < game.config.pickPrices.Length && data.coins >= game.config.pickPrices[next];
            upgradeText.text = next < game.config.pickPrices.Length ? game.config.pickNames[next] + " кирка · " + game.config.pickPrices[next] : "Кирка улучшена до максимума";
        }

        public void ShowBase(bool show) { basePanel.SetActive(show); controls.SetActive(!show); }
        public void ShowReturnConfirmation() { ClearInput(); controls.SetActive(false); confirmation.SetActive(true); game.SetMenuOpen(true); }
        public void ClearInput() { Stick.ResetInput(); Dig.ResetInput(); }
        public void Notify(string message) { toast.text = message; toastUntil = Time.unscaledTime + 3; }

        private RectTransform Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max, Vector2 low = default, Vector2 high = default)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high;
            obj.GetComponent<Image>().color = color;
            return rect;
        }
        private Text Label(Transform parent, string text, int size, Color color, Vector2 min, Vector2 max, Vector2 low = default, Vector2 high = default)
        {
            var obj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high;
            var label = obj.GetComponent<Text>();
            label.font = font; label.fontSize = size; label.text = text; label.color = color;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = size;
            return label;
        }
        private Button Button(Transform parent, string caption, Color color, UnityEngine.Events.UnityAction action, Vector2 min, Vector2 max, Vector2 low = default, Vector2 high = default)
        {
            var rect = Panel(caption, parent, color, min, max, low, high);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors; colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f); colors.pressedColor = new Color(0.7f, 0.7f, 0.7f); colors.disabledColor = new Color(0.5f, 0.5f, 0.5f); button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            Label(rect, caption, 22, Ink, Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
            return button;
        }
    }
}
