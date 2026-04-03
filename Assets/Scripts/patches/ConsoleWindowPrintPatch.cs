using System;
using Assets.Scripts;
using HarmonyLib;

namespace ChatMod.Patches
{
    /// <summary>Routes console Print lines into chat history (non-player messages).</summary>
    [HarmonyPatch(typeof(ConsoleWindow))]
    public static class ConsoleWindowPrintPatch
    {
        private const string PrefixError = "[Error]";
        private const string PrefixAction = "[Action]";
        private const string PrefixConsole = "[Console]";

        [HarmonyPatch(nameof(ConsoleWindow.Print), typeof(string), typeof(ConsoleColor), typeof(bool), typeof(bool), typeof(bool))]
        [HarmonyPostfix]
        public static void Print_Postfix(string output, ConsoleColor color)
        {
            if (string.IsNullOrWhiteSpace(output))
                return;

            string displayName = GetDisplayNameForColor(color);
            if (!ShouldCapture(displayName))
                return;

            ChatHistoryStore.Add(displayName, output, ChatEntry.DefaultNameColorHex);
        }

        private static bool ShouldCapture(string displayName)
        {
            return displayName switch
            {
                PrefixError => ModConfig.LogErrorMessages?.Value ?? true,
                PrefixAction => ModConfig.LogActionMessages?.Value ?? true,
                PrefixConsole => ModConfig.LogConsoleMessages?.Value ?? false,
                _ => false
            };
        }

        private static string GetDisplayNameForColor(ConsoleColor color)
        {
            return color switch
            {
                ConsoleColor.Red or ConsoleColor.DarkRed or ConsoleColor.DarkMagenta or ConsoleColor.Magenta => PrefixError,
                ConsoleColor.Yellow or ConsoleColor.DarkYellow => PrefixAction,
                _ => PrefixConsole
            };
        }
    }
}
