using System;
using StateFlux.Model;

namespace StateFlux.Service
{
    public class StateChangeMessageHandler : MessageHandler
    {
        public StateChangeMessageHandler(AppWebSocketBehavior serviceBehavior) : base(serviceBehavior)
        {
        }

        // host requests the server to broadcast state changes
        public Message HostStateChange(HostStateChangeMessage message)
        {
            Player currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);

            GameInstance gameInstance = _websocket.FindPlayerGameInstance(currentPlayer);
            var outgoingMessage = new HostStateChangedMessage
            {
                Payload = message.Payload
            };

            _websocket.Broadcast(outgoingMessage, currentPlayer.GameInstanceRef, meToo: false);

            return null;
        }

        public Message GuestStateChange(GuestStateChangeMessage message)
        {
            Player currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);

            GameInstance gameInstance = _websocket.FindPlayerGameInstance(currentPlayer);

            var outgoingMessage = new GuestStateChangedMessage
            {
                Guest = currentPlayer.Id,
                Payload = message.Payload
            };
            _websocket.Send(outgoingMessage, gameInstance.HostPlayer.Id);

            return null;
        }
    }
}
