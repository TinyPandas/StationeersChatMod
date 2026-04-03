using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects.Entities;

namespace ChatMod
{
    /// <summary>Sends chat messages to the server or broadcasts when hosting.</summary>
    public static class ChatMessageSender
    {
        public static void Send(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (Human.LocalHuman == null)
            {
                ChatModLog.Warning("[ChatMod] Cannot send chat: LocalHuman is null.");
                return;
            }

            var chatMessage = new ChatMessage
            {
                ChatText = text,
                DisplayName = Human.LocalHuman.DisplayName,
                HumanId = Human.LocalHuman.ReferenceId
            };

            if (NetworkManager.IsServer)
                chatMessage.DisplayName += " (Host)";

            if (NetworkManager.IsClient)
            {
                NetworkClient.SendToServer<ChatMessage>(chatMessage, NetworkChannel.GeneralTraffic);
                return;
            }

            if (NetworkManager.IsServer)
            {
                chatMessage.PrintToConsole();
                NetworkServer.SendToClients<ChatMessage>(chatMessage, NetworkChannel.GeneralTraffic, -1L);
                return;
            }

            chatMessage.PrintToConsole();
        }
    }
}
