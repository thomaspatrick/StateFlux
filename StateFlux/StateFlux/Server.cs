using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using StateFlux.Model;
using StateFlux.Model.Repository;

namespace StateFlux.Service
{
    public class Server
    {
        public List<Player> Players { get; set; }
        public List<Game> Games { get; set; }
        public List<ChatSaid> Chat { get; set; }
        public IPlayerRepository playerRepository = new PlayerRepository();
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

        static public Server Instance { get; } = new Server();
    }
}
