using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nubik
{
    public sealed partial class MineHud
    {
        private RectTransform journalAlbum, journalKeys;
        private Button keysTab, albumTab;
        private Text journalTitle, journalGoal;
        private readonly List<Text> keyNames = new List<Text>(), keyHints = new List<Text>();
        private readonly List<UiGlyph> keyIcons = new List<UiGlyph>();
        private readonly List<RectTransform> keyRows = new List<RectTransform>();
        private void BuildJournal()
        {
            keysTab = Action(pages[3], "Пять ключей", Mint, () => JournalTab(true), Ink);
            albumTab = Action(pages[3], "Коллекция и места", Inset, () => JournalTab(false), Cream);
            journalKeys = Rect("Quest journal", pages[3]);
            journalTitle = Caption(journalKeys, "", 20, Cream, 0, 0, 500, 30, true);
            for (int i = 0; i < Expedition.Keys.Length; i++)
            {
                var row = Panel("Key clue " + i, journalKeys, Inset); keyRows.Add(row);
                keyIcons.Add(Icon(row, UiGlyph.Kind.Key, Muted, 12, 16, 32));
                keyNames.Add(Caption(row, "", 17, Cream, 58, 3, 650, 25, true));
                keyHints.Add(Caption(row, "", 14, Muted, 58, 28, 650, 34));
            }
            journalGoal = Caption(journalKeys, "", 15, Mint, 0, 0, 500, 60);
            JournalTab(true);
        }
        private void JournalTab(bool keys)
        {
            journalKeys.gameObject.SetActive(keys); journalAlbum.gameObject.SetActive(!keys);
            keysTab.GetComponent<Image>().color = keys ? Mint : Inset;
            albumTab.GetComponent<Image>().color = keys ? Inset : Mint;
            keysTab.GetComponentInChildren<Text>().color = keys ? Ink : Cream;
            albumTab.GetComponentInChildren<Text>().color = keys ? Cream : Ink;
        }
        private void LayoutJournal(float width, float height, bool narrow)
        {
            At((RectTransform)keysTab.transform, 0, 0, width * .42f, 36);
            At((RectTransform)albumTab.transform, width * .42f + 8, 0, width * .58f - 8, 36);
            At(journalKeys, 0, 46, width, height - 46); At(journalAlbum, 0, 46, width, height - 46);
            At(journalTitle.rectTransform, 0, 0, width, 30);
            float rowHeight = narrow ? 78 : 53, gap = narrow ? 8 : 5;
            for (int i = 0; i < keyRows.Count; i++)
            {
                At(keyRows[i], 0, 40 + i * (rowHeight + gap), width, rowHeight);
                At(keyNames[i].rectTransform, 58, 2, width - 70, 24);
                At(keyHints[i].rectTransform, 58, 26, width - 70, narrow ? 46 : 25);
            }
            At(journalGoal.rectTransform, 0, 44 + 5 * (rowHeight + gap), width, narrow ? 90 : 48);
        }
        private void UpdateJournal(GameProgress progress)
        {
            journalTitle.text = "ДВЕРЬ ПЯТИ ПЕЧАТЕЙ · " + progress.KeyCount + " / 5";
            journalTitle.resizeTextForBestFit = true; journalTitle.resizeTextMinSize = 16; journalTitle.resizeTextMaxSize = 20;
            for (int i = 0; i < Expedition.Keys.Length; i++)
            {
                var key = Expedition.Keys[i]; bool found = progress.HasKey(i);
                keyIcons[i].color = found ? key.Color : Muted;
                keyNames[i].text = key.Name + (found ? " · найден" : ""); keyNames[i].color = found ? Mint : Cream;
                keyHints[i].text = found ? "Сохранён. Не занимает место в рюкзаке." : key.Clue;
            }
            journalGoal.text = progress.finished ? "Ктулху побеждён. Продолжай собирать коллекцию и исследовать шахту."
                : progress.KeyCount == 5 ? "Следующая цель: дверь на 120 м. За ней лежит оружие против Ктулху."
                : "Собери ключи и открой дверь на 120 м. Перед спуском заправляй общий бак у колонки или дома.";
        }
    }
}
