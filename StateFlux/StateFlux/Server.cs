using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using StateFlux.Model;
using StateFlux.Model.Repository;
using WebSocketSharp.Server;

namespace StateFlux.Service
{
    public class Server
    {

        public List<Player> Players { get; set; }
        public List<Game> Games { get; set; }
        public List<ChatSaid> Chat { get; set; }

        private const string _dateFormat = "yyyy-MM-ddTHH:mm:ss:ffff";

        private WebSocketSessionManager _sessionManager;
        public WebSocketSessionManager SessionManager
        {
            get => _sessionManager;
            set
            {
                if (_sessionManager == null) _sessionManager = value;
            }
        }

        public IPlayerRepository playerRepository = new PlayerRepository();
        private Timer _timer;
        private Server()
        {
            Players = new List<Player>();
            Games = new List<Game>();
            Chat = new List<ChatSaid>();
            _timer = new Timer(this.Tick, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        public GameInstance HostGameInstance(Player hostPlayer, Game game, string gameInstanceName)
        {
            GameInstance gameInstance = new GameInstance(game, gameInstanceName);
            gameInstance.HostPlayer = hostPlayer;
            game.Instances.Add(gameInstance);
            JoinGameInstance(gameInstance, hostPlayer);
            return gameInstance;
        }


        public void JoinGameInstance(GameInstance gameInstance, Player player)
        {
            bool playerAlreadyIn = gameInstance.Players.Any(p => p.Id == player.Id);
            if (playerAlreadyIn)
            {
                throw new Exception($"Player {player.Name} already in game instance {gameInstance.Game.Name}:{gameInstance.Name}");
            }
            player.GameInstanceRef = new GameInstanceRef(gameInstance);
            gameInstance.Players.Add(player);
        }

        public void LeaveGameInstance(GameInstance gameInstance, Player player)
        {
            bool playerAlreadyIn = gameInstance.Players.Any(p => p.Id == player.Id);
            bool playerHosting = gameInstance.HostPlayer.Id == player.Id;
            if (!playerAlreadyIn)
            {
                throw new Exception($"Player {player.Name} not in game instance {gameInstance.Game.Name}:{gameInstance.Name}");
            }
            if(playerHosting)
            {
                RemoveGameInstance(player);
            }
            player.GameInstanceRef = null;
            gameInstance.Players.Remove(player);
        }

        public GameInstance RemoveGameInstance(Player player)
        {
            bool removed = false;

            foreach(var game in Games)
            {
                foreach(var instance in game.Instances)
                {
                    if(instance.Players.Any(p => p.Id == player.Id))
                    {
                        foreach(var p in instance.Players)
                        {
                            p.GameInstanceRef = null;
                            playerRepository.UpdatePlayer(p);
                        }
                        removed = game.Instances.Remove(instance);
                        if(removed) LogMessage($"Removed game instance {instance.Id} ({instance.Game.Name}.{instance.Name})");
                        break;
                    }
                }
            }
            return null;
        }

        public GameInstance LookupInstance(string    id)
        {
            foreach (var game in Games)
            {
                GameInstance found = game.Instances.FirstOrDefault(i => i.Id == id);
                if(found != null)
                {
                    return found;
                }
            }
            return null;
        }

        public void StartGameInstance(string gameInstanceId)
        {
            GameInstance gameInstance = LookupInstance(gameInstanceId);
            gameInstance.State = GameInstanceState.Starting;
            var startMessage = new GameInstanceStartMessage()
            {
                GameInstance = new GameInstanceRef(gameInstance),
                Host = gameInstance.HostPlayer,
                Guests = gameInstance.Players
            };
                BroadcastSystemMessage(startMessage);
        }

        private void Tick(object state)
        {
            if (SessionManager == null) return;

            if(Players.Any())
            {
                var playerListingMessage = new PlayerListingMessage()
                {
                    MessageType = MessageTypeNames.PlayerListing,
                    Players = this.Players
                };

                BroadcastSystemMessage(playerListingMessage);

                //if (AnyGameInstances())
                {
                    var instances = new List<GameInstance>();
                    Games.ForEach(g => instances.AddRange(g.Instances));

                    var gameInstanceListingMessage = new GameInstanceListingMessage
                    {
                        GameInstances = instances
                    };
                    BroadcastSystemMessage(gameInstanceListingMessage);
                }
            }
        }

        public void BroadcastSystemMessage(Message message)
        {
            try
            {
                string msg = JsonConvert.SerializeObject(message);
                foreach (Player player in Players)
                {
                    IWebSocketSession session;
                    if (SessionManager.TryGetSession(player.SessionData.WebsocketSessionId, out session))
                    {
                        session.Context.WebSocket.Send(msg);
                    }
                }
            }
            catch (Exception e)
            {
                LogMessage(e.Message);
            }
        }

        public void LogMessage(string message)
        {
            string playerName = "unknown";
            Console.WriteLine($"{DateTime.Now.ToString(_dateFormat)},{playerName},{message}");
        }

        private bool AnyGameInstances()
        {
            foreach (var game in Games)
            {
                if (game.Instances.Any())
                {
                    return true;
                }
            }
            return false;
        }

        static public Server Instance { get; } = new Server();
    }
}
