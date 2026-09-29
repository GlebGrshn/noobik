using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nubik
{
    /// <summary>The settings window: a volume slider per sound group and the graphics level.</summary>
    public sealed partial class MineHud
    {
        private GameObject settings;
        private RectTransform settingsCard;
        private readonly List<Slider> volumeSliders = new List<Slider>();
        private readonly List<Text> volumeValues = new List<Text>();
        private readonly List<Button> qualityButtons = new List<Button>();
        private Text qualityNote;
        private Button startGear;
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
            Center(settingsCard, 0, 0, 500, 580);
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
            Caption(settingsCard, "ГРАФИКА", 15, Muted, 28, 346, 440, 26, true);
            for (int i = 0; i < GameSettings.QualityNames.Length; i++)
            {
                int level = i;
                var button = Action(settingsCard, GameSettings.QualityNames[i], Inset, () => GameSettings.Set(s => s.quality = level), Cream);
                At((RectTransform)button.transform, 28 + i * 152, 376, 140, 52);
                qualityButtons.Add(button);
            }
            qualityNote = Caption(settingsCard, "", 14, Muted, 28, 434, 444, 44);
            var done = Action(settingsCard, "Готово", Amber, CloseSettings);
            At((RectTransform)done.transform, 28, 494, 444, 60);
            settings.SetActive(false);
        }

        /// <summary>A plain horizontal slider built from the HUD's own sprites.</summary>
        private Slider MakeSlider(Transform parent)
        {
            var rect = Rect("Slider", parent);
            var track = Panel("Slider track", rect, Inset);
            track.anchorMin = new Vector2(0, .5f); track.anchorMax = new Vector2(1, .5f);
            track.sizeDelta = new Vector2(0, 10); track.anchoredPosition = Vector2.zero;
            var fillArea = Rect("Slider fill area", rect);
            fillArea.anchorMin = new Vector2(0, .5f); fillArea.anchorMax = new Vector2(1, .5f);
            fillArea.sizeDelta = new Vector2(-16, 10); fillArea.anchoredPosition = Vector2.zero;
            var fill = Panel("Slider fill", fillArea, Mint);
            fill.sizeDelta = new Vector2(16, 0);
            var handleArea = Rect("Slider handle area", rect);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(8, 0); handleArea.offsetMax = new Vector2(-8, 0);
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
            float[] values = { current.master, current.music, current.effects, current.ambience };
            for (int i = 0; i < volumeSliders.Count; i++)
            {
                volumeSliders[i].SetValueWithoutNotify(values[i]);
                volumeValues[i].text = Mathf.RoundToInt(values[i] * 100) + "%";
            }
            ClearInput();
            settings.SetActive(true);
            settings.transform.SetAsLastSibling();
            toastCard.SetAsLastSibling();
        }

        public void CloseSettings()
        {
            if (!SettingsOpen) return;
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
        }
    }
}
