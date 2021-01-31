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

            var outgoingMessage = new HostStateChangedMessage
            {
                Payload = message.Payload
            };

            _websocket.Broadcast(outgoingMessage, currentPlayer.GameInstanceRef, meToo: false);

            return null;
        }

        public Message GuestStateChange(GuestInputChangeMessage message)
        {
            Player currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);

            GameInstance gameInstance = _websocket.FindPlayerGameInstance(currentPlayer);

            if(gameInstance != null)
            {
                var outgoingMessage = new GuestInputChangedMessage
                {
                    Guest = currentPlayer.Id,
                    Payload = message.Payload
                };
                _websocket.Send(outgoingMessage, gameInstance.HostPlayer.Id);
            }

            return null;
        }
    }
}
