using System;
using StateFlux.Model;

namespace StateFlux.Service
{
    public class GuestRequestFullStateMessageHandler : MessageHandler
    {
        public GuestRequestFullStateMessageHandler(AppWebSocketBehavior serviceBehavior) : base(serviceBehavior)
        {
        }

        public Message GuestRequestFullState(GuestRequestFullStateMessage message)
        {
            Player player = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(player, "requires a user session", _websocket);

            GameInstance gameInstance = _websocket.FindPlayerGameInstance(player);
            Assert.ThrowIfFalse(player.Id != gameInstance.HostPlayer.Id, "only guests should ask for full state", _websocket);
            _websocket.Send(new GuestRequestFullStateMessage(), gameInstance.Id);
            return null;
        }
    }
}
