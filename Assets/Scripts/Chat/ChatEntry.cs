#nullable enable
using System;

namespace ChatMod
{
    /// <summary>A single chat message with timestamp, display name, and text; visibility rules for collapsed feed.</summary>
    public class ChatEntry
    {
        public DateTime Timestamp { get; set; }
        public string DisplayName { get; set; } = "";
        public string Message { get; set; } = "";
        /// <summary>Optional 6-char hex (RRGGBB) for name color; null = use default in UI.</summary>
        public string? NameColorHex { get; set; }

        /// <summary>Use host row prefab / sprite layout (e.g. session host in Stationeers).</summary>
        public bool IsHost { get; set; }

        /// <summary>Monotonic id assigned in <see cref="ChatHistoryStore"/>; aligns UI rows with store after head trim.</summary>
        public long Sequence { get; set; }

        public bool IsVisibleInCollapsedView(DateTime now)
        {
            var duration = ModConfig.NotificationDuration?.Value ?? 10;
            return (now - Timestamp).TotalSeconds <= duration;
        }
    }
}
