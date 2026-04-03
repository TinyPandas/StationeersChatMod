using System.IO;
using System.Reflection;
using UnityEngine;

namespace ChatMod
{
    /// <summary>Plays chat_message.wav from the mod DLL directory when enabled in config.</summary>
    public static class ChatNotificationSound
    {
        private static bool _loggedMissing;

        public static void Play()
        {
            if (ModConfig.PlaySoundOnMessage?.Value != true)
                return;

            string? pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(pluginDir))
                return;

            string wavPath = Path.Combine(pluginDir, "chat_message.wav");
            AudioClip? clip = WavLoader.LoadFromFile(wavPath);
            if (clip == null)
            {
                if (!_loggedMissing)
                {
                    _loggedMissing = true;
                    ChatModLog.Warning("[ChatMod] chat_message.wav not found or invalid; notification sound disabled.");
                }

                return;
            }

            float volume = Mathf.Clamp01(ModConfig.MessageSoundVolume?.Value ?? 0.7f);
            var cam = Camera.main;
            var position = cam != null ? cam.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}
