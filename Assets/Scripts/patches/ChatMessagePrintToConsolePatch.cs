using Assets.Scripts.Networking;
using Assets.Scripts.Objects.Entities;
using HarmonyLib;

namespace ChatMod.Patches
{
    /// <summary>Player chat → history + notification; skip vanilla console line for chat.</summary>
    [HarmonyPatch(typeof(ChatMessage), nameof(ChatMessage.PrintToConsole))]
    public static class ChatMessagePrintToConsolePatch
    {
        private static long _lastDedupeHumanId;
        private static string? _lastDedupeText;
        private static int _lastDedupeFrame = -1;

        private static bool Prefix(ChatMessage __instance)
        {
            if (__instance == null)
                return false;

            string displayName = __instance.DisplayName ?? "Unknown";
            string chatText = __instance.ChatText ?? string.Empty;

            if (string.IsNullOrWhiteSpace(chatText))
                return false;

            // On a listen server (host + client), PrintToConsole fires twice for the
            // same message in the same frame — once server-side, once client-side.
            int frame = UnityEngine.Time.frameCount;
            if (frame == _lastDedupeFrame &&
                __instance.HumanId == _lastDedupeHumanId &&
                chatText == _lastDedupeText)
                return false;

            _lastDedupeFrame = frame;
            _lastDedupeHumanId = __instance.HumanId;
            _lastDedupeText = chatText;

            string nameColorHex = PlayerNameColorCache.GetOrResolve(__instance.HumanId);
            bool isOwnMessage = Human.LocalHuman != null && __instance.HumanId == Human.LocalHuman.ReferenceId;
            ChatHistoryStore.Add(displayName, chatText, nameColorHex, countTowardUnreadBadge: !isOwnMessage);
            if (!isOwnMessage || ModConfig.PlaySoundForOwnMessages?.Value == true)
                ChatNotificationSound.Play();

            return false;
        }
    }
}
