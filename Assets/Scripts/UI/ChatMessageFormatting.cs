using System;
using TMPro;

namespace ChatMod
{
    /// <summary>Rich text for MessageRow / MessageRowHost prefab TMP (matches sample text on those prefabs).</summary>
    public static class ChatMessageFormatting
    {
        /// <summary>Sprite name on the host row TMP (see MessageRowHost.prefab).</summary>
        public const string HostSpriteTag = "<sprite name=\"host_icon_3\">";

        /// <summary>Removes Stationeers-style <c>(Host)</c> suffix; host is shown via MessageRowHost sprite instead.</summary>
        public static string StripHostSuffix(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return displayName;

            var s = displayName.TrimEnd();
            const string token = "(Host)";
            if (s.Length < token.Length || !s.EndsWith(token, StringComparison.OrdinalIgnoreCase))
                return displayName;

            return s.Substring(0, s.Length - token.Length).TrimEnd();
        }

        public static string BuildLine(ChatEntry entry, bool hostVariant)
        {
            string nameHex = entry.NameColorHex ?? ChatEntry.DefaultNameColorHex;
            string name = StripHostSuffix(entry.DisplayName);
            if (hostVariant)
                return $"[{entry.Timestamp:HH:mm:ss}] {HostSpriteTag}<color=#{nameHex}>{name}</color>: {entry.Message}";
            return $"[{entry.Timestamp:HH:mm:ss}] <color=#{nameHex}>{name}</color>: {entry.Message}";
        }

        public static void ApplyTo(TextMeshProUGUI? tmp, ChatEntry entry, bool hostVariant)
        {
            if (tmp == null)
                return;
            tmp.text = BuildLine(entry, hostVariant);
        }
    }
}
