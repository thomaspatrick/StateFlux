using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;
using StateFlux.Model;

namespace StateFlux.Service
{
    public class ChatMessageHandler : MessageHandler
    {
        const int MaxChatStringSize = 255;
        public ChatMessageHandler(AppWebSocketBehavior serviceBehavior) : base(serviceBehavior)
        {
        }

        public void ChatSay(ChatSayMessage message)
        {
            Player currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);
            ChatSaid said = new ChatSaid(currentPlayer.Name, message.Say.Truncate(MaxChatStringSize));
            _server.Chat.Add(said);
            ChatSaidMessage chatSaidMessage = new ChatSaidMessage();
            chatSaidMessage.PlayerName = said.PlayerName;
            chatSaidMessage.Say = said.Saying;
            _websocket.LogMessage($"{currentPlayer.Name} says '{said.Saying}'");
            _websocket.Broadcast(chatSaidMessage, currentPlayer.GameInstanceRef, true);
        }

        public void PlayerList(PlayerListMessage message)
        {
            Player currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);
            PlayerListingMessage playerListingMessage = new PlayerListingMessage();
            playerListingMessage.Players = Server.Instance.Players;
            _websocket.Broadcast(playerListingMessage, null, true);
        }

        public void PlayerRename(PlayerRenameMessage message)
        {
            Player currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);
            currentPlayer.Name = message.Name.Truncate(50);
            _server.playerRepository.UpdatePlayer(currentPlayer);
            PlayerListingMessage playerListingMessage = new PlayerListingMessage();
            playerListingMessage.Players = Server.Instance.Players;
            _websocket.Broadcast(playerListingMessage, null, true);
        }
    }
}
