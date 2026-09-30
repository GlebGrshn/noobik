using UnityEngine.UI;

namespace Nubik
{
    /// <summary>Keeps the original text, including inactive panels, for reversible live language changes.</summary>
    public sealed class LocalizedText : Text
    {
        public string SourceText { get; private set; } = "";
        public override string text
        {
            get => base.text;
            set { SourceText = value ?? ""; base.text = Localization.Translate(SourceText); }
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            Localization.Changed += RefreshLanguage;
            RefreshLanguage();
        }
        protected override void OnDisable()
        {
            Localization.Changed -= RefreshLanguage;
            base.OnDisable();
        }
        private void RefreshLanguage() => base.text = Localization.Translate(SourceText);
    }
}
