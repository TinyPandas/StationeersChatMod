using TMPro;
using UnityEngine;

namespace ChatMod
{
    /// <summary>Defaults for programmatic message rows and name colors — not panel layout (prefab owns that).</summary>
    public static class ChatUiLayout
    {
        public const float DefaultMessageFontSize = 18f;

        public static float MinRowHeightForFontSize(float fontSize) =>
            Mathf.Max(14f, fontSize * 1.15f);

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

        public const string DefaultNameColorHex = "B8B8B8";
        public static readonly Color MessageTextColor = new Color(1f, 1f, 1f, 1f);
    }
}
