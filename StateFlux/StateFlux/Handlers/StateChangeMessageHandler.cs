using System;
using StateFlux.Model;

namespace StateFlux.Service
{
    public class StateChangeMessageHandler : MessageHandler
    {
        public StateChangeMessageHandler(AppWebSocketBehavior serviceBehavior) : base(serviceBehavior)
        {
        }

        public Message StateChange(StateChangeMessage message)
        {
            Player currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);
            StateChangedMessage stateChangedMessage = new StateChangedMessage
            {
                Payload = message.Payload
            };
            _websocket.Broadcast(stateChangedMessage, currentPlayer.GameInstance, true);
            return null;
        }

    }
}
