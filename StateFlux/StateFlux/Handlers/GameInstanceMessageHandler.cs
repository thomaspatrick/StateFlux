using System;
using System.Collections.Generic;
using System.Linq;
using StateFlux.Model;

namespace StateFlux.Service.Handlers
{
    public class GameInstanceMessageHandler : MessageHandler
    {
        public GameInstanceMessageHandler(AppWebSocketBehavior serviceBehavior) : base(serviceBehavior)
        {
        }


        public void CreateGameInstance(CreateGameInstanceMessage message)
        {
            var currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);

            var game = _server.Games.FirstOrDefault(g => g.Name == message.GameName);
            if (game == null)
            {
                string msg = $"Player tried to create game instance of game '{message.GameName}' but it does not exist!";
                _websocket.LogMessage(msg);
                throw new Exception(msg);
            }

            if(game.Instances.Any(g => g.HostPlayer.Id == currentPlayer.Id))
            {
                string msg = $"Player can only create one game instance at a time.";
                _websocket.LogMessage(msg);
                throw new Exception(msg);
            }

            if (game.Instances.Any(g => g.Name == message.InstanceName))
            {
                string msg = $"Player tried to create game instance called '{message.InstanceName}' but that is already taken for game {message.GameName}!";
                _websocket.LogMessage(msg);
                throw new Exception(msg);
            }

            var gameInstance = Server.Instance.HostGameInstance(currentPlayer, game, message.InstanceName);
            _websocket.LogMessage($"Player creates game instance of game '{message.GameName}' and calls it '{message.InstanceName}'");

            var broadcastMessage = new GameInstanceCreatedMessage()
            {
                GameInstance = gameInstance
            };
            _websocket.Broadcast(broadcastMessage, new GameInstanceRef(gameInstance), true);
        }

        public GameInstanceListingMessage GameInstanceList(GameInstanceListMessage gameInstanceListMessage)
        {
            var currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);

            var instances = new List<GameInstance>();
            _server.Games.ForEach(g => instances.AddRange(g.Instances));

            var response = new GameInstanceListingMessage
            {
                GameInstances = instances
            };
            return response;
        }

        public void JoinGameInstance(JoinGameInstanceMessage message)
        {
            var currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);

            var game = Server.Instance.Games.FirstOrDefault(g => g.Name == message.GameName);
            Assert.ThrowIfNull(game, $"game {game.Name} not found", _websocket);

            var gameInstance = game.Instances.FirstOrDefault(g => g.Name == message.InstanceName);
            Assert.ThrowIfNull(gameInstance, $"game instance {game.Name}:{gameInstance.Name} not found", _websocket);
            if (gameInstance != null)
            {
                Server.Instance.JoinGameInstance(gameInstance, _websocket.GetCurrentSessionPlayer());
                _websocket.LogMessage($"Player joins game instance '{message.GameName}:{message.InstanceName}'");
                var broadcastMessage = new GameInstanceJoinedMessage() { Player = currentPlayer };
                _websocket.Broadcast(broadcastMessage, null, true);
                if(gameInstance.State == GameInstanceState.WaitingForPlayers && gameInstance.Players.Count >= gameInstance.Game.MinPlayers)
                {
                    Server.Instance.StartGameInstance(gameInstance.Id);
                }
            }
        }

        public void LeaveGameInstance(LeaveGameInstanceMessage message)
        {
            var currentPlayer = _websocket.GetCurrentSessionPlayer();
            Assert.ThrowIfNull(currentPlayer, "requires a user session", _websocket);

            var game = Server.Instance.Games.FirstOrDefault(g => g.Name == message.GameName);
            Assert.ThrowIfNull(game, $"game {game.Name} not found", _websocket);

            var gameInstance = game.Instances.FirstOrDefault(g => g.Name == message.InstanceName);
            Assert.ThrowIfNull(gameInstance, $"game instance {game.Name}:{message.InstanceName} not found", _websocket);
            if (gameInstance != null)
            {
                Server.Instance.LeaveGameInstance(gameInstance, _websocket.GetCurrentSessionPlayer());
                _websocket.LogMessage($"Player left game instance '{message.GameName}:{message.InstanceName}'");
                var broadcastMessage = new GameInstanceLeftMessage() { Player = currentPlayer, GameName = message.GameName, InstanceName = message.InstanceName };
                _websocket.Broadcast(broadcastMessage, null, true);
            }
        }
    }
}
