using System;
using StateFlux.Model;

namespace StateFlux.Service
{
    public class GameCommandChangeHandler : MessageHandler
    {
        public GameCommandChangeHandler(AppWebSocketBehavior serviceBehavior) : base(serviceBehavior)
        {
        }

        public Message HostCommandChange(HostCommandChangeMessage message)
        {
            Player currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);

            var outgoingMessage = new HostCommandChangedMessage
            {
                Payload = message.Payload
            };

            _websocket.Broadcast(outgoingMessage, currentPlayer.GameInstanceRef, meToo: false);

            return null;
        }

        public Message GuestCommandChange(GuestCommandChangeMessage message)
        {
            Player currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);

            GameInstance gameInstance = _websocket.FindPlayerGameInstance(currentPlayer);

            var outgoingMessage = new GuestCommandChangedMessage
            {
                Guest = currentPlayer.Id,
                Payload = message.Payload
            };
            _websocket.Send(outgoingMessage, gameInstance.HostPlayer.Id);

            return null;
        }
    }
}
