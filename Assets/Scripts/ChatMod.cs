using HarmonyLib;
using StationeersMods.Interface;

namespace ChatMod
{
    [StationeersMod(PluginGuid, "ChatMod", "0.1.0")]
    public class ChatMod : ModBehaviour
    {
        public const string PluginGuid = "tinypanda-stationeers-chat-mod";

        public override void OnLoaded(ContentHandler contentHandler)
        {
            ModConfig.Bind(Config);

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(ChatMod).Assembly);

            ChatPanel.Initialize(contentHandler);

            ChatModLog.Info($"{PluginGuid} loaded (Harmony patched, UI host ready).");
        }
    }
}
