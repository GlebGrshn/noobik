using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nubik
{
    /// <summary>Responsive expedition HUD. Layout uses safe-area coordinates and a separate portrait composition.</summary>
    public sealed class MineHud : MonoBehaviour
    {
        public TouchStick Stick { get; private set; }
        public TouchLook Look { get; private set; }
        public HoldButton Dig { get; private set; }
        public bool PanelOpen => shop.activeSelf || confirmation.activeSelf || ending.activeSelf;

        private static readonly Color Ink = new Color(0.065f, 0.105f, 0.12f, 0.98f);
        private static readonly Color Card = new Color(0.08f, 0.135f, 0.15f, 0.92f);
        private static readonly Color Inset = new Color(0.12f, 0.19f, 0.20f, 1);
        private static readonly Color Cream = new Color(0.97f, 0.95f, 0.87f);
        private static readonly Color Muted = new Color(0.69f, 0.78f, 0.77f);
        private static readonly Color Amber = new Color(1, 0.76f, 0.31f);
        private static readonly Color Mint = new Color(0.43f, 0.86f, 0.72f);
        private static readonly Color Scrim = new Color(0.025f, 0.055f, 0.065f, 0.72f);

        private MineGame game;
        private Font font;
        private Sprite rounded, circle;
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private CanvasScaler scaler;
        private RectTransform root, coinCard, depthCard, bagCard, actions, toolCard, targetCard, toastCard, depthTrack;
        private RectTransform shopCard, shopLeft, shopRight, startCard, touchControls, pad, digRect, jumpRect;
        private GameObject overlay, shop, confirmation, ending;
        private Button shopButton, returnButton, upgrade, descend, soundButton;
        private Text coins, depth, zone, backpack, target, hint, toast, toolName, toolKeys;
        private Text shopInfo, upgradeText, upgradeInfo, albumTitle, soundText, endingText, startHelp;
        private Text shopWallet, bagCaption, record;
        private Image depthFill, crosshair;
        private RawImage vignette;
        private RectTransform announceCard;
        private CanvasGroup announceGroup;
        private UiGlyph announceIcon;
        private Text announceKicker, announceTitle, announceNote;
        private float announceStart = -10;
        private const float AnnounceTime = 3.4f;
        private readonly List<Image> zoneTicks = new List<Image>();
        private readonly List<Image> albumTiles = new List<Image>();
        private readonly List<UiGlyph> albumIcons = new List<UiGlyph>();
        private readonly List<Text> albumNames = new List<Text>();
        private readonly List<FloatingText> popups = new List<FloatingText>();
        private bool jumpQueued, portrait, lastTouch;
        private int lastWidth, lastHeight, lastCoins = -1;
        private float toastUntil, hitUntil, pulseUntil, shownCoins;

        private sealed class FloatingText { public Text label; public Vector3 world; public float age = 1; }

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

            // The vignette sits under everything else and deepens underground.
            vignette = Rect("Vignette", root).gameObject.AddComponent<RawImage>();
            vignette.texture = MakeVignette();
            vignette.raycastTarget = false;
            Stretch(vignette.rectTransform);

            BuildTouch();
            BuildStats();
            BuildStart();
            BuildShop();
            BuildDialogs();
            BuildAnnouncement();
            // Toasts sit above modals: automatic sale feedback stays readable when the shop opens.
            toastCard = Panel("Notification", root, Ink);
            Icon(toastCard, UiGlyph.Kind.Coin, Amber, 14, 16, 28);
            toast = Caption(toastCard, "", 18, Cream, 52, 9, 422, 48);
            // Only buttons, touch areas and modal backdrops catch the pointer.
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                image.raycastTarget = image.GetComponent<Button>() != null || image.GetComponent<TouchStick>() != null ||
                    image.GetComponent<TouchLook>() != null || image.gameObject == overlay || image.gameObject == shop ||
                    image.gameObject == confirmation || image.gameObject == ending;
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
            var jump = Action(touchControls, "ПРЫЖОК", Inset, () => jumpQueued = true, Cream);
            jumpRect = (RectTransform)jump.transform;
        }

        private void BuildStats()
        {
            coinCard = Panel("Wallet", root, Card);
            Icon(coinCard, UiGlyph.Kind.Coin, Amber, 18, 22, 38);
            Caption(coinCard, "МОНЕТЫ", 15, Muted, 70, 10, 150, 22);
            coins = Caption(coinCard, "", 28, Cream, 70, 31, 150, 38, true);

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
            Icon(bagCard, UiGlyph.Kind.Bag, Mint, 18, 22, 36);
            bagCaption = Caption(bagCard, "РЮКЗАК", 15, Muted, 70, 10, 158, 22);
            backpack = Caption(bagCard, "", 25, Cream, 70, 33, 156, 35, true);

            actions = Rect("Actions", root);
            returnButton = Action(actions, "Наверх", Amber, game.RequestReturn);
            shopButton = Action(actions, "Лавка", Inset, game.OpenShop, Cream);

            toolCard = Panel("Tool card", root, Card);
            Icon(toolCard, UiGlyph.Kind.Shovel, Amber, 12, 14, 36);
            toolName = Caption(toolCard, "", 18, Cream, 58, 7, 290, 26, true);
            toolKeys = Caption(toolCard, "", 12, Muted, 58, 34, 290, 19);
            toolKeys.resizeTextForBestFit = true;
            toolKeys.resizeTextMinSize = 9;
            toolKeys.resizeTextMaxSize = 12;

            targetCard = Panel("Target", root, Card);
            target = Caption(targetCard, "", 17, Cream, 12, 4, 360, 34);
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

        /// <summary>Large card for milestones: a new zone or a collection item.</summary>
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

        private void BuildStart()
        {
            overlay = Backdrop("Start overlay");
            startCard = Panel("Start card", overlay.transform, Ink);
            Center(startCard, 0, 0, 480, 438);
            Icon(startCard, UiGlyph.Kind.Shovel, Amber, 210, 24, 60);
            var kicker = Caption(startCard, "МАЛЕНЬКИЙ ДВОР. БОЛЬШОЕ ПРИКЛЮЧЕНИЕ.", 12, Mint, 20, 94, 440, 24);
            kicker.alignment = TextAnchor.MiddleCenter;
            var title = Caption(startCard, "НУБИК ШАХТЁР", 37, Cream, 20, 124, 440, 55, true);
            title.alignment = TextAnchor.MiddleCenter;
            var sub = Caption(startCard, "Копай глубже. Находи сокровища.", 19, Muted, 24, 185, 432, 34);
            sub.alignment = TextAnchor.MiddleCenter;
            var start = Action(startCard, "Начать вылазку", Amber, game.Engage);
            At((RectTransform)start.transform, 32, 244, 416, 60);
            startHelp = Caption(startCard, "", 15, Muted, 32, 324, 416, 82);
            startHelp.alignment = TextAnchor.MiddleCenter;
        }

        private void BuildShop()
        {
            shop = Backdrop("Shop");
            shopCard = Panel("Shop card", shop.transform, Ink);
            Caption(shopCard, "ЛАВКА ШАХТЁРА", 30, Cream, 28, 20, 430, 42, true);
            Caption(shopCard, "Отдохни перед следующей вылазкой", 16, Muted, 28, 65, 470, 28);
            shopLeft = Panel("Equipment", shopCard, Card);
            shopRight = Panel("Collection", shopCard, Card);
            Caption(shopLeft, "ТВОЙ ИНСТРУМЕНТ", 13, Muted, 20, 14, 268, 24);
            Icon(shopLeft, UiGlyph.Kind.Shovel, Amber, 20, 53, 48);
            shopInfo = Caption(shopLeft, "", 20, Cream, 80, 47, 215, 65, true);
            Caption(shopLeft, "УЛУЧШЕНИЕ", 13, Mint, 20, 130, 260, 24);
            upgradeInfo = Caption(shopLeft, "", 16, Muted, 20, 158, 278, 55);
            upgrade = Action(shopLeft, "", Amber, game.Upgrade);
            upgradeText = upgrade.GetComponentInChildren<Text>();
            albumTitle = Caption(shopRight, "", 20, Cream, 20, 14, 422, 30, true);
            Caption(shopRight, "Истории, спрятанные под землёй", 15, Muted, 20, 50, 422, 26);
            for (int i = 0; i < game.config.collection.Length; i++)
            {
                var tile = Panel("Collection item " + i, shopRight, Inset);
                At(tile, 20 + i * 86, 94, 76, 86);
                albumTiles.Add(tile.GetComponent<Image>());
                albumIcons.Add(Icon(tile, (UiGlyph.Kind)((int)UiGlyph.Kind.Helmet + i), Muted, 16, 19, 44));
                var name = Caption(shopRight, "", 12, Muted, 16 + i * 86, 188, 84, 54);
                name.alignment = TextAnchor.UpperCenter;
                albumNames.Add(name);
            }
            var note = Caption(shopRight, "Находки продаются при выходе из шахты.\nКоллекция остаётся с тобой.", 16, Mint, 20, 258, 422, 54);
            note.alignment = TextAnchor.MiddleLeft;
            shopWallet = Caption(shopCard, "", 18, Amber, 28, 452, 800, 28, true);
            descend = Action(shopCard, "Вернуться к месту копания", Mint, game.Descend);
            var close = Action(shopCard, "Во двор", Inset, game.CloseShop, Cream);
            close.name = "Close shop";
            soundButton = Action(shopCard, "", Inset, game.ToggleSound, Cream);
            soundText = soundButton.GetComponentInChildren<Text>();
            shop.SetActive(false);
        }

        private void BuildDialogs()
        {
            confirmation = Backdrop("Confirm return");
            var card = Panel("Return card", confirmation.transform, Ink);
            Center(card, 0, 0, 480, 268);
            Caption(card, "Вернуться на поверхность?", 25, Cream, 28, 24, 424, 45, true);
            Caption(card, "Находки продадутся автоматически.\nТы сможешь спуститься обратно.", 18, Muted, 28, 82, 424, 68);
            var yes = Action(card, "Наверх", Amber, () => { confirmation.SetActive(false); game.ReturnToSurface(); });
            At((RectTransform)yes.transform, 28, 180, 202, 60);
            var no = Action(card, "Остаться", Inset, () => { confirmation.SetActive(false); game.Engage(); }, Cream);
            At((RectTransform)no.transform, 250, 180, 202, 60);
            confirmation.SetActive(false);

            ending = Backdrop("Ending");
            var endCard = Panel("Ending card", ending.transform, Ink);
            Center(endCard, 0, 0, 480, 460);
            Icon(endCard, UiGlyph.Kind.Key, Mint, 212, 24, 56);
            Caption(endCard, "ЗАПЕЧАТАННАЯ ДВЕРЬ", 27, Cream, 24, 94, 432, 44, true).alignment = TextAnchor.MiddleCenter;
            endingText = Caption(endCard, "", 18, Muted, 28, 151, 424, 203);
            var stay = Action(endCard, "Осмотреться", Inset, () => { ending.SetActive(false); game.Engage(); }, Cream);
            At((RectTransform)stay.transform, 28, 372, 202, 60);
            var up = Action(endCard, "Наверх", Amber, () => { ending.SetActive(false); game.ReturnToSurface(); });
            At((RectTransform)up.transform, 250, 372, 202, 60);
            ending.SetActive(false);
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

            float actionHeight = touch ? (portrait ? 76 : 88) : 56;
            Right(actions, edge, portrait ? 212 : 124, portrait ? 246 : 240, actionHeight);
            At((RectTransform)returnButton.transform, 0, 0, portrait ? 118 : 112, actionHeight);
            At((RectTransform)shopButton.transform, portrait ? 128 : 124, 0, portrait ? 118 : 116, actionHeight);

            BottomLeft(toolCard, 24, 24, 360, 60);
            BottomCenter(hint.rectTransform, touch ? 206 : 96, portrait ? 492 : 700, 48);
            Center(targetCard, 0, -54, 384, 42);
            Center(crosshair.rectTransform, 0, 0, 7, 7);
            CenterTop(toastCard, portrait ? 300 : 194, portrait ? 504 : 500, 68);
            CenterTop(announceCard, portrait ? 300 : 170, 470, 118);
            BottomLeft(pad, 24, 28, 150, 150);
            BottomRight(digRect, 24, 28, 156, 132);
            if (portrait) BottomRight(jumpRect, 24, 174, 156, 64);
            else BottomRight(jumpRect, 196, 28, 124, 88);

            Center(shopCard, 0, 0, portrait ? 512 : 860, portrait ? 858 : wideTouch ? 640 : 580);
            At(shopLeft, portrait ? 20 : 24, 110, portrait ? 472 : 318, portrait ? 285 : 318);
            At(shopRight, portrait ? 20 : 366, portrait ? 410 : 110, portrait ? 472 : 470, 318);
            // Equipment panel becomes wide in portrait, keeping the primary action at full width.
            At((RectTransform)upgrade.transform, 20, portrait ? 210 : 224, portrait ? 432 : 278, wideTouch ? 88 : 60);
            At(upgradeInfo.rectTransform, 20, 158, portrait ? 432 : 278, 45);
            At(shopWallet.rectTransform, 28, portrait ? 738 : 447, portrait ? 456 : 804, 32);
            float footer = portrait ? 786 : wideTouch ? 516 : 500, buttonHeight = wideTouch ? 88 : 64;
            At((RectTransform)descend.transform, 28, footer, portrait ? 220 : 358, buttonHeight);
            At((RectTransform)shopCard.Find("Close shop"), portrait ? 260 : 400, footer, portrait ? 104 : 204, buttonHeight);
            At((RectTransform)soundButton.transform, portrait ? 376 : 618, footer, portrait ? 108 : 214, buttonHeight);
            soundText.fontSize = portrait ? 15 : 18;

            At((RectTransform)startCard.GetComponentInChildren<Button>().transform, 32, 244, 416, wideTouch ? 88 : 60);
            At(startHelp.rectTransform, 32, wideTouch ? 344 : 324, 416, 82);
            foreach (var dialog in new[] { confirmation, ending })
            {
                var card = (RectTransform)dialog.transform.GetChild(0);
                card.sizeDelta = new Vector2(480, dialog == confirmation ? (wideTouch ? 296 : 268) : (wideTouch ? 488 : 460));
                foreach (var button in card.GetComponentsInChildren<Button>(true))
                {
                    var rect = (RectTransform)button.transform;
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, wideTouch ? 88 : 60);
                }
            }
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
            coins.text = Mathf.RoundToInt(shownCoins).ToString("N0");
            coins.color = Time.unscaledTime < pulseUntil ? Amber : Cream;
            depth.text = metres + " м";
            zone.text = config.zones[zoneIndex].nameRu.ToUpperInvariant();
            bool lastZone = zoneIndex + 1 >= config.zones.Length;
            int next = lastZone ? config.depth : config.zones[zoneIndex + 1].startDepth;
            record.text = "Рекорд " + progress.maxDepth + " м  ·  " + (lastZone ? "дверь" : "зона «" + config.zones[zoneIndex + 1].nameRu + "»") + " " + next + " м";
            depthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(metres / (float)config.depth), 1);
            for (int i = 0; i < zoneTicks.Count; i++)
                zoneTicks[i].color = progress.maxDepth >= config.zones[i + 1].startDepth ? Mint : Cream;
            backpack.text = progress.backpack + " " + MineGame.Plural(progress.backpack, "монета", "монеты", "монет");
            bagCaption.text = progress.backpack > 0 ? "ПРОДАДИМ НАВЕРХУ" : "РЮКЗАК";
            toolName.text = game.Tool.nameRu;
            toolKeys.text = game.FreeMouse ? "ЛКМ копать  ·  ПКМ обзор  ·  WASD идти  ·  R наверх"
                : "ЛКМ копать  ·  ПРОБЕЛ прыжок  ·  WASD идти  ·  R наверх";
            startHelp.text = game.FreeMouse ? "ЛКМ — копать · ПКМ — осмотреться\nWASD — идти · R — наверх"
                : "Мышь — обзор · ЛКМ — копать\nWASD — идти · R — наверх";

            bool modal = PanelOpen, waiting = game.NeedsClick;
            overlay.SetActive(waiting);
            coinCard.gameObject.SetActive(!modal);
            depthCard.gameObject.SetActive(!modal);
            bagCard.gameObject.SetActive(!modal);
            actions.gameObject.SetActive(!modal && !waiting);
            shopButton.gameObject.SetActive(game.NearShop);
            touchControls.gameObject.SetActive(game.TouchMode && !modal);
            toolCard.gameObject.SetActive(!game.TouchMode && !modal && !waiting);
            targetCard.gameObject.SetActive(game.Active && !string.IsNullOrEmpty(game.TargetText));
            target.text = game.TargetText;
            crosshair.enabled = game.Active;
            bool hit = Time.unscaledTime < hitUntil;
            crosshair.color = hit ? Amber : game.InReach ? Mint : Cream;
            crosshair.rectTransform.sizeDelta = Vector2.one * (hit ? 11 : 7);
            hint.text = modal || waiting ? "" : game.Hint;
            UpdateAnnouncement(modal || waiting);
            toastCard.gameObject.SetActive(Time.unscaledTime < toastUntil && !modal && !announceCard.gameObject.activeSelf);
            vignette.color = new Color(0, 0, 0, modal || waiting ? 0.35f : Mathf.Lerp(0.3f, 0.82f, Mathf.Clamp01(metres / 12f)));
            UpdatePopups();
            if (shop.activeSelf) UpdateShop();
        }

        private void UpdateShop()
        {
            var progress = game.Progress;
            var config = game.config;
            int next = progress.tool + 1;
            shopInfo.text = game.Tool.nameRu + "\n<size=15>Сила копания: " + game.Tool.damage + "</size>";
            albumTitle.text = "ТВОЯ КОЛЛЕКЦИЯ  ·  " + progress.CollectionCount + " / " + config.collection.Length;
            for (int i = 0; i < albumTiles.Count; i++)
            {
                bool found = progress.HasCollectible(i);
                albumIcons[i].kind = found ? (UiGlyph.Kind)((int)UiGlyph.Kind.Helmet + i) : UiGlyph.Kind.Lock;
                albumIcons[i].color = found ? config.collection[i].color : Muted;
                albumTiles[i].color = found ? new Color(0.17f, 0.28f, 0.27f) : Inset;
                // A rough depth turns a locked slot into a goal.
                albumNames[i].text = found ? config.collection[i].nameRu : "где-то\nна " + RoundDepth(config.collection[i].depth) + " м";
            }
            if (next < config.tools.Length)
            {
                var tool = config.tools[next];
                int missing = Mathf.Max(0, tool.price - progress.coins);
                upgradeInfo.text = tool.nameRu + " · сила " + tool.damage + "\n" + (missing > 0 ? "Ещё " + missing + " монет до покупки" : "Можно купить прямо сейчас");
                upgradeText.text = "Улучшить  ·  " + tool.price + " монет";
                upgrade.interactable = missing == 0;
            }
            else
            {
                upgradeInfo.text = "В руках лучшая лопата.";
                upgradeText.text = "Максимальный уровень";
                upgrade.interactable = false;
            }
            shopWallet.text = Time.unscaledTime < toastUntil ? toast.text : "В кошельке: " + progress.coins + " монет";
            descend.interactable = progress.hasDive;
            soundText.text = progress.muted ? "Звук: выкл" : "Звук: вкл";
        }

        private static int RoundDepth(int metres) => metres < 10 ? metres : Mathf.RoundToInt(metres / 5f) * 5;

        public void ShowShop(bool show)
        {
            shop.SetActive(show);
            if (!show) return;
            ending.SetActive(false);
            confirmation.SetActive(false);
            ClearInput();
        }

        public void ClosePanel()
        {
            shop.SetActive(false);
            confirmation.SetActive(false);
            ending.SetActive(false);
            ClearInput();
        }

        public void ShowReturnConfirmation()
        {
            ClearInput();
            confirmation.SetActive(true);
        }

        public void ShowEnding()
        {
            var progress = game.Progress;
            bool key = progress.HasCollectible(game.config.collection.Length - 1);
            endingText.text = "На глубине " + game.config.depth + " м тебя ждала древняя дверь. За камнем слышится тихий гул.\n\n" +
                (key ? "Найденный ключ откроет путь в следующем обновлении." : "Может быть, ключ ещё спрятан в шахте?") +
                "\n\nКоллекция: " + progress.CollectionCount + " / " + game.config.collection.Length;
            ClearInput();
            ending.SetActive(true);
        }

        public void ClearInput() { Stick.ResetInput(); Dig.ResetInput(); Look.ResetInput(); jumpQueued = false; }
        public bool ConsumeJump() { bool value = jumpQueued; jumpQueued = false; return value; }
        public void Notify(string message) { toast.text = message; toastUntil = Time.unscaledTime + 3.6f; }
        public void HitFeedback() => hitUntil = Time.unscaledTime + 0.13f;

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
            var label = Caption(rect, text, 18, foreground ?? Ink, 0, 0, 1, 1, true);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(10, 6);
            label.rectTransform.offsetMax = new Vector2(-10, -6);
            label.alignment = TextAnchor.MiddleCenter;
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

        /// <summary>Anti-aliased rounded square (radius &gt; 0, sliced) or circle (radius 0) generated at runtime.</summary>
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
