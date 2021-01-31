using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;
using StateFlux.Model;

namespace StateFlux.Service
{
    public class AuthenticationHandler : MessageHandler
    {
        private const int MaxPlayerNameLen = 50;

        public AuthenticationHandler(AppWebSocketBehavior serviceBehavior) : base(serviceBehavior)
        {
        }

        public AuthenticatedMessage Authenticate(AuthenticateMessage message)
        {
            AuthenticatedMessage response = new AuthenticatedMessage();
            if(String.IsNullOrWhiteSpace(message.PlayerName))
            {
                response.Status = AuthenticationStatus.BadUser;
                _websocket.LogMessage($"Player failed authenticate: bad PlayerName");
                return response;
            }
            Player player;
            try
            {
                player = _websocket.GetCurrentSessionPlayer();
            }
            catch(Exception e)
            {
                _websocket.LogMessage($"Caught exception from GetCurrentPlayer, {e.Message}");
                player = null;
            }

            if(player == null)
            {
                Player prev = Server.Instance.Players.FirstOrDefault(p => p.Name == message.PlayerName);
                if(prev != null)
                {
                    _websocket.LogMessage($"Found existing player matching name {message.PlayerName}, removing...");
                    Server.Instance.Players.Remove(prev);
                }
                player = _websocket.CreatePlayerSession(message.PlayerName.Truncate(MaxPlayerNameLen));
            }

            response.PlayerId = player.Id.ToString();
            response.PlayerName = player.Name;
            response.SessionId = player.SessionData.SessionId;
            _websocket.LogMessage($"Player authenticated: {JsonConvert.SerializeObject(response)}");
            return response;
        }
    }
}
