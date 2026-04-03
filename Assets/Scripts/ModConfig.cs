using BepInEx.Configuration;
using UnityEngine;

namespace ChatMod
{
    /// <summary>
    /// BepInEx config entries for chat history, window position, sounds, and console capture.
    /// </summary>
    public class ModConfig
    {
        public static ConfigEntry<int> MaxMessages = null!;
        public static ConfigEntry<float> NotificationDuration = null!;

        /// <summary>Capture [Action] messages (yellow), e.g. "Auto save started", "X days have passed".</summary>
        public static ConfigEntry<bool> LogActionMessages = null!;

        /// <summary>Capture [Error] messages (red).</summary>
        public static ConfigEntry<bool> LogErrorMessages = null!;

        /// <summary>Capture [Console] messages (general/white output from commands, debug, etc.). Can be noisy.</summary>
        public static ConfigEntry<bool> LogConsoleMessages = null!;

        /// <summary>Saved X position of ChatRoot (reference resolution space).</summary>
        public static ConfigEntry<float> ChatWindowPositionX = null!;

        /// <summary>Saved Y position of ChatRoot (reference resolution space).</summary>
        public static ConfigEntry<float> ChatWindowPositionY = null!;

        /// <summary>Key that toggles the mod chat panel (same binding as the cloned HUD row hint).</summary>
        public static ConfigEntry<KeyCode> ToggleChatPanelKey = null!;

        /// <summary>Font size (integer px) for chat message row TMP (applied at spawn time; single MessageRow / MessageRowHost prefabs).</summary>
        public static ConfigEntry<int> MessageFontSize = null!;

        /// <summary>Play a sound when a new chat message is received.</summary>
        public static ConfigEntry<bool> PlaySoundOnMessage = null!;

        /// <summary>Also play the sound when you send a message (otherwise only when others send one).</summary>
        public static ConfigEntry<bool> PlaySoundForOwnMessages = null!;

        /// <summary>Volume for the chat message notification sound (0–1).</summary>
        public static ConfigEntry<float> MessageSoundVolume = null!;

        private static ConfigFile? _configFile;

        /// <summary>Persist config to disk (e.g. after updating chat window position).</summary>
        public static void Save() => _configFile?.Save();

        public static void Bind(ConfigFile config)
        {
            _configFile = config;

            MaxMessages = config.Bind(
                "General",
                "MaxMessages",
                200,
                new ConfigDescription(
                    "Maximum number of chat messages to retain in history.",
                    new AcceptableValueRange<int>(10, 5000)
                )
            );

            NotificationDuration = config.Bind(
                "General",
                "NotificationDuration",
                10f,
                new ConfigDescription(
                    "How long a new chat notification remains visible in seconds."
                )
            );

            ChatWindowPositionX = config.Bind(
                "General",
                "ChatWindowPositionX",
                20f,
                "Saved X position of the chat window (persisted when you drag it)."
            );
            ChatWindowPositionY = config.Bind(
                "General",
                "ChatWindowPositionY",
                500f,
                "Saved Y position of the chat window (persisted when you drag it)."
            );

            ToggleChatPanelKey = config.Bind(
                "General",
                "ToggleChatPanelKey",
                KeyCode.F7,
                "Unity KeyCode to toggle the mod chat panel (and the key shown on the cloned HUD row). Examples: F7, T, BackQuote. Set to None to disable keyboard toggle (HUD row and launcher still work)."
            );

            MessageFontSize = config.Bind(
                "General",
                "MessageFontSize",
                18,
                new ConfigDescription(
                    "Font size (integer) for chat message text, applied to each row TMP at spawn.",
                    new AcceptableValueRange<int>(12, 26)
                )
            );

            PlaySoundOnMessage = config.Bind(
                "General",
                "PlaySoundOnMessage",
                true,
                "Play a sound when a new chat message is received."
            );

            PlaySoundForOwnMessages = config.Bind(
                "General",
                "PlaySoundForOwnMessages",
                false,
                "Also play the notification sound when you send a message. If false, the sound only plays for messages from other players."
            );

            MessageSoundVolume = config.Bind(
                "General",
                "MessageSoundVolume",
                0.7f,
                new ConfigDescription(
                    "Volume for the chat message notification sound.",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            );

            LogActionMessages = config.Bind(
                "Console capture",
                "LogActionMessages",
                true,
                "Capture [Action] messages in chat history (e.g. 'Auto save started', 'X days have passed')."
            );

            LogErrorMessages = config.Bind(
                "Console capture",
                "LogErrorMessages",
                true,
                "Capture [Error] messages in chat history."
            );

            LogConsoleMessages = config.Bind(
                "Console capture",
                "LogConsoleMessages",
                false,
                "Capture [Console] messages (general output from commands, debug, etc.). Can be noisy."
            );
        }
    }
}