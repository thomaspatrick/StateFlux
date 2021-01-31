using System;
using StateFlux.Model;

namespace StateFlux.Service
{
    public class MiceChangeHandler : MessageHandler
    {
        public MiceChangeHandler(AppWebSocketBehavior serviceBehavior) : base(serviceBehavior)
        {
        }

        public Message MiceChange(MiceChangeMessage message)
        {
            Player currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);

            GameInstance gameInstance = _websocket.FindPlayerGameInstance(currentPlayer);

            if(gameInstance != null)
            {
                var outgoingMessage = new MiceChangedMessage
                {
                    Payload = message.Payload
                };

                _websocket.Broadcast(outgoingMessage, currentPlayer.GameInstanceRef, meToo: false);
            }

            return null;
        }
    }
}
