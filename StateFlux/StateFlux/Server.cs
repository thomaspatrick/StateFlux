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

        private const string _dateFormat = "yyyy-MM-ddTHH:mm:ss";

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

            Game game = new Game
            {
                Name = "AssetCollapse",
                Description = "Asset Collapse Game",
                MinPlayers = 2
            };
            Games.Add(game);
            _timer = new Timer(this.Tick, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
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
            player.GameInstanceRef = null;
            gameInstance.Players.Remove(player);
            if(playerHosting)
            {
                gameInstance.Game.Instances.Remove(gameInstance);
            }
        }

        public GameInstance RemoveHostedGameInstance(Player hostPlayer)
        {
            foreach(var game in Games)
            {
                GameInstance found = game.Instances.FirstOrDefault(i=>i.HostPlayer==hostPlayer);
                game.Instances.Remove(found);
            }
            hostPlayer.GameInstanceRef = null;
            playerRepository.UpdatePlayer(hostPlayer);
            return null;
        }

        private GameInstance LookupInstance(Guid id)
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

        public void StartGameInstance(Guid gameInstanceId)
        {
            GameInstance gameInstance = LookupInstance(gameInstanceId);
            gameInstance.State = GameInstanceState.Starting;
        }

        private void Tick(object? state)
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

                if (AnyGameInstances())
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
