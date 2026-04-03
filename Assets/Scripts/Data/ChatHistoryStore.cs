#nullable enable
using System;
using System.Collections.Generic;
namespace ChatMod
{
    /// <summary>Thread-safe in-memory store of chat messages; snapshot and configurable max count.</summary>
    public static class ChatHistoryStore
    {
        private static readonly List<ChatEntry> Messages = new();
        private static readonly object Sync = new();
        private static long _nextSequence;

        /// <summary>Raised after a message is stored.</summary>
        public static event Action? MessageAdded;

        public static IReadOnlyList<ChatEntry> GetSnapshot()
        {
            lock (Sync)
            {
                return new List<ChatEntry>(Messages);
            }
        }

        /// <summary>Lightweight change detection without copying the full message list.</summary>
        public static void GetTailMetrics(out int count, out long lastTicks, out long firstSeq)
        {
            lock (Sync)
            {
                count = Messages.Count;
                if (count == 0)
                {
                    lastTicks = -1;
                    firstSeq = -1;
                    return;
                }

                lastTicks = Messages[^1].Timestamp.Ticks;
                firstSeq = Messages[0].Sequence;
            }
        }

        public static void Add(
            string displayName,
            string chatText,
            string? nameColorHex = null,
            bool? isHost = null,
            bool countTowardUnreadBadge = true)
        {
            bool host = isHost ?? displayName.Contains("(Host)", StringComparison.OrdinalIgnoreCase);
            var entry = new ChatEntry
            {
                Timestamp = DateTime.Now,
                DisplayName = displayName,
                Message = chatText,
                NameColorHex = nameColorHex,
                IsHost = host
            };

            lock (Sync)
            {
                entry.Sequence = ++_nextSequence;
                Messages.Add(entry);

                var maxMessages = ModConfig.MaxMessages?.Value ?? 200;
                while (Messages.Count > maxMessages)
                    Messages.RemoveAt(0);
            }

            if (countTowardUnreadBadge)
                MessageAdded?.Invoke();
        }

        public static void Clear()
        {
            lock (Sync)
            {
                Messages.Clear();
                _nextSequence = 0;
            }
        }
    }
}
