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
        private static readonly Color Ink = new Color(.065f,.105f,.12f,.98f);
        private static readonly Color Card = new Color(.08f,.135f,.15f,.96f);
        private static readonly Color Inset = new Color(.12f,.19f,.20f,1);
        private static readonly Color Cream = new Color(.97f,.95f,.87f);
        private static readonly Color Muted = new Color(.69f,.78f,.77f);
        private static readonly Color Amber = new Color(1,.76f,.31f);
        private static readonly Color Mint = new Color(.43f,.86f,.72f);
        private MineGame game;
        private Font font;
        private Sprite rounded;
        private Texture2D roundTexture;
        private CanvasScaler scaler;
        private RectTransform root, coinCard, depthCard, bagCard, actions, toolCard, targetCard, toastCard;
        private RectTransform shopCard, shopLeft, shopRight, startCard, touchControls, pad, digRect, jumpRect;
        private GameObject overlay, shop, confirmation, ending;
        private Button shopButton, returnButton, upgrade, descend, soundButton;
        private Text coins, depth, zone, backpack, target, hint, toast, toolName, keyHints;
        private Text shopInfo, upgradeText, upgradeInfo, albumTitle, soundText, endingText, startHelp;
        private Text shopWallet, bagCaption, record;
        private Image depthFill, crosshair;
        private readonly List<Image> albumTiles = new List<Image>();
        private readonly List<UiGlyph> albumIcons = new List<UiGlyph>();
        private readonly List<Text> albumNames = new List<Text>();
        private readonly List<FloatingText> popups = new List<FloatingText>();
        private bool jumpQueued, portrait;
        private int lastWidth, lastHeight;
        private bool lastTouch;
        private float toastUntil, hitUntil, pulseUntil, shownCoins;
        private int lastCoins = -1;
        private sealed class FloatingText { public Text label; public Vector3 world; public float age = 1; }

        public void Setup(MineGame owner)
        {
            game = owner;
            font = Resources.Load<Font>("Fonts/NotoSans");
            CreateRoundedSprite();
            var canvas = new GameObject("Game UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = Rect("Safe area", canvas.transform);
            Stretch(root);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            BuildTouch();
            coinCard = Panel("Wallet", root, Card);
            Icon(coinCard, UiGlyph.Kind.Coin, Amber, 18, 22, 38);
            Caption(coinCard, "МОНЕТЫ", 15, Muted, 70, 10, 150, 22);
            coins = Caption(coinCard, "", 28, Cream, 70, 31, 150, 38, true);
            depthCard = Panel("Depth", root, Card);
            Icon(depthCard, UiGlyph.Kind.Down, Mint, 15, 17, 32);
            depth = Caption(depthCard, "", 24, Cream, 54, 5, 110, 40, true);
            zone = Caption(depthCard, "", 15, Muted, 170, 13, 140, 25);
            var track = Panel("Depth track", depthCard, Inset);
            At(track, 18, 52, 284, 6);
            depthFill = Panel("Depth progress", track, Mint).GetComponent<Image>();
            Stretch(depthFill.rectTransform);
            record = Caption(depthCard, "", 12, Muted, 18, 62, 284, 20);
            bagCard = Panel("Backpack", root, Card);
            Icon(bagCard, UiGlyph.Kind.Bag, Mint, 18, 22, 36);
            bagCaption = Caption(bagCard, "РЮКЗАК", 15, Muted, 70, 10, 148, 22);
            backpack = Caption(bagCard, "", 25, Cream, 70, 33, 156, 35, true);
            actions = Rect("Actions", root);
            returnButton = Action(actions, "Наверх", Amber, game.RequestReturn);
            shopButton = Action(actions, "Лавка", Inset, game.OpenShop, Cream);
            toolCard = Panel("Tool card", root, Card);
            Icon(toolCard, UiGlyph.Kind.Shovel, Amber, 12, 14, 34);
            toolName = Caption(toolCard, "", 18, Cream, 58, 7, 225, 26, true);
            Caption(toolCard, "ЛКМ  /  УДЕРЖИВАЙ, ЧТОБЫ КОПАТЬ", 11, Muted, 58, 34, 225, 19);
            keyHints = Caption(root, "", 14, Cream, 0, 0, 500, 28);
            keyHints.alignment = TextAnchor.MiddleRight;
            targetCard = Panel("Target", root, Card);
            target = Caption(targetCard, "", 17, Cream, 12, 4, 360, 34);
            target.alignment = TextAnchor.MiddleCenter;
            crosshair = Panel("Crosshair", root, Cream).GetComponent<Image>();
            crosshair.raycastTarget = false;
            var outline = crosshair.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0,0,0,.65f); outline.effectDistance = new Vector2(1,-1);
            hint = Caption(root, "", 16, Cream, 0, 0, 600, 46);
            hint.alignment = TextAnchor.MiddleCenter;
            hint.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1,-1);
            BuildStart();
            BuildShop();
            BuildDialogs();
            // Toasts sit above modals: automatic sale feedback stays readable when the shop opens.
            toastCard = Panel("Notification", root, Ink);
            Icon(toastCard, UiGlyph.Kind.Coin, Amber, 14, 16, 28);
            toast = Caption(toastCard, "", 18, Cream, 52, 9, 422, 48);
            toast.alignment = TextAnchor.MiddleLeft;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                image.raycastTarget = image.GetComponent<Button>() != null || image.GetComponent<TouchStick>() != null ||
                    image.GetComponent<TouchLook>() != null || image.gameObject == overlay || image.gameObject == shop ||
                    image.gameObject == confirmation || image.gameObject == ending;
            Layout();
        }

        private void BuildTouch()
        {
            touchControls = Rect("Touch controls", root); Stretch(touchControls);
            var look = Panel("Look area", touchControls, Color.clear, false); Stretch(look);
            look.anchorMin = new Vector2(.34f,0);
            Look = look.gameObject.AddComponent<TouchLook>();
            pad = Panel("Movement stick", touchControls, Card);
            Stick = pad.gameObject.AddComponent<TouchStick>();
            Stick.knob = Panel("Stick knob", pad, Mint);
            Center(Stick.knob, 0, 0, 52, 52);
            Stick.knob.GetComponent<Image>().raycastTarget = false;
            var dig = Action(touchControls, "КОПАТЬ", Amber, null);
            digRect = (RectTransform)dig.transform;
            Dig = dig.gameObject.AddComponent<HoldButton>();
            var jump = Action(touchControls, "ПРЫЖОК", Inset, () => jumpQueued = true, Cream);
            jumpRect = (RectTransform)jump.transform;
        }

        private void BuildStart()
        {
            overlay = Scrim("Start overlay");
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
            shop = Scrim("Shop");
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
            At((RectTransform)upgrade.transform,20,224,278,60);
            upgradeText = upgrade.GetComponentInChildren<Text>();
            albumTitle = Caption(shopRight, "", 20, Cream, 20, 14, 422, 30, true);
            Caption(shopRight, "Истории, спрятанные под землёй", 15, Muted, 20, 50, 422, 26);
            for (int i=0;i<game.config.collection.Length;i++)
            {
                var tile = Panel("Collection item " + i, shopRight, Inset);
                At(tile, 20+i*86, 94, 76, 86);
                albumTiles.Add(tile.GetComponent<Image>());
                albumIcons.Add(Icon(tile, (UiGlyph.Kind)((int)UiGlyph.Kind.Helmet+i), Muted, 16, 19, 44));
                var name = Caption(shopRight,"",12,Muted,16+i*86,188,84,54);
                name.alignment = TextAnchor.UpperCenter; albumNames.Add(name);
            }
            var note = Caption(shopRight, "Находки продаются при выходе из шахты.\nКоллекция остаётся с тобой.", 16, Mint, 20, 258, 422, 54);
            note.alignment = TextAnchor.MiddleLeft;
            shopWallet = Caption(shopCard, "", 18, Amber, 28, 452, 800, 28, true);
            descend = Action(shopCard,"Вернуться к месту копания",Mint,game.Descend);
            var close = Action(shopCard,"Во двор",Inset,game.CloseShop,Cream); close.name="Close shop";
            soundButton = Action(shopCard,"",Inset,game.ToggleSound,Cream); soundText = soundButton.GetComponentInChildren<Text>();
            shop.SetActive(false);
        }

        private void BuildDialogs()
        {
            confirmation = Scrim("Confirm return");
            var card = Panel("Return card", confirmation.transform, Ink); Center(card,0,0,480,268);
            Caption(card,"Вернуться на поверхность?",25,Cream,28,24,424,45,true);
            Caption(card,"Находки продадутся автоматически.\nТы сможешь спуститься обратно.",18,Muted,28,82,424,68);
            var yes=Action(card,"Наверх",Amber,()=>{confirmation.SetActive(false);game.ReturnToSurface();});
            At((RectTransform)yes.transform,28,180,202,60);
            var no=Action(card,"Остаться",Inset,()=>{confirmation.SetActive(false);game.Engage();},Cream);
            At((RectTransform)no.transform,250,180,202,60); confirmation.SetActive(false);
            ending=Scrim("Ending");
            var endCard=Panel("Ending card",ending.transform,Ink);Center(endCard,0,0,480,460);
            Icon(endCard,UiGlyph.Kind.Key,Mint,212,24,56);
            Caption(endCard,"ЗАПЕЧАТАННАЯ ДВЕРЬ",27,Cream,24,94,432,44,true).alignment=TextAnchor.MiddleCenter;
            endingText=Caption(endCard,"",18,Muted,28,151,424,203);
            var stay=Action(endCard,"Осмотреться",Inset,()=>{ending.SetActive(false);game.Engage();},Cream);
            At((RectTransform)stay.transform,28,372,202,60);
            var up=Action(endCard,"Наверх",Amber,()=>{ending.SetActive(false);game.ReturnToSurface();});
            At((RectTransform)up.transform,250,372,202,60); ending.SetActive(false);
        }

        private void Layout()
        {
            lastWidth=Screen.width;lastHeight=Screen.height;lastTouch=game.TouchMode;
            portrait=Screen.width<Screen.height;
            scaler.referenceResolution=portrait?new Vector2(540,960):new Vector2(1280,720);
            At(coinCard,portrait?14:24,portrait?14:24,portrait?248:232,84);
            Right(bagCard,portrait?14:24,portrait?14:24,portrait?248:240,84);
            CenterTop(depthCard,portrait?112:24,320,86);
            if(portrait) { At(depthCard,14,112,512,86); At(zone.rectTransform,330,13,160,25); }
            else At(zone.rectTransform,170,13,140,25);
            var track=depthFill.rectTransform.parent as RectTransform;
            At(track,18,52,portrait?476:284,6);
            At(record.rectTransform,18,62,portrait?476:284,20);
            float actionHeight=game.TouchMode?(portrait?76:88):56;
            Right(actions,portrait?14:24,portrait?212:124,portrait?246:240,actionHeight);
            At((RectTransform)returnButton.transform,0,0,portrait?118:112,actionHeight);
            At((RectTransform)shopButton.transform,portrait?128:124,0,portrait?118:116,actionHeight);
            BottomLeft(toolCard,24,24,300,64);
            BottomRight(keyHints.rectTransform,24,30,530,30);
            BottomCenter(hint.rectTransform,game.TouchMode?206:100,portrait?492:700,48);
            Center(targetCard,0,-54,384,42);
            Center(crosshair.rectTransform,0,0,6,6);
            CenterTop(toastCard,portrait?300:194,portrait?504:500,68);
            BottomLeft(pad,24,28,146,146);
            BottomRight(digRect,24,28,156,132);
            BottomRight(jumpRect,24,174,156,64);
            if(!portrait) BottomRight(jumpRect,196,28,124,88);
            bool wideTouch=game.TouchMode&&!portrait;
            Center(shopCard,0,0,portrait?512:860,portrait?858:wideTouch?640:580);
            At(shopLeft,portrait?20:24,110,portrait?472:318,portrait?285:318);
            At(shopRight,portrait?20:366,portrait?410:110,portrait?472:470,318);
            // Equipment panel becomes wide in portrait, keeping the primary action at full width.
            At((RectTransform)upgrade.transform,20,portrait?210:224,portrait?432:278,wideTouch?88:60);
            At(upgradeInfo.rectTransform,20,158,portrait?432:278,45);
            At(shopWallet.rectTransform,28,portrait?738:447,portrait?456:804,32);
            float footer=portrait?786:wideTouch?516:500;
            At((RectTransform)descend.transform,28,footer,portrait?220:358,wideTouch?88:64);
            At((RectTransform)shopCard.Find("Close shop"),portrait?260:400,footer,portrait?104:204,wideTouch?88:64);
            At((RectTransform)soundButton.transform,portrait?376:618,footer,portrait?108:214,wideTouch?88:64);
            var startAction=startCard.GetComponentInChildren<Button>();
            At((RectTransform)startAction.transform,32,244,416,wideTouch?88:60);
            At(startHelp.rectTransform,32,wideTouch?344:324,416,82);
            foreach(var dialog in new[]{confirmation,ending})
            {
                var card=(RectTransform)dialog.transform.GetChild(0);
                card.sizeDelta=new Vector2(480,dialog==confirmation?(wideTouch?296:268):(wideTouch?488:460));
                foreach(var button in card.GetComponentsInChildren<Button>(true))
                {
                    var rect=(RectTransform)button.transform;
                    rect.sizeDelta=new Vector2(rect.sizeDelta.x,wideTouch?88:60);
                }
            }
            soundText.fontSize=portrait?15:18;
        }

        private void Update()
        {
            if(game==null || game.Progress==null)return;
            var safe=Screen.safeArea;
            root.anchorMin=new Vector2(safe.xMin/Screen.width,safe.yMin/Screen.height);
            root.anchorMax=new Vector2(safe.xMax/Screen.width,safe.yMax/Screen.height);
            if(lastWidth!=Screen.width || lastHeight!=Screen.height || lastTouch!=game.TouchMode)Layout();
            var p=game.Progress;var c=game.config;int z=c.ZoneIndex(game.Depth);
            if(lastCoins!=p.coins) { if(lastCoins>=0)pulseUntil=Time.unscaledTime+.35f;else shownCoins=p.coins;lastCoins=p.coins; }
            shownCoins=Mathf.MoveTowards(shownCoins,p.coins,Mathf.Max(30,Mathf.Abs(p.coins-shownCoins)*10)*Time.unscaledDeltaTime);
            coins.text=Mathf.RoundToInt(shownCoins).ToString("N0");
            coins.color=Time.unscaledTime<pulseUntil?Amber:Cream;
            depth.text=game.Depth+" м";zone.text=c.zones[z].nameRu.ToUpperInvariant();
            int next=z+1<c.zones.Length?c.zones[z+1].startDepth:c.depth;
            record.text="Рекорд "+p.maxDepth+" м  ·  "+(z+1<c.zones.Length?"следующая зона":"дверь")+" "+next+" м";
            depthFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(game.Depth/(float)c.depth),1);
            backpack.text=p.backpack+" монет";
            bagCaption.text=p.backpack>0?"ПРОДАДИМ НАВЕРХУ":"РЮКЗАК";
            toolName.text=game.Tool.nameRu;
            bool modal=PanelOpen;
            overlay.SetActive(game.NeedsClick);
            actions.gameObject.SetActive(!modal&&!game.NeedsClick);
            shopButton.gameObject.SetActive(game.NearShop);
            touchControls.gameObject.SetActive(game.TouchMode&&!modal);
            toolCard.gameObject.SetActive(!game.TouchMode&&!modal&&!game.NeedsClick);
            keyHints.gameObject.SetActive(!game.TouchMode&&!modal&&!game.NeedsClick);
            keyHints.text=game.FreeMouse?"ПКМ  обзор     WASD  идти     R  наверх":"WASD  идти     ПРОБЕЛ  прыжок     R  наверх";
            startHelp.text=game.FreeMouse?"ЛКМ — копать · ПКМ — осмотреться\nWASD — идти · R — наверх":"Мышь — обзор · ЛКМ — копать\nWASD — идти · R — наверх";
            targetCard.gameObject.SetActive(game.Active&&!string.IsNullOrEmpty(game.TargetText));
            target.text=game.TargetText;crosshair.enabled=game.Active;
            crosshair.color=Time.unscaledTime<hitUntil?Amber:game.InReach?Mint:Cream;
            float size=Time.unscaledTime<hitUntil?9:6;crosshair.rectTransform.sizeDelta=Vector2.one*size;
            hint.text=modal||game.NeedsClick?"":game.Hint;
            toastCard.gameObject.SetActive(Time.unscaledTime<toastUntil&&!modal);
            UpdatePopups();
            if(shop.activeSelf)UpdateShop();
        }

        private void UpdateShop()
        {
            var p=game.Progress;var c=game.config;int next=p.tool+1;
            shopInfo.text=game.Tool.nameRu+"\n<size=15>Сила копания: "+game.Tool.damage+"</size>";
            albumTitle.text="ТВОЯ КОЛЛЕКЦИЯ  ·  "+p.CollectionCount+" / "+c.collection.Length;
            for(int i=0;i<albumTiles.Count;i++)
            {
                bool found=p.HasCollectible(i);
                albumIcons[i].kind=found?(UiGlyph.Kind)((int)UiGlyph.Kind.Helmet+i):UiGlyph.Kind.Lock;
                albumIcons[i].color=found?c.collection[i].color:Muted;
                albumTiles[i].color=found?new Color(.17f,.28f,.27f):Inset;
                albumNames[i].text=found?c.collection[i].nameRu:"Не найдено";
            }
            if(next<c.tools.Length)
            {
                var t=c.tools[next];int missing=Mathf.Max(0,t.price-p.coins);
                upgradeInfo.text=t.nameRu+" · сила "+t.damage+"\n"+(missing>0?"Ещё "+missing+" монет до покупки":"Можно купить прямо сейчас");
                upgradeText.text="Улучшить  ·  "+t.price+" монет";upgrade.interactable=missing==0;
            }
            else { upgradeInfo.text="В руках лучшая лопата.";upgradeText.text="Максимальный уровень";upgrade.interactable=false; }
            shopWallet.text=Time.unscaledTime<toastUntil?toast.text:"В кошельке: "+p.coins+" монет";
            descend.interactable=p.hasDive;
            soundText.text=p.muted?"Звук: выкл":"Звук: вкл";
        }

        public void ShowShop(bool show)
        {
            shop.SetActive(show);
            if(show) { ending.SetActive(false); confirmation.SetActive(false); ClearInput(); }
        }
        public void ClosePanel() { shop.SetActive(false); confirmation.SetActive(false); ending.SetActive(false); ClearInput(); }
        public void ShowReturnConfirmation(){ClearInput();confirmation.SetActive(true);}
        public void ShowEnding()
        {
            endingText.text="На глубине "+game.config.depth+" м тебя ждала древняя дверь. За камнем слышится тихий гул.\n\n"+
                (game.Progress.HasCollectible(game.config.collection.Length-1)?"Найденный ключ откроет путь в следующем обновлении.":"Может быть, ключ ещё спрятан в шахте?")+"\n\nКоллекция: "+game.Progress.CollectionCount+" / "+game.config.collection.Length;
            ClearInput();ending.SetActive(true);
        }
        public void ClearInput(){Stick.ResetInput();Dig.ResetInput();Look.ResetInput();jumpQueued=false;}
        public bool ConsumeJump(){bool value=jumpQueued;jumpQueued=false;return value;}
        public void Notify(string message){toast.text=message;toastUntil=Time.unscaledTime+3.6f;}
        public void HitFeedback(){hitUntil=Time.unscaledTime+.13f;}
        public void Popup(Vector3 world,string text,Color color)
        {
            FloatingText p=null;foreach(var candidate in popups)if(candidate.age>=1){p=candidate;break;}
            if(p==null)
            {
                if(popups.Count>=12)return;
                p=new FloatingText{label=Caption(root,"",22,color,0,0,350,48,true)};
                p.label.alignment=TextAnchor.MiddleCenter;p.label.gameObject.AddComponent<Shadow>().effectDistance=new Vector2(1,-2);popups.Add(p);
            }
            p.label.text=text;p.label.color=color;p.world=world;p.age=0;p.label.enabled=true;
        }
        private void UpdatePopups()
        {
            var camera=Camera.main;
            foreach(var p in popups)
            {
                if(p.age>=1){p.label.enabled=false;continue;}
                p.age+=Time.unscaledDeltaTime/1.05f;
                var screen=camera!=null?camera.WorldToScreenPoint(p.world):Vector3.back;
                if(screen.z<=0||PanelOpen){p.label.enabled=false;continue;}
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root,screen,null,out var local);
                Center(p.label.rectTransform,local.x,local.y+34+p.age*54,350,48);
                var color=p.label.color;color.a=Mathf.Clamp01((1-p.age)*3);p.label.color=color;p.label.enabled=true;
            }
        }

        private GameObject Scrim(string name)
        {
            var rect=Panel(name,root,new Color(.025f,.055f,.065f,.7f),false);Stretch(rect);return rect.gameObject;
        }
        private RectTransform Rect(string name,Transform parent)
        {
            var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);return (RectTransform)obj.transform;
        }
        private RectTransform Panel(string name,Transform parent,Color color,bool round=true)
        {
            var rect=Rect(name,parent);var image=rect.gameObject.AddComponent<Image>();image.color=color;
            if(round){image.sprite=rounded;image.type=Image.Type.Sliced;}
            return rect;
        }
        private Text Caption(Transform parent,string text,int size,Color color,float x,float y,float w,float h,bool bold=false)
        {
            var rect=Rect("Label",parent);At(rect,x,y,w,h);var label=rect.gameObject.AddComponent<Text>();
            label.font=font;label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAnchor.MiddleLeft;
            label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;label.raycastTarget=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
            return label;
        }
        private UiGlyph Icon(Transform parent,UiGlyph.Kind kind,Color color,float x,float y,float size)
        {
            var rect=Rect("Icon "+kind,parent);At(rect,x,y,size,size);var icon=rect.gameObject.AddComponent<UiGlyph>();
            icon.kind=kind;icon.color=color;icon.raycastTarget=false;return icon;
        }
        private Button Action(Transform parent,string text,Color color,UnityEngine.Events.UnityAction action,Color? foreground=null)
        {
            var rect=Panel(text,parent,color);var b=rect.gameObject.AddComponent<Button>();b.targetGraphic=rect.GetComponent<Image>();
            var colors=b.colors;colors.highlightedColor=new Color(1.15f,1.15f,1.15f);colors.selectedColor=colors.highlightedColor;
            colors.pressedColor=new Color(.76f,.81f,.79f);colors.disabledColor=new Color(.64f,.68f,.66f,.9f);colors.fadeDuration=.12f;b.colors=colors;
            if(action!=null)b.onClick.AddListener(action);
            var label=Caption(rect,text,18,foreground??Ink,0,0,1,1,true);
            Stretch(label.rectTransform);label.rectTransform.offsetMin=new Vector2(10,6);label.rectTransform.offsetMax=new Vector2(-10,-6);
            label.alignment=TextAnchor.MiddleCenter;return b;
        }
        private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        private static void At(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        private static void Right(RectTransform r,float x,float y,float w,float h){At(r,0,y,w,h);r.anchorMin=r.anchorMax=Vector2.one;r.pivot=new Vector2(1,1);r.anchoredPosition=new Vector2(-x,-y);}
        private static void CenterTop(RectTransform r,float y,float w,float h){At(r,0,y,w,h);r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);}
        private static void Center(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
        private static void BottomLeft(RectTransform r,float x,float y,float w,float h){At(r,x,0,w,h);r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;r.anchoredPosition=new Vector2(x,y);}
        private static void BottomRight(RectTransform r,float x,float y,float w,float h){BottomLeft(r,0,y,w,h);r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,0);r.anchoredPosition=new Vector2(-x,y);}
        private static void BottomCenter(RectTransform r,float y,float w,float h){BottomLeft(r,0,y,w,h);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,0);}
        private void CreateRoundedSprite()
        {
            const int size=32;
            roundTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="UI rounded corners",filterMode=FilterMode.Bilinear};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var d=new Vector2(Mathf.Max(0,Mathf.Abs(x-15.5f)-7.5f),Mathf.Max(0,Mathf.Abs(y-15.5f)-7.5f));
                roundTexture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(8.5f-d.magnitude)));
            }
            roundTexture.Apply(false,true);
            rounded=Sprite.Create(roundTexture,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,Vector4.one*10);
        }
        private void OnDestroy(){if(rounded)Destroy(rounded);if(roundTexture)Destroy(roundTexture);}
    }
}
