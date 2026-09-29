using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nubik
{
    /// <summary>Responsive expedition HUD. Layout uses safe-area coordinates and a separate portrait composition.</summary>
    public sealed partial class MineHud : MonoBehaviour
    {
        public TouchStick Stick { get; private set; }
        public TouchLook Look { get; private set; }
        public HoldButton Dig { get; private set; }
        public HoldButton Jump { get; private set; }
        public bool PanelOpen => house.activeSelf || ending.activeSelf || SettingsOpen;

        private static readonly Color Ink = new Color(0.065f, 0.105f, 0.12f, 0.98f);
        private static readonly Color Card = new Color(0.08f, 0.135f, 0.15f, 0.92f);
        private static readonly Color Inset = new Color(0.12f, 0.19f, 0.20f, 1);
        private static readonly Color Cream = new Color(0.97f, 0.95f, 0.87f);
        private static readonly Color Muted = new Color(0.69f, 0.78f, 0.77f);
        private static readonly Color Amber = new Color(1, 0.76f, 0.31f);
        private static readonly Color Mint = new Color(0.43f, 0.86f, 0.72f);
        private static readonly Color Red = new Color(1f, 0.40f, 0.34f);
        private static readonly Color Blue = new Color(0.40f, 0.72f, 1f);
        private static readonly Color Scrim = new Color(0.025f, 0.055f, 0.065f, 0.72f);

        private MineGame game;
        private Font font;
        private Sprite rounded, circle;
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private CanvasScaler scaler;
        private RectTransform root, coinCard, depthCard, bagCard, vitalsCard, actions, toolCard, targetCard, toastCard, depthTrack;
        private RectTransform startCard, touchControls, pad, digRect, jumpRect, scanRect, medkitRect, dynamiteRect, fuelRow;
        private Button dynamiteButton;
        private Text dynamiteText;
        private RectTransform orderCard, confirmCard;
        private UiGlyph orderIcon;
        private Text orderTitle, orderInfo;
        private Button restartButton;
        private GameObject overlay, ending;
        private Button startButton, stationButton, menuButton, scanButton, medkitButton, rescueButton;
        private Text coins, depth, zone, backpack, bagCaption, record, target, hint, toast, toolName, toolKeys;
        private Text endingTitle, endingText, startHelp, startKicker, startSub, startAction, stationText, medkitText, scanText, healthText, fuelText;
        private Image depthFill, crosshair, bagFill, healthFill, fuelFill;
        private RawImage vignette;
        private Image fade, flash;
        private float flashAt = -10, flashStrength;
        private float fadeStart = -10, fadeLength = 0.45f, hurtAt = -10, hurtStrength, bagFullAt = -10;
        private RectTransform announceCard;
        private CanvasGroup announceGroup;
        private UiGlyph announceIcon;
        private Text announceKicker, announceTitle, announceNote;
        private float announceStart = -10;
        private const float AnnounceTime = 3.4f;
        private readonly List<Image> zoneTicks = new List<Image>();
        private readonly List<FloatingText> popups = new List<FloatingText>();
        private readonly List<ScanMark> scanMarks = new List<ScanMark>();
        private bool jumpQueued, portrait, lastTouch;
        private int lastWidth, lastHeight, lastCoins = -1;
        private float toastUntil, hitUntil, pulseUntil, shownCoins;

        private sealed class FloatingText { public Text label; public Vector3 world; public float age = 1; }

        // Texts are rebuilt only when what they show changes: building strings every frame fed the garbage collector,
        // whose pauses showed as hitches in the browser.
        private readonly Dictionary<Text, int> textKeys = new Dictionary<Text, int>();
        private Text digLabel;
        private bool Changed(Text label, int key)
        {
            if (textKeys.TryGetValue(label, out int last) && last == key) return false;
            textKeys[label] = key;
            return true;
        }
        private sealed class ScanMark { public RectTransform rect; public UiGlyph icon; public Text label; }

        public void Setup(MineGame owner)
        {
            game = owner;
            font = Resources.Load<Font>("Fonts/NotoSans");
            rounded = MakeSprite("UI rounded corners", 32, 7.5f, 10);
            circle = MakeSprite("UI circle", 64, 0, 0);
            var canvas = new GameObject("Game UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = Rect("Safe area", canvas.transform);
            Stretch(root);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // The vignette sits under everything else, deepens underground and flashes red on a hard landing.
            vignette = Rect("Vignette", root).gameObject.AddComponent<RawImage>();
            vignette.texture = MakeVignette();
            vignette.raycastTarget = false;
            Stretch(vignette.rectTransform);
            // Short fade from dark after waking at home; under the cards and windows so they stay crisp.
            fade = Panel("Fade", root, Color.black, false).GetComponent<Image>();
            Stretch(fade.rectTransform);
            fade.enabled = false;
            // A warm flash over the view when dynamite goes off nearby.
            flash = Panel("Flash", root, new Color(1f, .75f, .4f, 0), false).GetComponent<Image>();
            Stretch(flash.rectTransform);
            flash.enabled = false;

            BuildScanMarks();
            BuildTouch();
            BuildStats();
            BuildStart();
            BuildHouse();
            BuildEnding();
            BuildAnnouncement();
            BuildSettings();
            // Toasts sit above modals: sale feedback stays readable in the house.
            toastCard = Panel("Notification", root, Ink);
            Icon(toastCard, UiGlyph.Kind.Coin, Amber, 14, 16, 28);
            toast = Caption(toastCard, "", 18, Cream, 52, 9, 422, 48);
            // Only buttons, touch areas and modal backdrops catch the pointer.
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                image.raycastTarget = image.GetComponent<Button>() != null || image.GetComponent<TouchStick>() != null ||
                    image.GetComponent<TouchLook>() != null || image.gameObject == overlay || image.gameObject == house || image.gameObject == ending ||
                    image.gameObject == settings || image.GetComponentInParent<Slider>(true) != null;
            Layout();
        }

        private void BuildTouch()
        {
            touchControls = Rect("Touch controls", root);
            Stretch(touchControls);
            var look = Panel("Look area", touchControls, Color.clear, false);
            Stretch(look);
            look.anchorMin = new Vector2(0.34f, 0);
            Look = look.gameObject.AddComponent<TouchLook>();
            pad = Panel("Movement stick", touchControls, Card);
            pad.GetComponent<Image>().sprite = circle;
            pad.GetComponent<Image>().type = Image.Type.Simple;
            var ring = Panel("Stick ring", pad, new Color(Mint.r, Mint.g, Mint.b, 0.18f), false);
            ring.GetComponent<Image>().sprite = circle;
            Center(ring, 0, 0, 104, 104);
            Stick = pad.gameObject.AddComponent<TouchStick>();
            Stick.knob = Panel("Stick knob", pad, Mint, false);
            Stick.knob.GetComponent<Image>().sprite = circle;
            Center(Stick.knob, 0, 0, 58, 58);
            var dig = Action(touchControls, "КОПАТЬ", Amber, null);
            digRect = (RectTransform)dig.transform;
            Dig = dig.gameObject.AddComponent<HoldButton>();
            // Tap to jump; keep holding in the air to fire the jetpack.
            var jump = Action(touchControls, "ПРЫЖОК", Inset, null, Cream);
            jumpRect = (RectTransform)jump.transform;
            Jump = jump.gameObject.AddComponent<HoldButton>();
            scanButton = Action(touchControls, "СКАН", Inset, game.Scan, Cream);
            scanRect = (RectTransform)scanButton.transform;
            scanText = scanButton.GetComponentInChildren<Text>();
            medkitButton = Action(touchControls, "", Inset, game.UseMedkit, Cream);
            medkitRect = (RectTransform)medkitButton.transform;
            medkitText = medkitButton.GetComponentInChildren<Text>();
            dynamiteButton = Action(touchControls, "", Inset, game.ThrowDynamite, Cream);
            dynamiteRect = (RectTransform)dynamiteButton.transform;
            dynamiteText = dynamiteButton.GetComponentInChildren<Text>();
        }

        private void BuildStats()
        {
            coinCard = Panel("Wallet", root, Card);
            Icon(coinCard, UiGlyph.Kind.Coin, Amber, 18, 22, 38);
            Caption(coinCard, "МОНЕТЫ", 15, Muted, 70, 10, 150, 22);
            coins = Caption(coinCard, "", 28, Cream, 70, 31, 150, 38, true);

            vitalsCard = Panel("Vitals", root, Card);
            Icon(vitalsCard, UiGlyph.Kind.Heart, Red, 12, 8, 20);
            healthFill = Bar(vitalsCard, 42, 14, 138, Red);
            healthText = Caption(vitalsCard, "", 14, Cream, 188, 3, 40, 26, true);
            fuelRow = Rect("Fuel", vitalsCard);
            At(fuelRow, 0, 30, 232, 30);
            Icon(fuelRow, UiGlyph.Kind.Fuel, Amber, 12, 5, 20);
            fuelFill = Bar(fuelRow, 42, 11, 120, Amber);
            fuelText = Caption(fuelRow, "", 14, Cream, 170, 1, 58, 26, true);

            orderCard = Panel("Order", root, Card);
            orderIcon = Icon(orderCard, UiGlyph.Kind.Order, Amber, 12, 13, 30);
            orderTitle = Caption(orderCard, "", 13, Muted, 52, 5, 172, 22, true);
            orderInfo = Caption(orderCard, "", 16, Cream, 52, 25, 172, 26, true);
            orderInfo.resizeTextForBestFit = true;
            orderInfo.resizeTextMinSize = 11;
            orderInfo.resizeTextMaxSize = 16;

            depthCard = Panel("Depth", root, Card);
            Icon(depthCard, UiGlyph.Kind.Down, Mint, 15, 17, 32);
            depth = Caption(depthCard, "", 24, Cream, 54, 5, 110, 40, true);
            zone = Caption(depthCard, "", 15, Muted, 170, 13, 140, 25);
            zone.alignment = TextAnchor.MiddleRight;
            depthTrack = Panel("Depth track", depthCard, Inset);
            depthFill = Panel("Depth progress", depthTrack, Mint).GetComponent<Image>();
            Stretch(depthFill.rectTransform);
            // Marks where the next zones begin, so the bar reads as a route.
            var config = game.config;
            for (int i = 1; i < config.zones.Length; i++)
            {
                var tick = Panel("Zone mark", depthTrack, Cream, false);
                tick.anchorMin = tick.anchorMax = new Vector2(config.zones[i].startDepth / (float)config.depth, 0.5f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.sizeDelta = new Vector2(2, 12);
                zoneTicks.Add(tick.GetComponent<Image>());
            }
            record = Caption(depthCard, "", 12, Muted, 18, 62, 284, 20);

            bagCard = Panel("Backpack", root, Card);
            Icon(bagCard, UiGlyph.Kind.Bag, Mint, 18, 18, 36);
            bagCaption = Caption(bagCard, "", 14, Muted, 70, 8, 162, 22);
            backpack = Caption(bagCard, "", 24, Cream, 70, 28, 162, 34, true);
            bagFill = Bar(bagCard, 70, 66, 150, Mint);

            actions = Rect("Actions", root);
            stationButton = Action(actions, "", Amber, game.Interact);
            stationText = stationButton.GetComponentInChildren<Text>();
            menuButton = Action(actions, "", Inset, game.OpenMenu, Cream);
            Center(Icon(menuButton.transform, UiGlyph.Kind.Menu, Cream, 0, 0, 30).rectTransform, 0, 0, 30, 30);

            toolCard = Panel("Tool card", root, Card);
            Icon(toolCard, UiGlyph.Kind.Shovel, Amber, 12, 14, 36);
            toolName = Caption(toolCard, "", 18, Cream, 58, 7, 430, 26, true);
            toolKeys = Caption(toolCard, "", 12, Muted, 58, 34, 430, 19);
            toolKeys.resizeTextForBestFit = true;
            toolKeys.resizeTextMinSize = 9;
            toolKeys.resizeTextMaxSize = 12;

            targetCard = Panel("Target", root, Card);
            target = Caption(targetCard, "", 17, Cream, 12, 4, 400, 34);
            target.alignment = TextAnchor.MiddleCenter;
            crosshair = Panel("Crosshair", root, Cream, false).GetComponent<Image>();
            crosshair.sprite = circle;
            var outline = crosshair.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.65f);
            outline.effectDistance = new Vector2(1, -1);
            hint = Caption(root, "", 16, Cream, 0, 0, 600, 46);
            hint.alignment = TextAnchor.MiddleCenter;
            hint.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
        }

        private void BuildScanMarks()
        {
            for (int i = 0; i < 14; i++)
            {
                var rect = Rect("Scan mark", root);
                var icon = rect.gameObject.AddComponent<UiGlyph>();
                icon.raycastTarget = false;
                var label = Caption(rect, "", 13, Cream, -47, 28, 120, 20, true);
                label.alignment = TextAnchor.MiddleCenter;
                label.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
                rect.gameObject.SetActive(false);
                scanMarks.Add(new ScanMark { rect = rect, icon = icon, label = label });
            }
        }

        private void BuildStart()
        {
            overlay = Backdrop("Start overlay");
            startCard = Panel("Start card", overlay.transform, Ink);
            Center(startCard, 0, 0, 480, 470);
            Icon(startCard, UiGlyph.Kind.Shovel, Amber, 210, 24, 60);
            startKicker = Caption(startCard, "", 12, Mint, 20, 94, 440, 24);
            startKicker.alignment = TextAnchor.MiddleCenter;
            var title = Caption(startCard, "НУБИК ШАХТЁР", 37, Cream, 20, 124, 440, 55, true);
            title.alignment = TextAnchor.MiddleCenter;
            startSub = Caption(startCard, "", 19, Muted, 24, 185, 432, 34);
            startSub.alignment = TextAnchor.MiddleCenter;
            startButton = Action(startCard, "", Amber, game.Engage);
            At((RectTransform)startButton.transform, 32, 236, 416, 60);
            startAction = startButton.GetComponentInChildren<Text>();
            rescueButton = Action(startCard, "Вызвать спасателей · руда останется в шахте", Inset, game.CallRescue, Cream);
            At((RectTransform)rescueButton.transform, 32, 306, 416, 48);
            restartButton = Action(startCard, "Начать игру заново", Inset, () => ConfirmRestart(true), new Color(1f, .62f, .55f));
            startHelp = Caption(startCard, "", 14, Muted, 32, 364, 416, 92);
            startHelp.alignment = TextAnchor.MiddleCenter;
            startGear = Action(startCard, "", Inset, OpenSettings, Cream);
            At((RectTransform)startGear.transform, 416, 16, 48, 48);
            Center(Icon(startGear.transform, UiGlyph.Kind.Gear, Cream, 0, 0, 30).rectTransform, 0, 0, 30, 30);

            confirmCard = Panel("Restart confirm", overlay.transform, Ink);
            Center(confirmCard, 0, 0, 480, 300);
            Icon(confirmCard, UiGlyph.Kind.Heart, Red, 212, 22, 56);
            var question = Caption(confirmCard, "Начать заново?", 30, Cream, 24, 88, 432, 44, true);
            question.alignment = TextAnchor.MiddleCenter;
            var warning = Caption(confirmCard, "Монеты, улучшения, ключи, секреты и выкопанная земля будут удалены. Отменить это нельзя.", 16, Muted, 32, 134, 416, 60);
            warning.alignment = TextAnchor.MiddleCenter;
            var wipe = Action(confirmCard, "Да, всё сначала", Red, game.RestartGame);
            At((RectTransform)wipe.transform, 32, 212, 200, 58);
            var keep = Action(confirmCard, "Отмена", Amber, () => ConfirmRestart(false));
            At((RectTransform)keep.transform, 248, 212, 200, 58);
            confirmCard.gameObject.SetActive(false);
        }

        private void ConfirmRestart(bool ask)
        {
            confirmCard.gameObject.SetActive(ask);
            startCard.gameObject.SetActive(!ask);
        }

        private void BuildEnding()
        {
            ending = Backdrop("Ending");
            var endCard = Panel("Ending card", ending.transform, Ink);
            Center(endCard, 0, 0, 480, 500);
            Icon(endCard, UiGlyph.Kind.Key, Mint, 212, 24, 56);
            endingTitle = Caption(endCard, "", 27, Cream, 24, 94, 432, 44, true);
            endingTitle.alignment = TextAnchor.MiddleCenter;
            endingText = Caption(endCard, "", 18, Muted, 28, 151, 424, 180);
            var retry = Action(endCard, "Продолжить", Amber, () =>
            {
                if (game.Battle.Phase == BattlePhase.Lost) game.RetryBoss();
                else { ending.SetActive(false); game.Engage(); }
            });
            At((RectTransform)retry.transform, 28, 346, 424, 58);
            var home = Action(endCard, "Вернуться домой", Inset, game.ReturnFromBoss, Cream);
            At((RectTransform)home.transform, 28, 418, 424, 54);
            ending.SetActive(false);
        }

        /// <summary>Large card for milestones: a new zone, a collection item, waking up at home.</summary>
        private void BuildAnnouncement()
        {
            announceCard = Panel("Announcement", root, Ink);
            announceGroup = announceCard.gameObject.AddComponent<CanvasGroup>();
            announceGroup.blocksRaycasts = false;
            var badge = Panel("Badge", announceCard, Inset);
            At(badge, 16, 16, 86, 86);
            announceIcon = Icon(badge, UiGlyph.Kind.Down, Mint, 17, 17, 52);
            announceKicker = Caption(announceCard, "", 13, Mint, 118, 14, 330, 22, true);
            announceTitle = Caption(announceCard, "", 30, Cream, 118, 34, 330, 42, true);
            announceNote = Caption(announceCard, "", 15, Muted, 118, 76, 330, 36);
            announceNote.resizeTextForBestFit = true;
            announceNote.resizeTextMinSize = 11;
            announceNote.resizeTextMaxSize = 15;
            announceCard.gameObject.SetActive(false);
        }

        public void Announce(UiGlyph.Kind icon, Color color, string kicker, string title, string note)
        {
            announceIcon.kind = icon;
            announceIcon.color = color;
            announceKicker.text = kicker;
            announceKicker.color = color;
            announceTitle.text = title;
            announceNote.text = note;
            announceStart = Time.unscaledTime;
        }

        private void UpdateAnnouncement(bool blocked)
        {
            float age = Time.unscaledTime - announceStart;
            // Windows and the start screen hold the announcement until they close.
            if (blocked && age < AnnounceTime) announceStart = Mathf.Min(Time.unscaledTime, announceStart + Time.unscaledDeltaTime);
            bool visible = age < AnnounceTime && !blocked;
            announceCard.gameObject.SetActive(visible);
            if (!visible) return;
            // Pops in, holds, then fades out.
            float appear = Mathf.Clamp01(age / 0.25f), vanish = Mathf.Clamp01((AnnounceTime - age) / 0.45f);
            announceGroup.alpha = Mathf.Min(appear, vanish);
            announceCard.localScale = Vector3.one * Mathf.Lerp(0.88f, 1, 1 - (1 - appear) * (1 - appear));
        }

        private void Layout()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastTouch = game.TouchMode;
            portrait = Screen.width < Screen.height;
            bool touch = game.TouchMode, wideTouch = touch && !portrait;
            scaler.referenceResolution = portrait ? new Vector2(540, 960) : new Vector2(1280, 720);

            float edge = portrait ? 14 : 24;
            At(coinCard, edge, edge, portrait ? 248 : 232, 84);
            Right(bagCard, edge, edge, portrait ? 248 : 240, 84);
            if (portrait)
            {
                At(depthCard, 14, 112, 512, 86);
                At(zone.rectTransform, 330, 13, 160, 25);
            }
            else
            {
                CenterTop(depthCard, 24, 320, 86);
                At(zone.rectTransform, 170, 13, 132, 25);
            }
            At(depthTrack, 18, 52, portrait ? 476 : 284, 6);
            At(record.rectTransform, 18, 62, portrait ? 476 : 284, 20);
            At(vitalsCard, edge, portrait ? 206 : 118, 232, 62);
            At(orderCard, edge, portrait ? 276 : 188, 232, 56);

            float actionHeight = touch ? (portrait ? 64 : 72) : 56;
            Right(actions, edge, portrait ? 206 : 118, 246, actionHeight);
            At((RectTransform)stationButton.transform, 0, 0, touch ? 170 : 246, actionHeight);
            At((RectTransform)menuButton.transform, 180, 0, 66, actionHeight);

            BottomLeft(toolCard, 24, 24, 500, 60);
            BottomCenter(hint.rectTransform, touch ? 206 : 96, portrait ? 492 : 700, 48);
            Center(targetCard, 0, -54, 424, 42);
            Center(crosshair.rectTransform, 0, 0, 7, 7);
            CenterTop(toastCard, portrait ? 300 : 194, portrait ? 504 : 500, 68);
            CenterTop(announceCard, portrait ? 300 : 170, 470, 118);
            BottomLeft(pad, 24, 28, 150, 150);
            BottomRight(digRect, 24, 28, 156, 132);
            if (portrait)
            {
                BottomRight(jumpRect, 24, 174, 156, 64);
                BottomRight(scanRect, 190, 110, 104, 56);
                BottomRight(medkitRect, 190, 174, 104, 56);
                BottomRight(dynamiteRect, 190, 238, 104, 56);
            }
            else
            {
                BottomRight(jumpRect, 196, 28, 124, 88);
                BottomRight(scanRect, 24, 174, 110, 58);
                BottomRight(medkitRect, 144, 174, 124, 58);
                BottomRight(dynamiteRect, 278, 174, 130, 58);
            }
            At((RectTransform)startButton.transform, 32, 236, 416, wideTouch ? 64 : 60);
            LayoutHouse(portrait, wideTouch);
        }

        private void Update()
        {
            if (game == null || game.Progress == null) return;
            var safe = Screen.safeArea;
            root.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            if (lastWidth != Screen.width || lastHeight != Screen.height || lastTouch != game.TouchMode) Layout();

            var progress = game.Progress;
            var config = game.config;
            int metres = game.Depth, zoneIndex = config.ZoneIndex(metres);
            if (lastCoins != progress.coins)
            {
                if (lastCoins >= 0) pulseUntil = Time.unscaledTime + 0.35f;
                else shownCoins = progress.coins;
                lastCoins = progress.coins;
            }
            // Coins roll towards the new total instead of jumping.
            shownCoins = Mathf.MoveTowards(shownCoins, progress.coins, Mathf.Max(30, Mathf.Abs(progress.coins - shownCoins) * 10) * Time.unscaledDeltaTime);
            int shown = Mathf.RoundToInt(shownCoins);
            if (Changed(coins, shown)) coins.text = shown.ToString("N0");
            coins.color = Time.unscaledTime < pulseUntil ? Amber : Cream;
            if (Changed(depth, game.InBoss ? -1 : metres)) depth.text = game.InBoss ? "КТУЛХУ" : metres + " м";
            if (Changed(zone, game.InBoss ? (game.Battle.Enraged ? -2 : -1) : zoneIndex))
                zone.text = game.InBoss ? (game.Battle.Enraged ? "ЯРОСТЬ" : "ДРЕВНИЙ") : config.zones[zoneIndex].nameRu.ToUpperInvariant();
            if (Changed(record, game.InBoss ? -1 : (progress.finished ? 1000 : 0) + progress.KeyCount * 10 + (progress.doorOpened ? 1 : 0)))
                record.text = game.InBoss ? "Голова уязвима между атаками" : game.ExpeditionGoal;
            depthFill.rectTransform.anchorMax = new Vector2(game.InBoss ? game.Battle.Health / BossBattle.MaxHealth : Mathf.Clamp01(metres / (float)config.depth), 1);
            depthFill.color = game.InBoss ? Red : Mint;
            for (int i = 0; i < zoneTicks.Count; i++)
            {
                zoneTicks[i].gameObject.SetActive(!game.InBoss);
                zoneTicks[i].color = progress.maxDepth >= config.zones[i + 1].startDepth ? Mint : Cream;
            }
            UpdateVitals();
            UpdateBag(progress, config);
            bool armed = game.InBoss && progress.hasWeapon, drill = game.UsingDrill;
            if (Changed(toolName, armed ? -1 : progress.tool * 2 + (drill ? 1 : 0)))
                toolName.text = armed ? "Древний гарпун" : progress.tool == config.tools.Length - 1 && !drill ? "Лопата · бак пуст" : game.Tool.nameRu;
            if (digLabel == null) digLabel = digRect.GetComponentInChildren<Text>();
            if (Changed(digLabel, game.InBoss ? 2 : drill ? 1 : 0)) digLabel.text = game.InBoss ? "СТРЕЛЯТЬ" : drill ? "БУРИТЬ" : "КОПАТЬ";
            int keysShown = (game.InBoss ? 1 : 0) | (drill ? 2 : 0) | (game.FreeMouse ? 4 : 0) | (game.HasJetpack ? 8 : 0) | (progress.scanner ? 16 : 0) |
                (progress.medkits > 0 ? 32 : 0) | (progress.dynamite > 0 ? 64 : 0);
            if (Changed(toolKeys, keysShown)) toolKeys.text = (game.InBoss ? "ЛКМ стрелять" : game.UsingDrill ? "ЛКМ бурить" : "ЛКМ копать") + (game.FreeMouse ? " · ПКМ обзор" : "") + " · ПРОБЕЛ " +
                (game.HasJetpack ? "прыжок / полёт" : "прыжок") + (progress.scanner ? " · F скан" : "") +
                (progress.medkits > 0 ? " · Q аптечка" : "") + (progress.dynamite > 0 ? " · G динамит" : "");

            bool modal = PanelOpen, waiting = game.NeedsClick;
            UpdateStart(progress, config, waiting);
            coinCard.gameObject.SetActive(!modal);
            depthCard.gameObject.SetActive(!modal);
            bagCard.gameObject.SetActive(!modal);
            vitalsCard.gameObject.SetActive(!modal);
            UpdateOrder(progress, config, modal || waiting);
            actions.gameObject.SetActive(!modal && !waiting);
            stationButton.gameObject.SetActive(!string.IsNullOrEmpty(game.Interaction));
            var interaction = game.Interaction;
            if (Changed(stationText, (interaction ?? "").GetHashCode() ^ (game.TouchMode ? 1 : 0))) stationText.text = interaction + (game.TouchMode ? "" : "  ·  E");
            menuButton.gameObject.SetActive(game.TouchMode);
            touchControls.gameObject.SetActive(game.TouchMode && !modal && !waiting);
            scanButton.gameObject.SetActive(progress.scanner);
            int wait = Mathf.CeilToInt(game.ScanWait);
            if (Changed(scanText, wait)) scanText.text = wait > 0 ? "СКАН " + wait : "СКАН";
            medkitButton.gameObject.SetActive(progress.medkits > 0);
            if (Changed(medkitText, progress.medkits)) medkitText.text = "АПТЕЧКА ×" + progress.medkits;
            dynamiteButton.gameObject.SetActive(progress.dynamite > 0 && !game.InBoss);
            if (Changed(dynamiteText, progress.dynamite)) dynamiteText.text = "ДИНАМИТ ×" + progress.dynamite;
            toolCard.gameObject.SetActive(!game.TouchMode && !modal && !waiting);
            targetCard.gameObject.SetActive(game.Active && !string.IsNullOrEmpty(game.TargetText));
            target.text = game.TargetText;
            crosshair.enabled = game.Active;
            bool hit = Time.unscaledTime < hitUntil;
            crosshair.color = hit ? Amber : game.InReach ? Mint : Cream;
            crosshair.rectTransform.sizeDelta = Vector2.one * (hit ? 11 : 7);
            hint.text = modal || waiting ? "" : game.Hint;
            UpdateAnnouncement(modal || waiting);
            toastCard.gameObject.SetActive(Time.unscaledTime < toastUntil && !modal && !waiting && !announceCard.gameObject.activeSelf);
            float flashLeft = flashStrength * (1 - (Time.unscaledTime - flashAt) / .45f);
            flash.enabled = flashLeft > 0;
            if (flash.enabled) flash.color = new Color(1f, .75f, .4f, flashLeft * .6f);
            float fadeAge = (Time.unscaledTime - fadeStart) / fadeLength;
            fade.enabled = fadeAge < 1;
            if (fade.enabled) fade.color = new Color(0.02f, 0.04f, 0.05f, 1 - Mathf.SmoothStep(0, 1, fadeAge));
            // Red edge after a hard landing, fading over a second.
            float hurt = Mathf.Clamp01(1 - (Time.unscaledTime - hurtAt)) * hurtStrength;
            float dark = modal || waiting ? 0.35f : Mathf.Lerp(0.3f, 0.82f, Mathf.Clamp01(metres / 12f));
            vignette.color = Color.Lerp(new Color(0, 0, 0, dark), new Color(0.75f, 0.05f, 0.03f, 0.9f), hurt);
            UpdatePopups();
            UpdateScanMarks();
            if (house.activeSelf) UpdateHouse();
            if (SettingsOpen) UpdateSettings();
        }

        private void UpdateVitals()
        {
            float health = game.Health, max = game.MaxHealth;
            healthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(health / max), 1);
            healthFill.color = health < max * 0.3f ? Color.Lerp(Red, Cream, Mathf.PingPong(Time.unscaledTime * 3, 1) * 0.5f) : Red;
            int hp = Mathf.CeilToInt(health);
            if (Changed(healthText, hp)) healthText.text = hp.ToString();
            bool jet = game.HasJetpack;
            fuelRow.gameObject.SetActive(jet);
            vitalsCard.sizeDelta = new Vector2(232, jet ? 62 : 36);
            if (!jet) return;
            fuelFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(game.Fuel / Mathf.Max(0.01f, game.FuelMax)), 1);
            fuelFill.color = game.Fuel < game.FuelMax * .2f ? Red : game.Refuelling ? Mint : Amber;
            int litres = Mathf.FloorToInt(game.Fuel);
            if (Changed(fuelText, litres)) fuelText.text = litres + " л";
        }

        private void UpdateBag(GameProgress progress, MineConfig config)
        {
            int used = progress.UsedSlots(config), capacity = progress.Capacity(config);
            bool full = used >= capacity;
            bool flash = Time.unscaledTime - bagFullAt < 0.8f;
            if (Changed(bagCaption, full ? -1 : used * 1000 + capacity)) bagCaption.text = full ? "РЮКЗАК ПОЛОН" : "РЮКЗАК  ·  " + used + " / " + capacity;
            bagCaption.color = full || flash ? Red : Muted;
            int value = progress.BagValue(config);
            if (Changed(backpack, value)) backpack.text = value + " " + MineGame.Plural(value, "монета", "монеты", "монет");
            bagFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(used / (float)Mathf.Max(1, capacity)), 1);
            bagFill.color = full || flash ? Red : Mint;
        }

        private void UpdateOrder(GameProgress progress, MineConfig config, bool hidden)
        {
            bool show = !hidden && !game.InBoss && progress.HasQuest;
            orderCard.gameObject.SetActive(show);
            if (!show) return;
            var ore = config.ores[progress.questOre];
            int have = Mathf.Min(progress.OreCount(progress.questOre), progress.questAmount);
            bool ready = progress.CanDeliver;
            orderIcon.color = ready ? Mint : Amber;
            if (Changed(orderTitle, progress.questOre)) orderTitle.text = "ЗАКАЗ · " + ore.nameRu.ToUpperInvariant();
            if (Changed(orderInfo, (ready ? 1 : 0) + have * 2 + progress.questAmount * 2000 + progress.questReward * 2000000))
                orderInfo.text = ready ? "Готово · +" + progress.questReward + " в доме" : have + " / " + progress.questAmount + "  ·  +" + progress.questReward + " монет";
            orderInfo.color = ready ? Mint : Cream;
        }

        private void UpdateStart(GameProgress progress, MineConfig config, bool waiting)
        {
            overlay.SetActive(waiting && !SettingsOpen);
            if (!waiting) { if (confirmCard.gameObject.activeSelf) ConfirmRestart(false); return; }
            // Returning players see their progress instead of the tagline; the same card is the pause menu.
            bool returning = progress.maxDepth > 0 || progress.expeditions > 0;
            startKicker.text = game.MenuOpen ? "ПАУЗА" : returning ? "С ВОЗВРАЩЕНИЕМ" : "МАЛЕНЬКИЙ ДВОР. БОЛЬШОЕ ПРИКЛЮЧЕНИЕ.";
            startSub.text = returning ? "Ключи " + progress.KeyCount + " / 5 · глубина " + progress.maxDepth + " м"
                : "Пять ключей. Дверь. Тайна глубины.";
            startAction.text = game.MenuOpen || returning ? "Продолжить" : "Начать вылазку";
            rescueButton.gameObject.SetActive(game.CanRescue);
            restartButton.gameObject.SetActive(returning);
            // Optional buttons stack under the main one; the card grows to fit them.
            float y = 306;
            if (game.CanRescue) { At((RectTransform)rescueButton.transform, 32, y, 416, 48); y += 56; }
            if (returning) { At((RectTransform)restartButton.transform, 32, y, 416, 44); y += 52; }
            At(startHelp.rectTransform, 32, y + 2, 416, 92);
            startCard.sizeDelta = new Vector2(480, y + 108);
            startHelp.text = game.TouchMode ? "Стик — идти, справа — осмотр\n" +
                (game.HasJetpack ? "Держи ПРЫЖОК в воздухе — джетпак\n" : "Копай ступеньки, чтобы вернуться наверх\n") + "Общий бензобак заправляется на базе"
                : "WASD — идти · " + (game.FreeMouse ? "ПКМ — обзор" : "мышь — обзор") + "\nЛКМ — копать · пробел — прыжок" +
                  (game.HasJetpack ? " / полёт" : "") + "\nE — скупка и мастерская в доме · Esc — пауза" +
                  (progress.scanner || progress.medkits > 0 || progress.dynamite > 0 ? "\n" + (progress.scanner ? "F — сканер  " : "") +
                   (progress.medkits > 0 ? "Q — аптечка  " : "") + (progress.dynamite > 0 ? "G — динамит" : "") : "");
        }

        private void UpdateScanMarks()
        {
            var camera = Camera.main;
            var hits = game.ScanHits;
            bool show = game.ScanActive && !PanelOpen && camera != null;
            for (int i = 0; i < scanMarks.Count; i++)
            {
                var mark = scanMarks[i];
                if (!show || i >= hits.Count) { mark.rect.gameObject.SetActive(false); continue; }
                var item = hits[i];
                var screen = camera.WorldToScreenPoint(item.Position);
                if (screen.z <= 0) { mark.rect.gameObject.SetActive(false); continue; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
                mark.rect.gameObject.SetActive(true);
                Center(mark.rect, local.x, local.y, 26, 26);
                mark.icon.kind = item.Kind == LootKind.Secret ? UiGlyph.Kind.Star : item.Kind == LootKind.Key || item.Kind == LootKind.Collectible ? UiGlyph.Kind.Key
                    : item.Kind == LootKind.Chest ? UiGlyph.Kind.Bag : UiGlyph.Kind.Gem;
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6 + i);
                mark.icon.color = new Color(item.Color.r, item.Color.g, item.Color.b, pulse);
                string name = item.Kind == LootKind.Key ? Expedition.Keys[item.Key].Name : item.Ore < 0 ? "???" : game.config.ores[item.Ore].nameRu;
                mark.label.text = name + " · " + Mathf.RoundToInt(screen.z) + " м";
            }
        }

        public void ClosePanel()
        {
            // The settings sit over the house or the pause card: close only them first.
            if (SettingsOpen) { CloseSettings(); return; }
            if (house.activeSelf) game.Ui("close");
            house.SetActive(false);
            ending.SetActive(false);
            ClearInput();
        }

        public void ShowBattleResult(bool won)
        {
            endingTitle.text = won ? "ДВОР СПАСЁН" : "КТУЛХУ ОКАЗАЛСЯ СИЛЬНЕЕ";
            endingTitle.resizeTextForBestFit = true; endingTitle.resizeTextMinSize = 19; endingTitle.resizeTextMaxSize = 27;
            endingText.text = won ? "Пять ключей разомкнули печать, а твой гарпун отправил Ктулху обратно в глубину.\n\nТеперь под двором тихо. Можно вернуться домой или продолжить поиски коллекции. Победа сохранена."
                : "Ключи и покупки сохранены.\n\nВыходи из светящихся зон до удара. Между атаками голова уязвима — целься в глаза.\n\n«Продолжить» начнёт новую попытку.";
            ClearInput(); ending.SetActive(true);
        }

        public void ClearInput() { Stick.ResetInput(); Dig.ResetInput(); Look.ResetInput(); Jump.ResetInput(); jumpQueued = false; }
        public bool ConsumeJump() { bool value = jumpQueued; jumpQueued = false; return value; }
        public void Notify(string message) { toast.text = message; toastUntil = Time.unscaledTime + 3.6f; }
        public void HitFeedback() => hitUntil = Time.unscaledTime + 0.13f;
        public void FadeIn(float seconds = 0.45f) { fadeStart = Time.unscaledTime; fadeLength = seconds; }
        public void Hurt(float share) { hurtAt = Time.unscaledTime; hurtStrength = Mathf.Clamp(share * 2.5f, 0.35f, 1f); }
        public void BagFull() => bagFullAt = Time.unscaledTime;
        public void Flash(float strength) { if (strength <= 0) return; flashAt = Time.unscaledTime; flashStrength = strength; }

        public void Popup(Vector3 world, string text, Color color)
        {
            FloatingText popup = null;
            foreach (var candidate in popups) if (candidate.age >= 1) { popup = candidate; break; }
            if (popup == null)
            {
                if (popups.Count >= 12) return;
                popup = new FloatingText { label = Caption(root, "", 22, color, 0, 0, 350, 48, true) };
                popup.label.alignment = TextAnchor.MiddleCenter;
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
                popup.age += Time.unscaledDeltaTime / 1.05f;
                var screen = camera != null ? camera.WorldToScreenPoint(popup.world) : Vector3.back;
                if (screen.z <= 0 || PanelOpen) { popup.label.enabled = false; continue; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
                Center(popup.label.rectTransform, local.x, local.y + 34 + popup.age * 54, 350, 48);
                var color = popup.label.color;
                color.a = Mathf.Clamp01((1 - popup.age) * 3);
                popup.label.color = color;
                popup.label.enabled = true;
            }
        }

        // ---------- Building blocks ----------

        private GameObject Backdrop(string name)
        {
            var rect = Panel(name, root, Scrim, false);
            Stretch(rect);
            return rect.gameObject;
        }

        private RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }

        private RectTransform Panel(string name, Transform parent, Color color, bool round = true)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (round) { image.sprite = rounded; image.type = Image.Type.Sliced; }
            return rect;
        }

        /// <summary>Thin rounded track with a fill; the fill's right anchor is the value.</summary>
        private Image Bar(Transform parent, float x, float y, float width, Color color)
        {
            var track = Panel("Track", parent, Inset);
            At(track, x, y, width, 8);
            var fill = Panel("Fill", track, color).GetComponent<Image>();
            Stretch(fill.rectTransform);
            return fill;
        }

        private Text Caption(Transform parent, string text, int size, Color color, float x, float y, float w, float h, bool bold = false)
        {
            var rect = Rect("Label", parent);
            At(rect, x, y, w, h);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private UiGlyph Icon(Transform parent, UiGlyph.Kind kind, Color color, float x, float y, float size)
        {
            var rect = Rect("Icon " + kind, parent);
            At(rect, x, y, size, size);
            var icon = rect.gameObject.AddComponent<UiGlyph>();
            icon.kind = kind; icon.color = color; icon.raycastTarget = false;
            return icon;
        }

        private Button Action(Transform parent, string text, Color color, UnityEngine.Events.UnityAction action, Color? foreground = null)
        {
            var rect = Panel(text, parent, color);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.76f, 0.81f, 0.79f);
            colors.disabledColor = new Color(0.64f, 0.68f, 0.66f, 0.9f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            // Menu buttons click; the touch controls are played on, not pressed.
            if (parent != touchControls) button.onClick.AddListener(game.Click);
            var label = Caption(rect, text, 18, foreground ?? Ink, 0, 0, 1, 1, true);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(10, 6);
            label.rectTransform.offsetMax = new Vector2(-10, -6);
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = 18;
            return button;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        /// <summary>Places a rect by its top-left corner, in pixels from the parent's top-left.</summary>
        private static void At(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        private static void Right(RectTransform r, float x, float y, float w, float h)
        {
            At(r, 0, y, w, h);
            r.anchorMin = r.anchorMax = Vector2.one;
            r.pivot = new Vector2(1, 1);
            r.anchoredPosition = new Vector2(-x, -y);
        }

        private static void CenterTop(RectTransform r, float y, float w, float h)
        {
            At(r, 0, y, w, h);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1);
            r.pivot = new Vector2(0.5f, 1);
        }

        private static void Center(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        private static void BottomLeft(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        private static void BottomRight(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1, 0);
            r.anchoredPosition = new Vector2(-x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        private static void BottomCenter(RectTransform r, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0);
            r.anchoredPosition = new Vector2(0, y);
            r.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Anti-aliased rounded square (inset &gt; 0, sliced) or circle (inset 0) generated at runtime.</summary>
        private Sprite MakeSprite(string name, int size, float inset, float border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear };
            float middle = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float alpha;
                    if (inset > 0)
                    {
                        var d = new Vector2(Mathf.Max(0, Mathf.Abs(x - middle) - inset), Mathf.Max(0, Mathf.Abs(y - middle) - inset));
                        alpha = Mathf.Clamp01(middle - inset + 0.5f - d.magnitude);
                    }
                    else alpha = Mathf.Clamp01(middle + 0.5f - new Vector2(x - middle, y - middle).magnitude);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            texture.Apply(false, true);
            textures.Add(texture);
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * border);
        }

        private Texture2D MakeVignette()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UI vignette", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x / (size - 1f) - 0.5f, y / (size - 1f) - 0.5f) * 2;
                    float alpha = Mathf.Pow(Mathf.Clamp01((p.magnitude - 0.62f) / 0.8f), 1.6f);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            texture.Apply(false, true);
            textures.Add(texture);
            return texture;
        }

        private void OnDestroy()
        {
            if (rounded) Destroy(rounded);
            if (circle) Destroy(circle);
            foreach (var texture in textures) Destroy(texture);
        }
    }
}
