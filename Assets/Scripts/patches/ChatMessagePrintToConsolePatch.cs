using Assets.Scripts.Networking;
using Assets.Scripts.Objects.Entities;
using HarmonyLib;

namespace ChatMod.Patches
{
    /// <summary>Player chat → history + notification; skip vanilla console line for chat.</summary>
    [HarmonyPatch(typeof(ChatMessage), nameof(ChatMessage.PrintToConsole))]
    public static class ChatMessagePrintToConsolePatch
    {
        private static bool Prefix(ChatMessage __instance)
        {
            if (__instance == null)
                return false;

            string displayName = __instance.DisplayName ?? "Unknown";
            string chatText = __instance.ChatText ?? string.Empty;

            if (string.IsNullOrWhiteSpace(chatText))
                return false;

            string nameColorHex = PlayerNameColorCache.GetOrResolve(__instance.HumanId);
            bool isOwnMessage = Human.LocalHuman != null && __instance.HumanId == Human.LocalHuman.ReferenceId;
            ChatHistoryStore.Add(displayName, chatText, nameColorHex, countTowardUnreadBadge: !isOwnMessage);
            ChatUiBootstrap.EnsureExists();
            if (!isOwnMessage || ModConfig.PlaySoundForOwnMessages?.Value == true)
                ChatNotificationSound.Play();

            return false;
        }
    }
}
