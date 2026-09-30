using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nubik
{
    /// <summary>Device settings: volume, camera sensitivity and graphics.</summary>
    public sealed partial class MineHud
    {
        private GameObject settings;
        private RectTransform settingsCard;
        private readonly List<Slider> volumeSliders = new List<Slider>();
        private readonly List<Text> volumeValues = new List<Text>();
        private readonly List<Button> qualityButtons = new List<Button>();
        private Text qualityNote;
        private Button startGear;
        private Slider sensitivitySlider;
        private Text sensitivityValue;
        private GameObject languageChoice;
        private Button russianLanguage, englishLanguage;
        public bool LanguageChoiceOpen => languageChoice != null && languageChoice.activeSelf;
        private float languageWaitUntil = -1;
        /// <summary>The platform is still telling its language; the start card waits so it does not flash in the other one.</summary>
        public bool WaitingForLanguage => languageWaitUntil >= 0;
        public bool SettingsOpen => settings != null && settings.activeSelf;

        private static readonly string[] VolumeNames = { "Общая громкость", "Музыка", "Эффекты", "Окружение" };
        private static readonly string[] QualityNotes =
        {
            "Для слабых телефонов: ниже разрешение, без теней, проще текстуры.",
            "Баланс качества и скорости. Подходит большинству устройств.",
            "Полное разрешение, сглаживание, мягкие тени и больше света. Для мощных компьютеров.",
        };

        private void BuildSettings()
        {
            settings = Backdrop("Settings");
            settingsCard = Panel("Settings card", settings.transform, Ink);
            Center(settingsCard, 0, 0, 500, 692);
            Icon(settingsCard, UiGlyph.Kind.Gear, Mint, 24, 20, 36);
            Caption(settingsCard, "НАСТРОЙКИ", 26, Cream, 72, 18, 380, 40, true);
            Func<GameSettings, float>[] read = { s => s.master, s => s.music, s => s.effects, s => s.ambience };
            Action<GameSettings, float>[] write = { (s, v) => s.master = v, (s, v) => s.music = v, (s, v) => s.effects = v, (s, v) => s.ambience = v };
            for (int i = 0; i < VolumeNames.Length; i++)
            {
                int index = i;
                float y = 82 + i * 64;
                Caption(settingsCard, VolumeNames[i], 17, Cream, 28, y, 170, 40, true);
                var slider = MakeSlider(settingsCard);
                At((RectTransform)slider.transform, 196, y + 4, 214, 32);
                slider.value = read[i](GameSettings.Current);
                var value = Caption(settingsCard, "", 16, Muted, 418, y, 56, 40, true);
                value.alignment = TextAnchor.MiddleRight;
                slider.onValueChanged.AddListener(v =>
                {
                    GameSettings.Set(s => write[index](s, v), false);
                    volumeValues[index].text = Mathf.RoundToInt(v * 100) + "%";
                });
                volumeSliders.Add(slider);
                volumeValues.Add(value);
            }
            Caption(settingsCard, "Чувствительность\nкамеры", 17, Cream, 28, 332, 170, 56, true);
            sensitivitySlider = MakeSlider(settingsCard);
            sensitivitySlider.name = "Camera sensitivity";
            At((RectTransform)sensitivitySlider.transform, 196, 328, 214, 64);
            sensitivitySlider.minValue = GameSettings.MinSensitivity * 100;
            sensitivitySlider.maxValue = GameSettings.MaxSensitivity * 100;
            sensitivitySlider.wholeNumbers = true;
            sensitivitySlider.SetValueWithoutNotify(GameSettings.Current.cameraSensitivity * 100);
            sensitivityValue = Caption(settingsCard, "", 16, Muted, 418, 340, 56, 40, true);
            sensitivityValue.alignment = TextAnchor.MiddleRight;
            sensitivitySlider.onValueChanged.AddListener(v =>
            {
                GameSettings.Set(s => s.cameraSensitivity = v / 100f, false);
                sensitivityValue.text = Mathf.RoundToInt(v) + "%";
            });
            Caption(settingsCard, "ГРАФИКА", 15, Muted, 28, 410, 440, 26, true);
            for (int i = 0; i < GameSettings.QualityNames.Length; i++)
            {
                int level = i;
                var button = Action(settingsCard, GameSettings.QualityNames[i], Inset, () => GameSettings.Set(s => s.quality = level), Cream);
                At((RectTransform)button.transform, 28 + i * 152, 440, 140, 52);
                qualityButtons.Add(button);
            }
            qualityNote = Caption(settingsCard, "", 14, Muted, 28, 498, 444, 44);
            Caption(settingsCard, "Язык", 17, Cream, 28, 548, 150, 44, true);
            russianLanguage = Action(settingsCard, "Русский", Inset, () => Localization.Select("ru"), Cream);
            englishLanguage = Action(settingsCard, "English", Inset, () => Localization.Select("en"), Cream);
            At((RectTransform)russianLanguage.transform, 184, 548, 138, 44);
            At((RectTransform)englishLanguage.transform, 334, 548, 138, 44);
            var done = Action(settingsCard, "Готово", Amber, CloseSettings);
            At((RectTransform)done.transform, 28, 608, 444, 60);
            settings.SetActive(false);
        }

        private void BuildLanguageChoice()
        {
            languageChoice = Backdrop("Language choice");
            var card = Panel("Language card", languageChoice.transform, Ink);
            Center(card, 0, 0, 480, 310);
            var title = Caption(card, "Choose your language", 28, Cream, 24, 36, 432, 48, true);
            title.alignment = TextAnchor.MiddleCenter;
            var subtitle = Caption(card, "Выберите язык", 20, Muted, 24, 84, 432, 40);
            subtitle.alignment = TextAnchor.MiddleCenter;
            var ru = Action(card, "Русский", Inset, () => ChooseLanguage("ru"), Cream);
            ru.name = "Choose Russian";
            var en = Action(card, "English", Amber, () => ChooseLanguage("en"));
            en.name = "Choose English";
            At((RectTransform)ru.transform, 28, 162, 204, 72);
            At((RectTransform)en.transform, 248, 162, 204, 72);
            var note = Caption(card, "You can change this in Settings", 15, Muted, 24, 254, 432, 28);
            note.alignment = TextAnchor.MiddleCenter;
            languageChoice.SetActive(false);
        }

        public void ShowLanguageChoice()
        {
            ClearInput();
            languageChoice.SetActive(true);
            languageChoice.transform.SetAsLastSibling();
        }

        /// <summary>
        /// First launch without a language: a platform that sets one (Yandex Games, requirement 2.14) decides without a
        /// question; elsewhere, or when the platform does not answer within 8 seconds, the player chooses.
        /// </summary>
        public void AwaitLanguage()
        {
            languageWaitUntil = -1;
            if (Localization.Chosen) return;
            if (Localization.PlatformLanguage() == null) { ShowLanguageChoice(); return; }
            languageWaitUntil = Time.unscaledTime + 8;
            UpdateLanguageWait();
        }

        private void UpdateLanguageWait()
        {
            if (languageWaitUntil < 0) return;
            if (Localization.Chosen) { languageWaitUntil = -1; return; }
            string language = Localization.PlatformLanguage();
            if (!string.IsNullOrEmpty(language)) { languageWaitUntil = -1; Localization.Select(language); }
            else if (language == null || Time.unscaledTime > languageWaitUntil) { languageWaitUntil = -1; ShowLanguageChoice(); }
        }

        private void ChooseLanguage(string language)
        {
            Localization.Select(language);
            languageChoice.SetActive(false);
            ClearInput();
        }

        /// <summary>A plain horizontal slider built from the HUD's own sprites.</summary>
        private Slider MakeSlider(Transform parent)
        {
            var rect = Rect("Slider", parent);
            rect.gameObject.AddComponent<Image>().color = Color.clear;
            var track = Panel("Slider track", rect, Inset);
            track.anchorMin = new Vector2(0, .5f); track.anchorMax = new Vector2(1, .5f);
            track.sizeDelta = new Vector2(0, 10); track.anchoredPosition = Vector2.zero;
            var fillArea = Rect("Slider fill area", rect);
            fillArea.anchorMin = new Vector2(0, .5f); fillArea.anchorMax = new Vector2(1, .5f);
            fillArea.sizeDelta = new Vector2(-16, 10); fillArea.anchoredPosition = Vector2.zero;
            var fill = Panel("Slider fill", fillArea, Mint);
            fill.sizeDelta = new Vector2(16, 0);
            var handleArea = Rect("Slider handle area", rect);
            handleArea.anchorMin = new Vector2(0, .5f); handleArea.anchorMax = new Vector2(1, .5f);
            handleArea.sizeDelta = new Vector2(-16, 30); handleArea.anchoredPosition = Vector2.zero;
            var handle = Panel("Slider handle", handleArea, Cream, false);
            handle.GetComponent<Image>().sprite = circle;
            handle.sizeDelta = new Vector2(30, 0);
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 1;
            return slider;
        }

        public void OpenSettings()
        {
            var current = GameSettings.Current;
            sensitivitySlider.SetValueWithoutNotify(current.cameraSensitivity * 100);
            sensitivityValue.text = Mathf.RoundToInt(current.cameraSensitivity * 100) + "%";
            float[] values = { current.master, current.music, current.effects, current.ambience };
            for (int i = 0; i < volumeSliders.Count; i++)
            {
                volumeSliders[i].SetValueWithoutNotify(values[i]);
                volumeValues[i].text = Mathf.RoundToInt(values[i] * 100) + "%";
            }
            ClearInput();
            game.Ui("open");
            settings.SetActive(true);
            settings.transform.SetAsLastSibling();
            toastCard.SetAsLastSibling();
        }

        public void CloseSettings()
        {
            if (!SettingsOpen) return;
            game.Ui("close");
            settings.SetActive(false);
            GameSettings.Save();
            ClearInput();
        }

        private void UpdateSettings()
        {
            int level = GameSettings.Current.quality;
            for (int i = 0; i < qualityButtons.Count; i++)
            {
                qualityButtons[i].GetComponent<Image>().color = i == level ? Mint : Inset;
                qualityButtons[i].GetComponentInChildren<Text>().color = i == level ? Ink : Cream;
            }
            qualityNote.text = QualityNotes[level];
            russianLanguage.GetComponent<Image>().color = Localization.IsEnglish ? Inset : Mint;
            englishLanguage.GetComponent<Image>().color = Localization.IsEnglish ? Mint : Inset;
            russianLanguage.GetComponentInChildren<Text>().color = Localization.IsEnglish ? Cream : Ink;
            englishLanguage.GetComponentInChildren<Text>().color = Localization.IsEnglish ? Ink : Cream;
        }
    }
}
