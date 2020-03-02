using System;
using StateFlux.Model;

namespace StateFlux.Service
{
    public class RequestFullStateMessageHandler : MessageHandler
    {
        public RequestFullStateMessageHandler(AppWebSocketBehavior serviceBehavior) : base(serviceBehavior)
        {
        }

        public Message RequestFullBatch(RequestFullStateMessage message)
        {
            Player player = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(player, "requires a user session", _websocket);

            GameInstance gameInstance = _websocket.FindPlayerGameInstance(player);
            _websocket.Broadcast(new RequestFullStateMessage(), new GameInstanceRef(gameInstance), true);
            return null;
        }
    }
}
