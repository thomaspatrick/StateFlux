﻿using System;
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
            Player player = _websocket.GetCurrentSessionPlayer();
            if(player == null)
            {
                player = _websocket.CreatePlayerSession(message.PlayerName.Truncate(MaxPlayerNameLen));
            }

            response.PlayerName = player.Name;
            response.SessionId = player.SessionId;
            _websocket.LogMessage($"Player authenticated: {JsonConvert.SerializeObject(response)}");
            return response;
        }
    }
}
