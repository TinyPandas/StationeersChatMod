using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace ChatMod
{
    /// <summary>
    /// Logs to BepInEx (<c>BepInEx/LogOutput.log</c> and console) and Unity (<c>Player.log</c>).
    /// </summary>
    public static class ChatModLog
    {
        private static ManualLogSource? _log;

        private static ManualLogSource? TryGet()
        {
            if (_log != null)
                return _log;
            try
            {
                _log = Logger.CreateLogSource("ChatMod");
            }
            catch
            {
                // BepInEx not available; Unity logs only.
            }

            return _log;
        }

        public static void Info(string message)
        {
            TryGet()?.LogInfo(message);
            Debug.Log(message);
        }

        public static void Warning(string message)
        {
            TryGet()?.LogWarning(message);
            Debug.LogWarning(message);
        }

        public static void Error(string message)
        {
            TryGet()?.LogError(message);
            Debug.LogError(message);
        }
    }
}
