using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nubik
{
    public sealed class MineHud : MonoBehaviour
    {
        public TouchStick Stick { get; private set; }
        public TouchLook Look { get; private set; }
        public HoldButton Dig { get; private set; }
        public bool PanelOpen => shop.activeSelf || confirmation.activeSelf || ending.activeSelf;
        private MineGame game;
        private Font font;
        private RectTransform root, canvasRect;
        private Text soundText, coins, depth, zone, backpack, target, hint, toast, shopInfo, upgradeText, sellText, albumTitle, endingText;
        private GameObject touchControls, overlay, shop, confirmation, ending, shopButton, descendButton;
        private Button upgrade, sell;
        private Image crosshair;
        private readonly List<Image> albumTiles = new List<Image>();
        private readonly List<Text> albumNames = new List<Text>();
        private readonly List<FloatingText> popups = new List<FloatingText>();
        private float toastUntil;
        private bool jumpQueued;
        private static readonly Color Ink = new Color(0.055f, 0.11f, 0.15f, 0.92f);
        private static readonly Color Shade = new Color(0.02f, 0.05f, 0.07f, 0.55f);
        private static readonly Color Amber = new Color(1f, 0.76f, 0.25f);
        private static readonly Color Mint = new Color(0.22f, 0.83f, 0.73f);
        private static readonly Color Muted = new Color(0.78f, 0.84f, 0.88f);

        private sealed class FloatingText { public Text label; public Vector3 world; public float age; }

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
            canvasRect = (RectTransform)canvas.transform;
            root = Panel("Safe area", canvas.transform, Color.clear, Vector2.zero, Vector2.one);
            root.GetComponent<Image>().raycastTarget = false;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Touch look sits under every other control so buttons win the tap.
            touchControls = Panel("Touch controls", root, Color.clear, Vector2.zero, Vector2.one).gameObject;
            touchControls.GetComponent<Image>().raycastTarget = false;
            var lookArea = Panel("Look area", touchControls.transform, Color.clear, new Vector2(0.35f, 0), Vector2.one);
            Look = lookArea.gameObject.AddComponent<TouchLook>();
            var pad = Panel("Movement stick", touchControls.transform, Shade, Vector2.zero, Vector2.zero, new Vector2(24, 34), new Vector2(194, 204));
            Stick = pad.gameObject.AddComponent<TouchStick>();
            Stick.knob = Panel("Stick knob", pad, Mint, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-30, -30), new Vector2(30, 30));
            Stick.knob.GetComponent<Image>().raycastTarget = false;
            var dig = Button(touchControls.transform, "КОПАТЬ", Amber, null, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-196, 34), new Vector2(-24, 190));
            Dig = dig.gameObject.AddComponent<HoldButton>();
            Button(touchControls.transform, "ПРЫЖОК", Mint, () => jumpQueued = true, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-324, 34), new Vector2(-208, 130));

            var stats = Panel("Stats", root, Shade, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -172), new Vector2(352, -12));
            coins = Label(stats, "", 30, Amber, Vector2.zero, Vector2.one, new Vector2(14, 118), new Vector2(-10, -4), TextAnchor.MiddleLeft);
            depth = Label(stats, "", 30, Color.white, Vector2.zero, Vector2.one, new Vector2(14, 78), new Vector2(-10, -44), TextAnchor.MiddleLeft);
            zone = Label(stats, "", 19, Muted, Vector2.zero, Vector2.one, new Vector2(14, 42), new Vector2(-10, -86), TextAnchor.MiddleLeft);
            backpack = Label(stats, "", 19, Color.white, Vector2.zero, Vector2.one, new Vector2(14, 6), new Vector2(-10, -122), TextAnchor.MiddleLeft);
            Button(root, "Наверх", Mint, game.RequestReturn, Vector2.one, Vector2.one, new Vector2(-170, -72), new Vector2(-12, -12));
            shopButton = Button(root, "Лавка", Amber, game.OpenShop, Vector2.one, Vector2.one, new Vector2(-170, -138), new Vector2(-12, -80)).gameObject;

            crosshair = Panel("Crosshair", root, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-4, -4), new Vector2(4, 4)).GetComponent<Image>();
            crosshair.raycastTarget = false;
            crosshair.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            target = Label(root, "", 19, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260, -62), new Vector2(260, -22));
            target.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -2);
            hint = Label(root, "", 19, Color.white, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 214), new Vector2(-16, 256));
            hint.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -2);
            toast = Label(root, "", 25, Amber, new Vector2(0.04f, 0.5f), new Vector2(0.96f, 0.5f), new Vector2(0, 90), new Vector2(0, 150));
            toast.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -2);

            overlay = Panel("Click to play", root, new Color(0.02f, 0.05f, 0.07f, 0.62f), Vector2.zero, Vector2.one).gameObject;
            var overlayButton = overlay.AddComponent<Button>();
            overlayButton.onClick.AddListener(game.Engage);
            Label(overlay.transform, "НУБИК ШАХТЁР", 40, Amber, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.5f), new Vector2(0, 90), new Vector2(0, 150));
            Label(overlay.transform, "Нажми, чтобы копать", 30, Color.white, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.5f), new Vector2(0, 20), new Vector2(0, 80));
            Label(overlay.transform, "Мышь — осмотр · ЛКМ — копать · WASD — ходить\nПробел — прыжок · R — наверх · E — лавка · M — звук · Esc — пауза", 19, Muted, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.5f), new Vector2(0, -80), new Vector2(0, 10));

            BuildShop();
            confirmation = Panel("Confirm return", root, Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-240, -110), new Vector2(240, 110)).gameObject;
            Label(confirmation.transform, "Закончить вылазку и подняться?", 24, Color.white, new Vector2(0.04f, 0.55f), new Vector2(0.96f, 0.94f));
            Button(confirmation.transform, "Наверх", Amber, () => { confirmation.SetActive(false); game.ReturnToSurface(); }, new Vector2(0.05f, 0.12f), new Vector2(0.47f, 0.45f));
            Button(confirmation.transform, "Остаться", Mint, () => { confirmation.SetActive(false); game.Engage(); }, new Vector2(0.53f, 0.12f), new Vector2(0.95f, 0.45f));
            confirmation.SetActive(false);

            ending = Panel("Ending", root, Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250, -220), new Vector2(250, 220)).gameObject;
            Label(ending.transform, "ЗАПЕЧАТАННАЯ ДВЕРЬ", 28, Amber, new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.97f));
            endingText = Label(ending.transform, "", 19, Color.white, new Vector2(0.06f, 0.3f), new Vector2(0.94f, 0.82f));
            Button(ending.transform, "Осмотреться", Mint, () => { ending.SetActive(false); game.Engage(); }, new Vector2(0.05f, 0.06f), new Vector2(0.47f, 0.24f));
            Button(ending.transform, "Наверх", Amber, () => { ending.SetActive(false); game.ReturnToSurface(); }, new Vector2(0.53f, 0.06f), new Vector2(0.95f, 0.24f));
            ending.SetActive(false);
        }

        private void BuildShop()
        {
            shop = Panel("Shop", root, Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250, -330), new Vector2(250, 330)).gameObject;
            Label(shop.transform, "ЛАВКА", 30, Amber, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -58), new Vector2(-16, -10));
            shopInfo = Label(shop.transform, "", 19, Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -126), new Vector2(-20, -60));
            albumTitle = Label(shop.transform, "", 18, Muted, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -158), new Vector2(-20, -128));
            int count = game.config.collection.Length;
            float tile = 84, gap = 8, start = -(count * tile + (count - 1) * gap) / 2;
            for (int i = 0; i < count; i++)
            {
                float x = start + i * (tile + gap);
                var slot = Panel("Album slot", shop.transform, Color.gray, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -250), new Vector2(x + tile, -166));
                albumTiles.Add(slot.GetComponent<Image>());
                albumNames.Add(Label(shop.transform, "", 13, Muted, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x - 4, -292), new Vector2(x + tile + 4, -252)));
            }
            sell = Button(shop.transform, "", Mint, game.Sell, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -380), new Vector2(-24, -306));
            sellText = sell.GetComponentInChildren<Text>();
            upgrade = Button(shop.transform, "", Amber, game.Upgrade, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -466), new Vector2(-24, -392));
            upgradeText = upgrade.GetComponentInChildren<Text>();
            descendButton = Button(shop.transform, "Спуститься к месту копания", Mint, game.Descend, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -552), new Vector2(-24, -478)).gameObject;
            Button(shop.transform, "Закрыть", new Color(0.72f, 0.78f, 0.82f), game.CloseShop, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -638), new Vector2(-196, -564));
            soundText = Button(shop.transform, "", new Color(0.72f, 0.78f, 0.82f), game.ToggleSound, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-184, -638), new Vector2(-24, -564)).GetComponentInChildren<Text>();
            shop.SetActive(false);
        }

        private void Update()
        {
            if (game == null || game.Progress == null) return;
            Rect safe = Screen.safeArea;
            root.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            var data = game.Progress;
            var config = game.config;
            int metres = game.Depth;
            int zoneIndex = config.ZoneIndex(metres);
            coins.text = data.coins + " " + MineGame.Plural(data.coins, "монета", "монеты", "монет");
            depth.text = metres + " м  <size=19><color=#c7d6e0>рекорд " + data.maxDepth + " м</color></size>";
            zone.text = zoneIndex + 1 < config.zones.Length
                ? config.zones[zoneIndex].nameRu + " · до зоны «" + config.zones[zoneIndex + 1].nameRu + "» " + (config.zones[zoneIndex + 1].startDepth - metres) + " м"
                : config.zones[zoneIndex].nameRu + " · дверь на " + config.depth + " м";
            backpack.text = "Рюкзак: " + data.backpack + " монет";

            bool panel = PanelOpen;
            bool playing = game.Active;
            touchControls.SetActive(game.TouchMode && !panel);
            overlay.SetActive(game.NeedsClick);
            shopButton.SetActive(game.NearShop && !panel);
            crosshair.enabled = playing;
            crosshair.color = game.InReach ? Amber : Color.white;
            target.text = playing ? game.TargetText : "";
            hint.text = panel ? "" : game.Hint;
            hint.rectTransform.anchoredPosition = new Vector2(0, game.TouchMode ? 235 : 64);
            toast.enabled = Time.unscaledTime < toastUntil;
            UpdatePopups();
            if (shop.activeSelf) UpdateShop();
        }

        private void UpdateShop()
        {
            var data = game.Progress;
            var config = game.config;
            int next = data.tool + 1;
            var tool = game.Tool;
            shopInfo.text = tool.nameRu + " · урон " + tool.damage + "\n" + ProgressStore.Status;
            albumTitle.text = "Коллекция " + data.CollectionCount + " / " + config.collection.Length;
            for (int i = 0; i < albumTiles.Count; i++)
            {
                bool found = data.HasCollectible(i);
                albumTiles[i].color = found ? config.collection[i].color : new Color(0.2f, 0.25f, 0.3f);
                albumNames[i].text = found ? config.collection[i].nameRu : "???";
            }
            sell.interactable = data.backpack > 0;
            sellText.text = data.backpack > 0 ? "Продать находки · +" + data.backpack : "Рюкзак пуст";
            if (next < config.tools.Length)
            {
                var nextTool = config.tools[next];
                upgrade.interactable = data.coins >= nextTool.price;
                upgradeText.text = nextTool.nameRu + " · " + nextTool.price + " (урон " + nextTool.damage + ")";
            }
            else
            {
                upgrade.interactable = false;
                upgradeText.text = "Лучшая лопата уже у тебя";
            }
            descendButton.SetActive(data.hasDive);
            soundText.text = data.muted ? "Звук: выкл" : "Звук: вкл";
        }

        public void ShowShop(bool show) => shop.SetActive(show);
        public void ShowReturnConfirmation() { ClearInput(); confirmation.SetActive(true); }

        public void ShowEnding()
        {
            var data = game.Progress;
            bool key = data.HasCollectible(game.config.collection.Length - 1);
            endingText.text = "На глубине " + game.config.depth + " м лопата звякнула о древнюю плиту. Руны на двери светятся, но она заперта изнутри.\n\n" +
                (key ? "Загадочный ключ дрожит в рюкзаке — замок пока молчит. " : "Может, где-то в шахте лежит ключ? ") +
                "Что за дверью — узнаем в обновлении.\n\nКоллекция: " + data.CollectionCount + " / " + game.config.collection.Length;
            ClearInput();
            ending.SetActive(true);
        }

        public void ClearInput() { Stick.ResetInput(); Dig.ResetInput(); Look.ResetInput(); jumpQueued = false; }
        public bool ConsumeJump() { bool value = jumpQueued; jumpQueued = false; return value; }
        public void Notify(string message) { toast.text = message; toastUntil = Time.unscaledTime + 3; }

        public void Popup(Vector3 world, string text, Color color)
        {
            FloatingText popup = null;
            foreach (var candidate in popups) if (candidate.age >= 1) { popup = candidate; break; }
            if (popup == null)
            {
                if (popups.Count >= 12) return;
                popup = new FloatingText { label = Label(root, "", 24, color, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-160, -20), new Vector2(160, 20)) };
                popup.label.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -2);
                popups.Add(popup);
            }
            popup.label.text = text;
            popup.label.color = color;
            popup.world = world;
            popup.age = 0;
            popup.label.enabled = true;
        }

        private void UpdatePopups()
        {
            var camera = Camera.main;
            foreach (var popup in popups)
            {
                if (popup.age >= 1) { popup.label.enabled = false; continue; }
                popup.age += Time.unscaledDeltaTime / 0.9f;
                var screen = camera != null ? camera.WorldToScreenPoint(popup.world) : Vector3.back;
                if (screen.z <= 0) { popup.label.enabled = false; continue; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
                var rect = popup.label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = local + Vector2.up * (30 + popup.age * 70);
                var color = popup.label.color; color.a = Mathf.Clamp01(2 - popup.age * 2); popup.label.color = color;
                popup.label.enabled = true;
            }
        }

        private RectTransform Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max, Vector2 low = default, Vector2 high = default)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high;
            obj.GetComponent<Image>().color = color;
            return rect;
        }

        private Text Label(Transform parent, string text, int size, Color color, Vector2 min, Vector2 max, Vector2 low = default, Vector2 high = default, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var obj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high;
            var label = obj.GetComponent<Text>();
            label.font = font; label.fontSize = size; label.text = text; label.color = color;
            label.alignment = anchor; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = size;
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
