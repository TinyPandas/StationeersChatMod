using UnityEngine;

namespace ChatMod
{
    /// <summary>Plays the chat notification sound when enabled in config.</summary>
    public static class ChatNotificationSound
    {
        /// <summary>Set from ChatPanel during initialization (wired via prefab).</summary>
        public static AudioClip? Clip;

        public static void Play()
        {
            if (ModConfig.PlaySoundOnMessage?.Value != true)
                return;

            if (Clip == null)
                return;

            float volume = Mathf.Clamp01(ModConfig.MessageSoundVolume?.Value ?? 0.7f);
            var cam = Camera.main;
            var position = cam != null ? cam.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(Clip, position, volume);
        }
    }
}
