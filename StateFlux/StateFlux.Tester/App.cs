using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Timers;
using Newtonsoft.Json;
using StateFlux;
using StateFlux.Model;
using WebSocketSharp;
using WebSocketSharp.Net;

namespace StateFlux.Tester
{
    public class App
    {
        private const string _url = "ws://localhost:8888/Service";
        private const string CurrentPlayerFilename = "currentplayer.json";
        private const double _timerTick = 1000d;
        private PlayerClientInfo _currentPlayer;
        private AuthenticatedMessage authenticated;
        private WebSocket _webSocket;

        public App()
        {
            Console.WriteLine("StateFluxClient started " + DateTime.Now);
            _currentPlayer = new PlayerClientInfo();
        }

        public void Run()
        {
            while(true)
            {
                if (!File.Exists(CurrentPlayerFilename))
                {
                    do
                    {
                        ConnectForAuthentication();
                    }
                    while (authenticated == null || (authenticated != null && authenticated.Status != AuthenticationStatus.Authenticated));
                }
                DemoCommands();
            }
        }

        public void ConnectForAuthentication()
        {
            Console.WriteLine("Enter username:");
            string username = Console.ReadLine();

            try
            {
                _webSocket = new WebSocket(_url);

                // this delegate function will be called when we receive either a response or a broadcast
                _webSocket.OnMessage += (object source, MessageEventArgs e) =>
                {
                    authenticated = JsonConvert.DeserializeObject<AuthenticatedMessage>(e.Data.ToString());
                    if (authenticated.MessageType != MessageTypeNames.Authenticated)
                    {
                        File.Delete(CurrentPlayerFilename);
                        authenticated = null;
                        _currentPlayer = null;
                        return;
                    }

                    if (authenticated.Status != AuthenticationStatus.Authenticated)
                    {
                        Console.WriteLine($"{authenticated.Status} : {authenticated.StatusMessage}");
                        return;
                    }

                    _currentPlayer = new PlayerClientInfo
                    {
                        Name = authenticated.PlayerName,
                        SessionId = authenticated.SessionId
                    };
                    File.WriteAllText(CurrentPlayerFilename, JsonConvert.SerializeObject(_currentPlayer, Formatting.Indented));
                    Console.WriteLine($"Player is authenticated  as {_currentPlayer.Name} with sessionId = {_currentPlayer.SessionId}");
                };

                _webSocket.Connect();

                AuthenticateMessage requestMessage = new AuthenticateMessage();
                requestMessage.PlayerName = username;
                string msg = JsonConvert.SerializeObject(requestMessage);
                _webSocket.Send(msg);

                // wait for the delegate function to set _currentPlayer
                int limit = 10;
                while (limit > 0 && authenticated == null)
                {
                    Thread.Sleep(1000);
                    limit--;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                File.Delete(CurrentPlayerFilename);
                authenticated = null;
                _currentPlayer = null;
            }


            _webSocket.Close();
            _webSocket = null;
        }

        public void DemoCommands()
        {
            _currentPlayer = JsonConvert.DeserializeObject<PlayerClientInfo>(File.ReadAllText(CurrentPlayerFilename));
            Console.WriteLine($"Player has saved session as {_currentPlayer.Name} with sessionId = {_currentPlayer.SessionId}");

            _webSocket = new WebSocket(_url);
            _webSocket.OnMessage += (object source, MessageEventArgs e) =>
            {
                string msgTxt = e.Data.ToString();
                Message responseMessage = JsonConvert.DeserializeObject<Message>(msgTxt);

                if (responseMessage.MessageType == MessageTypeNames.ChatSaid)
                {
                    OnChatSaid(JsonConvert.DeserializeObject<ChatSaidMessage>(msgTxt));
                }
                else if (responseMessage.MessageType == MessageTypeNames.PlayerListing)
                {
                    OnPlayerListing(JsonConvert.DeserializeObject<PlayerListingMessage>(msgTxt));
                }
                else if (responseMessage.MessageType == MessageTypeNames.GameInstanceCreated)
                {
                    OnGameInstanceCreated(JsonConvert.DeserializeObject<GameInstanceCreatedMessage>(msgTxt));
                }
                else if (responseMessage.MessageType == MessageTypeNames.JoinedGameInstance)
                {
                    OnJoinedGameInstance(JsonConvert.DeserializeObject<JoinedGameInstanceMessage>(msgTxt));
                }
                else if (responseMessage.MessageType == MessageTypeNames.RequestFullState)
                {
                    OnRequestFullState(JsonConvert.DeserializeObject<RequestFullStateMessage>(msgTxt));
                }
                else if (responseMessage.MessageType == MessageTypeNames.StateChanged)
                {
                    OnStateChanged(JsonConvert.DeserializeObject<StateChangedMessage>(msgTxt));
                }
                else if (responseMessage.MessageType == MessageTypeNames.ServerError)
                {
                    OnServerError(JsonConvert.DeserializeObject<ServerErrorMessage>(msgTxt));
                }
            };

            try
            {
                _webSocket.SetCookie(new Cookie(MessageConstants.SessionCookieName, _currentPlayer.SessionId));
                _webSocket.Connect();
            }
            catch (Exception e)
            {
                File.Delete(App.CurrentPlayerFilename);
                _currentPlayer = null;
                Console.WriteLine(e.Message);
            }

            while (_currentPlayer != null)
            {
                try
                {
                    Console.WriteLine("Enter a command:");
                    string command = Console.ReadLine();

                    if (command.StartsWith("say ", true, null))
                    {
                        ChatSayMessage chatSayMessage = new ChatSayMessage();
                        chatSayMessage.say = command.Substring(4);
                        _webSocket.Send(JsonConvert.SerializeObject(chatSayMessage));
                    }
                    else if (command.StartsWith("list", true, null))
                    {
                        PlayerListMessage playerListMessage = new PlayerListMessage();
                        _webSocket.Send(JsonConvert.SerializeObject(playerListMessage));
                    }
                    else if (command.StartsWith("rename", true, null))
                    {
                        string newName = command.Substring("rename".Length + 1);
                        PlayerRenameMessage playerRenameMessage = new PlayerRenameMessage();
                        playerRenameMessage.Name = newName;
                        _currentPlayer.Name = newName;
                        _webSocket.Send(JsonConvert.SerializeObject(playerRenameMessage));
                        File.WriteAllText(CurrentPlayerFilename, JsonConvert.SerializeObject(_currentPlayer));
                    }
                    else if (command.StartsWith("create", true, null))
                    {
                        string[] parts = command.Split(' ');
                        CreateGameInstanceMessage createGameInstanceMessage = new CreateGameInstanceMessage();
                        createGameInstanceMessage.GameName = parts[1];
                        createGameInstanceMessage.InstanceName = parts[2];
                        _webSocket.Send(JsonConvert.SerializeObject(createGameInstanceMessage));
                    }
                    else if (command.StartsWith("join", true, null))
                    {
                        string[] parts = command.Split(' ');
                        JoinGameInstanceMessage joinGameInstanceMessage = new JoinGameInstanceMessage();
                        joinGameInstanceMessage.GameName = parts[1];
                        joinGameInstanceMessage.InstanceName = parts[2];
                        _webSocket.Send(JsonConvert.SerializeObject(joinGameInstanceMessage));
                    }
                    else if (command.StartsWith("sync", true, null))
                    {
                        RequestFullStateMessage message = new RequestFullStateMessage();
                        _webSocket.Send(JsonConvert.SerializeObject(message));
                    }
                    else if (command.StartsWith("move", true, null))
                    {
                        StateChangeMessage message = new StateChangeMessage();
                        StateChange batch = new StateChange();
                        batch.changes = new List<Change2d>();

                        Random rnd = new Random();
                        int randomCount = rnd.Next() % 100;
                        for (int index = 0; index < randomCount; index++)
                        {
                            Change2d change = new Change2d();
                            change.Event = ChangeEvent.Created;
                            change.ObjectID = rnd.Next().ToString();
                            change.Transform = new Transform2d
                            {
                                Pos = new Vector2d { X = rnd.NextDouble() * 100.0, Y = rnd.NextDouble() % 100.0 },
                                Vel = new Vector2d { X = rnd.NextDouble() % 100.0, Y = rnd.NextDouble() % 100.0 },
                                Rot = rnd.NextDouble() * 380.0,
                                Scale = 1.0
                            };
                            batch.changes.Add(change);
                        }
                        message.Payload = batch;
                        _webSocket.Send(JsonConvert.SerializeObject(message));
                    }
                    else if (command.StartsWith("help", true, null))
                    {
                        Console.WriteLine("FIXME: add a list of commands");
                    }
                    else
                    {
                        Console.WriteLine("unknown command");
                    }
                }
                catch (Exception e)
                {
                    File.Delete(App.CurrentPlayerFilename);
                    this._currentPlayer = null; // causes re-login
                    Console.WriteLine(e.Message);
                }
            }

            try
            {
                _webSocket.Close();
                _webSocket = null;
            }
            catch (Exception e)
            {
                File.Delete(App.CurrentPlayerFilename);
                this._currentPlayer = null; // causes re-login
                Console.WriteLine(e.Message);
            }
        }

        private void OnRequestFullState(RequestFullStateMessage requestFullStateMessage)
        {
            Console.WriteLine($"{DateTime.Now}: server has requested client owner send a full StateChange!");
        }

        private void OnServerError(ServerErrorMessage serverErrorMessage)
        {
            if(serverErrorMessage.Error.Contains("Requires a user session"))
            {
                File.Delete(App.CurrentPlayerFilename);
                this.authenticated = null; // causes re-login
            }
            Console.WriteLine($"{DateTime.Now}: server error '{serverErrorMessage.Error}'!");
        }

        private void OnStateChanged(StateChangedMessage stateChangedMessage)
        {
            string msgDump = JsonConvert.SerializeObject(stateChangedMessage);
            Console.WriteLine($"{DateTime.Now}: received a state changed : { msgDump }");
        }

        void OnChatSaid(ChatSaidMessage message)
        {
            Console.WriteLine($"{DateTime.Now}: player {message.PlayerName} said {message.Say}");
        }

        void OnPlayerListing(PlayerListingMessage message)
        {
            Console.WriteLine("Players Online:");
            Console.WriteLine("---------------");
            foreach (Player player in message.Players)
            {
                Console.Write($"Player {player.Name}");
                if(player.GameInstance != null)
                {
                    Console.WriteLine($" in game instance {JsonConvert.SerializeObject(player)}");
                }
                else
                {
                    Console.WriteLine(" not in a game instance");
                }
            };
        }

        void OnGameInstanceCreated(GameInstanceCreatedMessage message)
        {
            Console.WriteLine("Game instance created:");
            Console.WriteLine("---------------");
            Console.WriteLine($"{JsonConvert.SerializeObject(message)}");
        }


        void OnJoinedGameInstance(JoinedGameInstanceMessage message)
        {
            Console.WriteLine("Player joined game instance:");
            Console.WriteLine("---------------");
            Console.WriteLine($"{JsonConvert.SerializeObject(message)}");
        }


    }
}
