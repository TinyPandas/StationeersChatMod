using TMPro;

namespace ChatMod
{
    /// <summary>Applies configured font size to spawned message row TMP components.</summary>
    public static class ChatUiLayout
    {
        public const float DefaultMessageFontSize = 18f;

        /// <summary>Applies <see cref="ModConfig.MessageFontSize"/> to a spawned row TMP (fixed size; disables auto-sizing).</summary>
        public static void ApplyConfiguredMessageFontSize(TextMeshProUGUI? tmp)
        {
            if (tmp == null)
                return;

            float size = ModConfig.MessageFontSize.Value;
            tmp.enableAutoSizing = false;
            tmp.fontSize = size;
            tmp.fontSizeMin = size;
            tmp.fontSizeMax = size;
        }
    }
}
