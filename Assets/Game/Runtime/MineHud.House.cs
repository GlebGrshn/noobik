using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nubik
{
    /// <summary>The house window: sell ore, upgrade gear, buy items that ride in the backpack, see the collection.</summary>
    public sealed partial class MineHud
    {
        private static readonly string[] TabNames = { "Скупка", "Заказы", "Улучшения", "Предметы", "Дневник" };
        public const int SellPage = 0, OrderPage = 1, UpgradePage = 2, ItemPage = 3, JournalPage = 4;
        private GameObject house;
        private RectTransform houseCard, houseTitle, houseSub;
        private Text houseWallet, houseFooter;
        private Button closeButton, soundButton;
        private Text soundText;
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<RectTransform> pages = new List<RectTransform>();
        private int tab;
        private bool jumpWasHeld;

        private Text sellSummary, sellText, adText;
        private Button sellButton, adButton;
        private RectTransform orderBox;
        private UiGlyph orderBig;
        private Text orderHead, orderHave, orderPay, orderNote, deliverText;
        private Image orderFill;
        private Button deliverButton, skipButton;
        private Text secretsText;
        private readonly List<OreLine> oreLines = new List<OreLine>();
        private readonly List<ShopRow> upgradeRows = new List<ShopRow>();
        private readonly List<ShopRow> itemRows = new List<ShopRow>();
        private Text itemsNote, albumTitle, albumNote;
        private Text sitesTitle;
        private readonly List<Text> siteNotes = new List<Text>();
        private readonly List<Image> albumTiles = new List<Image>();
        private readonly List<UiGlyph> albumIcons = new List<UiGlyph>();
        private readonly List<Text> albumNames = new List<Text>();
        private static readonly Track[] Tracks = { Track.Tool, Track.Backpack, Track.Fuel, Track.Jetpack, Track.Health };
        private static readonly string[] TrackNames = { "Инструмент", "Рюкзак", "Бензобак", "Джетпак", "Здоровье" };

        private sealed class OreLine { public RectTransform rect; public UiGlyph icon; public Text name, amount; }
        private sealed class ShopRow { public RectTransform rect; public UiGlyph icon; public Text title, info, buttonText; public Button button, free; }

        private void BuildHouse()
        {
            house = Backdrop("House");
            houseCard = Panel("House card", house.transform, Ink);
            houseTitle = Caption(houseCard, "ДОМ", 30, Cream, 28, 16, 300, 42, true).rectTransform;
            houseSub = Caption(houseCard, "Скупка руды и мастерская", 15, Muted, 28, 56, 400, 26).rectTransform;
            houseWallet = Caption(houseCard, "", 24, Amber, 0, 0, 260, 40, true);
            houseWallet.alignment = TextAnchor.MiddleRight;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                var button = Action(houseCard, TabNames[i], Inset, () => SelectTab(index), Cream);
                tabButtons.Add(button);
                pages.Add(Rect("Page " + TabNames[i], houseCard));
            }
            houseFooter = Caption(houseCard, "", 15, Muted, 0, 0, 400, 48);
            soundButton = Action(houseCard, "Настройки", Inset, OpenSettings, Cream);
            soundText = soundButton.GetComponentInChildren<Text>();
            closeButton = Action(houseCard, "Закрыть  ·  Esc", Amber, game.CloseHouse);

            // Sell page.
            var sell = pages[0];
            sellSummary = Caption(sell, "", 16, Muted, 0, 0, 400, 30);
            for (int i = 0; i < game.config.ores.Length; i++)
            {
                var line = Panel("Ore line", sell, Card);
                var ore = game.config.ores[i];
                oreLines.Add(new OreLine
                {
                    rect = line,
                    icon = Icon(line, ore.chest ? UiGlyph.Kind.Bag : UiGlyph.Kind.Gem, ore.color, 12, 9, 26),
                    name = Caption(line, ore.nameRu, 16, Cream, 48, 4, 200, 36, true),
                    amount = Caption(line, "", 16, Amber, 0, 4, 160, 36, true),
                });
                oreLines[i].amount.alignment = TextAnchor.MiddleRight;
            }
            sellButton = Action(sell, "", Amber, game.SellOre);
            sellText = sellButton.GetComponentInChildren<Text>();
            // Voluntary and labelled: the button says it is an ad and what it pays.
            adButton = Action(sell, "", Inset, game.WatchAd, Cream);
            adText = adButton.GetComponentInChildren<Text>();
            adText.alignment = TextAnchor.MiddleLeft;
            adText.rectTransform.offsetMin = new Vector2(52, 6);
            var play = Icon(adButton.transform, UiGlyph.Kind.Play, Amber, 0, 0, 30).rectTransform;
            play.anchorMin = play.anchorMax = play.pivot = new Vector2(0, .5f);
            play.anchoredPosition = new Vector2(14, 0);

            // Buyer's order page.
            var order = pages[OrderPage];
            orderBox = Panel("Order card", order, Card);
            orderBig = Icon(orderBox, UiGlyph.Kind.Gem, Amber, 22, 26, 60);
            orderHead = Caption(orderBox, "", 24, Cream, 100, 16, 600, 36, true);
            orderHave = Caption(orderBox, "", 17, Muted, 100, 54, 600, 28);
            orderPay = Caption(orderBox, "", 17, Amber, 100, 84, 600, 28, true);
            orderFill = Bar(orderBox, 100, 126, 400, Mint);
            foreach (var line in new[] { orderHave, orderPay })
            {
                line.resizeTextForBestFit = true;
                line.resizeTextMinSize = 11;
                line.resizeTextMaxSize = 17;
            }
            deliverButton = Action(order, "", Amber, game.DeliverOrder);
            deliverText = deliverButton.GetComponentInChildren<Text>();
            skipButton = Action(order, "Попросить другой заказ", Inset, game.SkipOrder, Cream);
            orderNote = Caption(order, "", 15, Muted, 0, 0, 600, 80);

            // Upgrades page.
            var icons = new[] { UiGlyph.Kind.Shovel, UiGlyph.Kind.Bag, UiGlyph.Kind.Fuel, UiGlyph.Kind.Jet, UiGlyph.Kind.Heart };
            var colors = new[] { Amber, Mint, Amber, Blue, Red };
            for (int i = 0; i < Tracks.Length; i++)
            {
                var track = Tracks[i];
                var row = Row(pages[UpgradePage], TrackNames[i], icons[i], colors[i], () => game.Buy(track));
                // Testing aid (MineGame.TestUpgrades): the next level for free.
                if (MineGame.TestUpgrades) row.free = Action(row.rect, "Даром · тест", new Color(.3f, .22f, .38f), () => game.BuyFree(track), Cream);
                upgradeRows.Add(row);
            }

            // Items page: they take backpack slots.
            itemRows.Add(Row(pages[ItemPage], game.config.scanner.nameRu, UiGlyph.Kind.Radar, Mint, game.BuyScanner));
            itemRows.Add(Row(pages[ItemPage], game.config.medkit.nameRu, UiGlyph.Kind.Cross, Red, game.BuyMedkit));
            itemRows.Add(Row(pages[ItemPage], game.config.dynamite.nameRu, UiGlyph.Kind.Blast, new Color(.95f, .35f, .25f), game.BuyDynamite));
            itemsNote = Caption(pages[ItemPage], "", 15, Mint, 0, 0, 400, 50);

            // Collection page.
            var album = Rect("Collection page", pages[JournalPage]);
            journalAlbum = album;
            albumTitle = Caption(album, "", 20, Cream, 0, 0, 500, 30, true);
            for (int i = 0; i < game.config.collection.Length; i++)
            {
                var tile = Panel("Collection item " + i, album, Inset);
                albumTiles.Add(tile.GetComponent<Image>());
                albumIcons.Add(Icon(tile, (UiGlyph.Kind)((int)UiGlyph.Kind.Helmet + i), Muted, 16, 19, 44));
                var name = Caption(album, "", 13, Muted, 0, 0, 100, 54);
                name.alignment = TextAnchor.UpperCenter;
                albumNames.Add(name);
            }
            albumNote = Caption(album, "Предметы коллекции не занимают места в рюкзаке и остаются с тобой навсегда.", 15, Mint, 0, 0, 500, 50);
            sitesTitle = Caption(album, "ЗАБРОШЕННЫЕ МЕСТА", 15, Muted, 0, 0, 500, 26, true);
            foreach (var site in MineSites.All) siteNotes.Add(Caption(album, "", 15, Cream, 0, 0, 500, 30));
            secretsText = Caption(album, "", 15, Cream, 0, 0, 500, 44);
            secretsText.resizeTextForBestFit = true;
            secretsText.resizeTextMinSize = 11;
            secretsText.resizeTextMaxSize = 15;
            BuildJournal();
            house.SetActive(false);
        }

        private ShopRow Row(Transform page, string title, UiGlyph.Kind icon, Color color, UnityEngine.Events.UnityAction buy)
        {
            var rect = Panel("Row " + title, page, Card);
            var row = new ShopRow
            {
                rect = rect,
                icon = Icon(rect, icon, color, 16, 18, 46),
                title = Caption(rect, title, 19, Cream, 78, 8, 400, 30, true),
                info = Caption(rect, "", 15, Muted, 78, 38, 400, 40),
                button = Action(rect, "", Amber, buy),
            };
            row.buttonText = row.button.GetComponentInChildren<Text>();
            row.info.resizeTextForBestFit = true;
            row.info.resizeTextMinSize = 13;
            row.info.resizeTextMaxSize = 17;
            return row;
        }

        private void LayoutHouse(bool portraitLayout, bool wideTouch)
        {
            float width = portraitLayout ? 512 : 880, height = portraitLayout ? 964 : 680;
            Center(houseCard, 0, 0, width, height);
            At(houseWallet.rectTransform, width - 288, 18, 260, 40);
            float tabWidth = (width - 56 - (TabNames.Length - 1) * 8) / TabNames.Length;
            for (int i = 0; i < tabButtons.Count; i++)
                At((RectTransform)tabButtons[i].transform, 28 + i * (tabWidth + 8), 94, tabWidth, 48);
            float contentWidth = width - 56, contentTop = 156, footerHeight = wideTouch ? 72 : 60;
            float contentHeight = height - contentTop - footerHeight - 30;
            foreach (var page in pages) At(page, 28, contentTop, contentWidth, contentHeight);
            float footerY = height - footerHeight - 18;
            At((RectTransform)closeButton.transform, width - 28 - (portraitLayout ? 190 : 230), footerY, portraitLayout ? 190 : 230, footerHeight);
            At((RectTransform)soundButton.transform, width - 28 - (portraitLayout ? 190 : 230) - 150, footerY, 140, footerHeight);
            At(houseFooter.rectTransform, 28, footerY, width - 56 - (portraitLayout ? 340 : 380), footerHeight);

            // Sell page: two columns of ore lines in landscape, one in portrait.
            At(sellSummary.rectTransform, 0, 0, contentWidth, 30);
            if (portraitLayout)
            {
                At((RectTransform)adButton.transform, 0, contentHeight - 64 - 64, contentWidth, 56);
                At((RectTransform)sellButton.transform, 0, contentHeight - 64, contentWidth, 64);
            }
            else
            {
                float sellWidth = Mathf.Round(contentWidth * .56f);
                At((RectTransform)sellButton.transform, 0, contentHeight - 64, sellWidth, 64);
                At((RectTransform)adButton.transform, sellWidth + 12, contentHeight - 64, contentWidth - sellWidth - 12, 64);
            }

            // Order page: card, then the two buttons side by side (stacked in portrait), then the explanation.
            float boxHeight = portraitLayout ? 200 : 160;
            At(orderBox, 0, 0, contentWidth, boxHeight);
            At(orderHead.rectTransform, 100, 16, contentWidth - 120, portraitLayout ? 64 : 36);
            orderHead.resizeTextForBestFit = true; orderHead.resizeTextMinSize = 16; orderHead.resizeTextMaxSize = 24;
            float textTop = portraitLayout ? 84 : 54;
            At(orderHave.rectTransform, 100, textTop, contentWidth - 120, 28);
            At(orderPay.rectTransform, 100, textTop + 30, contentWidth - 120, 28);
            At((RectTransform)orderFill.rectTransform.parent, 100, textTop + 72, contentWidth - 130, 8);
            float buttonTop = boxHeight + 12;
            if (portraitLayout)
            {
                At((RectTransform)deliverButton.transform, 0, buttonTop, contentWidth, 62);
                At((RectTransform)skipButton.transform, 0, buttonTop + 70, contentWidth, 54);
                At(orderNote.rectTransform, 0, buttonTop + 136, contentWidth, 150);
            }
            else
            {
                float deliverWidth = Mathf.Round(contentWidth * .56f);
                At((RectTransform)deliverButton.transform, 0, buttonTop, deliverWidth, 62);
                At((RectTransform)skipButton.transform, deliverWidth + 12, buttonTop, contentWidth - deliverWidth - 12, 62);
                At(orderNote.rectTransform, 0, buttonTop + 78, contentWidth, 110);
            }

            // Upgrade and item rows: button on the right in landscape, below the text in portrait.
            float rowHeight = portraitLayout ? 138 : 80, gap = 6;
            LayoutRows(upgradeRows, contentWidth, rowHeight, gap, portraitLayout, wideTouch);
            LayoutRows(itemRows, contentWidth, rowHeight, gap, portraitLayout, wideTouch);
            At(itemsNote.rectTransform, 0, itemRows.Count * (rowHeight + gap) + 6, contentWidth, 50);

            LayoutJournal(contentWidth, contentHeight, portraitLayout);
            At(albumTitle.rectTransform, 0, 0, contentWidth, 30);
            int count = albumTiles.Count;
            float tile = portraitLayout ? 84 : 110, tileGap = (contentWidth - count * tile) / Mathf.Max(1, count - 1);
            for (int i = 0; i < count; i++)
            {
                At(albumTiles[i].rectTransform, i * (tile + tileGap), 46, tile, tile);
                At(albumIcons[i].rectTransform, (tile - 52) / 2, (tile - 52) / 2, 52, 52);
                At(albumNames[i].rectTransform, i * (tile + tileGap) - 8, 52 + tile, tile + 16, 54);
            }
            At(albumNote.rectTransform, 0, portrait ? 120 + tile : 108 + tile, contentWidth, portrait ? 60 : 36);
            At(sitesTitle.rectTransform, 0, portrait ? 308 : 262, contentWidth, 26);
            for (int i = 0; i < siteNotes.Count; i++)
                At(siteNotes[i].rectTransform, portrait ? 0 : i * contentWidth / 3, (portrait ? 348 + i * 52 : 290),
                    portrait ? contentWidth : contentWidth / 3 - 8, portrait ? 44 : 48);
            At(secretsText.rectTransform, 0, portrait ? 512 : 342, contentWidth, portrait ? 90 : 44);
        }

        private void LayoutRows(List<ShopRow> rows, float width, float rowHeight, float gap, bool portraitLayout, bool wideTouch)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                At(row.rect, 0, i * (rowHeight + gap), width, rowHeight);
                float buttonWidth = portraitLayout ? width - 32 : 250, freeWidth = row.free != null ? 118 : 0, spare = freeWidth > 0 ? freeWidth + 8 : 0;
                At(row.title.rectTransform, 78, 8, portraitLayout ? width - 94 : width - 94 - buttonWidth - spare - 16, 30);
                At(row.info.rectTransform, 78, portraitLayout ? 31 : 34, portraitLayout ? width - 94 : width - 94 - buttonWidth - spare - 16, 40);
                float buttonHeight = portraitLayout ? 60 : wideTouch ? 64 : 56, buttonY = portraitLayout ? rowHeight - 66 : (rowHeight - buttonHeight) / 2;
                if (portraitLayout) At((RectTransform)row.button.transform, 16, buttonY, buttonWidth - spare, buttonHeight);
                else At((RectTransform)row.button.transform, width - 16 - buttonWidth, buttonY, buttonWidth, buttonHeight);
                if (row.free != null)
                    At((RectTransform)row.free.transform, portraitLayout ? width - 16 - freeWidth : width - 16 - buttonWidth - spare, buttonY, freeWidth, buttonHeight);
            }
        }

        public void ShowHouse(int page)
        {
            if (!house.activeSelf) game.Ui("open");
            house.SetActive(true);
            ending.SetActive(false);
            ClearInput();
            SelectTab(page);
            game.TryAdvertisingBreak();
        }

        private void SelectTab(int index)
        {
            tab = index;
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].gameObject.SetActive(i == tab);
                tabButtons[i].GetComponent<Image>().color = i == tab ? Mint : Inset;
                tabButtons[i].GetComponentInChildren<Text>().color = i == tab ? Ink : Cream;
            }
        }

        private void UpdateHouse()
        {
            var progress = game.Progress;
            var config = game.config;
            houseWallet.text = progress.coins.ToString("N0") + " монет";
            int used = progress.UsedSlots(config), capacity = progress.Capacity(config);
            houseFooter.text = Time.unscaledTime < toastUntil ? toast.text : "Рюкзак: " + used + " / " + capacity + " слотов";
            houseFooter.color = Time.unscaledTime < toastUntil ? Amber : Muted;
            soundText.text = "Настройки";
            switch (tab)
            {
                case SellPage: UpdateSell(progress, config); break;
                case OrderPage: UpdateOrderPage(progress, config); break;
                case UpgradePage: UpdateUpgrades(progress, config); break;
                case ItemPage: UpdateItems(progress, config, used, capacity); break;
                default: UpdateAlbum(progress, config); break;
            }
        }

        private void UpdateSell(GameProgress progress, MineConfig config)
        {
            var page = pages[SellPage].rect;
            int pieces = progress.OrePieces, value = progress.SaleValue(config);
            bool statue = progress.HasSecret(Secrets.Statue);
            sellSummary.text = pieces > 0 ? "В рюкзаке " + pieces + " " + MineGame.Plural(pieces, "находка", "находки", "находок") + " на " + value + " монет" +
                    (statue ? " · +10% за Золотого Нубика" : "")
                : "Рюкзак пуст. Руда блестит в стенах шахты — копай и приноси сюда.";
            bool twoColumns = !portrait;
            float lineWidth = twoColumns ? (page.width - 12) / 2 : page.width;
            int shown = 0;
            for (int i = 0; i < oreLines.Count; i++)
            {
                int count = progress.OreCount(i);
                var line = oreLines[i];
                line.rect.gameObject.SetActive(count > 0);
                if (count <= 0) continue;
                int column = twoColumns ? shown % 2 : 0, row = twoColumns ? shown / 2 : shown;
                At(line.rect, column * (lineWidth + 12), 40 + row * 50, lineWidth, 44);
                At(line.name.rectTransform, 48, 4, lineWidth - 220, 36);
                At(line.amount.rectTransform, lineWidth - 180, 4, 166, 36);
                line.amount.text = count + " × " + config.ores[i].value + " = " + count * config.ores[i].value;
                shown++;
            }
            sellButton.interactable = value > 0;
            sellText.text = value > 0 ? "Продать всё  ·  +" + value + " монет" : "Нечего продавать";
            adButton.gameObject.SetActive(game.AdSupported);
            adButton.interactable = game.AdAvailable;
            int wait = game.AdWait;
            adText.text = game.AdBusy ? "Реклама открывается…" : wait > 0 ? "Реклама снова через " + wait / 60 + ":" + (wait % 60).ToString("00")
                : "Смотреть рекламу  ·  +" + game.AdCoins + " монет";
        }

        private void UpdateOrderPage(GameProgress progress, MineConfig config)
        {
            if (!progress.HasQuest) return;
            var ore = config.ores[progress.questOre];
            int have = progress.OreCount(progress.questOre), need = progress.questAmount;
            bool ready = progress.CanDeliver;
            // Coal is almost black: lift dark ores so the icon reads on the card.
            orderBig.color = Color.Lerp(ore.color, Cream, .3f);
            orderBig.kind = ore.chest ? UiGlyph.Kind.Bag : UiGlyph.Kind.Gem;
            orderHead.text = "Заказ №" + (progress.questNumber + 1) + ":  " + ore.nameRu + " × " + need;
            orderHave.text = "В рюкзаке " + Mathf.Min(have, need) + " из " + need + (ready ? " · можно сдавать" : " · найди в шахте ещё " + (need - have));
            orderHave.color = ready ? Mint : Muted;
            orderPay.text = "Награда " + progress.questReward + " монет · обычная продажа дала бы " + need * ore.value;
            orderFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(have / (float)need), 1);
            orderFill.color = ready ? Mint : Amber;
            deliverButton.interactable = ready;
            deliverText.text = ready ? "Сдать заказ  ·  +" + progress.questReward : "Пока не хватает руды";
            int zone = config.ZoneIndex(progress.maxDepth);
            orderNote.text = "Скупщик переплачивает за заказы: руда для заказа не продаётся по обычной цене, а уходит ему. " +
                "С каждой выполненной работой он доверяет тебе больше и платит щедрее. Новые зоны — новые заказы" +
                (zone < config.zones.Length - 1 ? " (следующая: " + config.zones[zone + 1].nameRu + ", " + config.zones[zone + 1].startDepth + " м)." : ".") +
                "\nВыполнено заказов: " + progress.questNumber;
        }

        private void UpdateUpgrades(GameProgress progress, MineConfig config)
        {
            for (int i = 0; i < Tracks.Length; i++)
            {
                var track = Tracks[i];
                var row = upgradeRows[i];
                int level = progress.Level(track), levels = progress.Levels(track, config), price = progress.NextPrice(track, config);
                bool max = price < 0;
                row.title.text = TrackNames[i] + "  ·  ур. " + (level + 1) + " / " + levels;
                row.info.text = UpgradeInfo(track, level, max, config);
                row.button.interactable = !max && progress.coins >= price;
                row.buttonText.text = max ? "Максимум" : "Улучшить" + "  ·  " + price;
                if (row.free != null) row.free.interactable = !max;
            }
        }

        private static string UpgradeInfo(Track track, int level, bool max, MineConfig config)
        {
            int next = level + 1;
            switch (track)
            {
                case Track.Tool:
                    var tool = config.tools[level];
                    return max ? tool.nameRu + ": сила " + tool.damage + ". Использует общий бензобак."
                        : "Далее: " + config.tools[next].nameRu + "\nСила с " + tool.damage + " до " + config.tools[next].damage + ", удар быстрее";
                case Track.Backpack:
                    return max ? config.backpack[level].value + " слотов. Больше не унести."
                        : "Вместимость с " + config.backpack[level].value + " до " + config.backpack[next].value + " слотов";
                case Track.Fuel:
                    return max ? "Общий бак " + config.fuelTank[level].value + " л. Заправка на базе."
                        : "Общий бак с " + config.fuelTank[level].value + " до " + config.fuelTank[next].value + " л · бур + джетпак";
                case Track.Jetpack:
                    return max ? "Расход " + config.jetpack[level].value + " л/с. Максимальная экономичность."
                        : "Расход с " + config.jetpack[level].value + " до " + config.jetpack[next].value + " л/с. Бак общий с буром.";
                default:
                    return max ? config.health[level].value + " здоровья. Крепче некуда."
                        : "Здоровье с " + config.health[level].value + " до " + config.health[next].value + ": падения менее опасны";
            }
        }

        private void UpdateItems(GameProgress progress, MineConfig config, int used, int capacity)
        {
            var scanner = itemRows[0];
            scanner.info.text = "Показывает руду сквозь породу в " + config.scanner.power + " м. Занимает " + config.scanner.slots + " слота. " + (game.TouchMode ? "Кнопка «Скан»" : "Клавиша F");
            scanner.button.interactable = !progress.scanner && progress.coins >= config.scanner.price && capacity - used >= config.scanner.slots;
            scanner.buttonText.text = progress.scanner ? "Уже в рюкзаке" : capacity - used < config.scanner.slots ? "Нет места" : "Купить  ·  " + config.scanner.price;
            var medkit = itemRows[1];
            medkit.title.text = config.medkit.nameRu + (progress.medkits > 0 ? "  ·  в рюкзаке " + progress.medkits : "");
            medkit.info.text = "+" + config.medkit.power + " здоровья в шахте. Занимает " + config.medkit.slots + " слот. " + (game.TouchMode ? "Кнопка «Аптечка»" : "Клавиша Q");
            medkit.button.interactable = progress.coins >= config.medkit.price && capacity - used >= config.medkit.slots;
            medkit.buttonText.text = capacity - used < config.medkit.slots ? "Нет места" : "Купить  ·  " + config.medkit.price;
            var dynamite = itemRows[2];
            var blast = config.dynamite;
            dynamite.title.text = blast.nameRu + (progress.dynamite > 0 ? "  ·  в рюкзаке " + progress.dynamite : "");
            dynamite.info.text = "Взрывает любую породу в радиусе " + blast.power.ToString("0.#") + " м через " + config.dynamiteFuse.ToString("0.#") +
                " с после броска. Отойди — взрыв ранит. " + blast.slots + " слот. " + (game.TouchMode ? "Кнопка «Динамит»" : "Клавиша G");
            dynamite.button.interactable = progress.coins >= blast.price && capacity - used >= blast.slots;
            dynamite.buttonText.text = capacity - used < blast.slots ? "Нет места" : "Купить  ·  " + blast.price;
            itemsNote.text = "Предметы едут в рюкзаке и занимают место для руды. Свободно: " + Mathf.Max(0, capacity - used) + " из " + capacity + ".";
        }

        private void UpdateAlbum(GameProgress progress, MineConfig config)
        {
            UpdateJournal(progress);
            int secrets = Secrets.Count(progress);
            var names = new List<string>();
            for (int i = 0; i < Secrets.All.Length; i++) if (progress.HasSecret(i)) names.Add(Secrets.All[i].Name);
            // The letter's riddle stays readable until the capsule it points at is found.
            bool letter = progress.HasSecret(1) && !progress.HasSecret(2);
            secretsText.text = "СЕКРЕТЫ ДВОРА · " + secrets + " / " + Secrets.All.Length + (names.Count > 0 ? " · " + string.Join(", ", names) : " · где-то рядом спрятаны странные вещи") +
                (letter ? "\nПисьмо: " + Secrets.All[1].Note : "");
            secretsText.color = secrets > 0 ? Amber : Muted;
            albumTitle.text = "ТВОЯ КОЛЛЕКЦИЯ  ·  " + progress.CollectionCount + " / " + config.collection.Length;
            for (int i = 0; i < siteNotes.Count; i++)
            {
                var site = MineSites.All[i];
                bool visited = progress.HasSite(i), taken = progress.HasSpecial(site.CacheId);
                siteNotes[i].color = visited ? site.Accent : Muted;
                siteNotes[i].text = site.Depth + " м · " + (visited ? site.Name : "Неизвестный проход") + "\n" +
                    (taken ? "Тайник собран" : visited ? "Тайник ещё здесь" : "Найди вход в стене шахты");
            }
            for (int i = 0; i < albumTiles.Count; i++)
            {
                bool found = progress.HasCollectible(i);
                albumIcons[i].kind = found ? (UiGlyph.Kind)((int)UiGlyph.Kind.Helmet + i) : UiGlyph.Kind.Lock;
                albumIcons[i].color = found ? config.collection[i].color : Muted;
                albumTiles[i].color = found ? new Color(0.17f, 0.28f, 0.27f) : Inset;
                // A rough depth turns a locked slot into a goal.
                albumNames[i].text = found ? config.collection[i].nameRu : "где-то\nна " + RoundDepth(config.collection[i].depth) + " м";
            }
        }

        private static int RoundDepth(int metres) => metres < 10 ? metres : Mathf.RoundToInt(metres / 5f) * 5;

        /// <summary>Jump fires on the press, so holding on can light the jetpack.</summary>
        private void LateUpdate()
        {
            if (Jump == null) return;
            if (Jump.Held && !jumpWasHeld) jumpQueued = true;
            jumpWasHeld = Jump.Held;
        }
    }
}
