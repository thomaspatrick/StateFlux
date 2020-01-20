using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StateFlux.Model;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Net;

namespace StateFlux.Client
{
    public class Client
    {
        private string _sessionFilePath = Application.dataPath + "/" + "currentplayer.json";

        private PlayerClientInfo _currentPlayer;
        private WebSocket _webSocket;
        private Task _task;
        private bool ShouldExit { get; set; }
        private ConcurrentQueue<Message> _requests = new ConcurrentQueue<Message>();
        private ConcurrentQueue<Message> _responses = new ConcurrentQueue<Message>();

        public string Endpoint { get; set; }
        public string RequestedUsername { get; set; }
        public string SessionSaveFilename { get; set; }

        public WebSocketState ReadyState {
            get {
                lock(this) {
                    return _webSocket != null ? _webSocket.ReadyState : WebSocketState.Closed;
                }
            }
        }

        public bool SocketOpenWithIdentity
        {
            get
            {
                lock (this)
                {
                    return ReadyState == WebSocketState.Open && _currentPlayer != null;
                }
            }
        }

        public void SendRequest(Message message)
        {
            _requests.Enqueue(message);
        }

        public Message ReceiveResponse()
        {
            Message message = null;
            if(_responses.TryDequeue(out message))
            {
                return message;
            }
            return null;
        }

        public void Start()
        {
            _task = new Task(MainAction);
            _task.Start();
        }

        public void Stop()
        {
            lock(this)
            {
                ShouldExit = true;
            }
            Log("Stop called...");
        }

        private void MainAction()
        {
            while(!ShouldExit)
            {
                bool needsAuth = (_currentPlayer == null);

                if (needsAuth)
                {
                    if (HasSavedSession())
                    {
                        _currentPlayer = LoadSession();
                    }
                    else
                    {
                        Authenticate();
                    }
                }
                else
                {
                    ProcessMessages();
                }
            }
        }

        private void ProcessMessages()
        {
            Log($"Starting ProcessMessages as {_currentPlayer.Name} with sessionId = {_currentPlayer.SessionId}");

            WebSocketSharp.ErrorEventArgs errorEventArgs = null;
            try
            {
                lock(this)
                {
                    _webSocket = new WebSocket(Endpoint);
                    _webSocket.OnOpen += (object source, EventArgs e) =>
                    {
                        Log("Websocket.OnOpen");
                    };
                    _webSocket.OnMessage += ProcessMessagesHandler;
                    _webSocket.OnError += (object source, WebSocketSharp.ErrorEventArgs e) =>
                    {
                        errorEventArgs = e;
                        Log($"Websocket.OnError - {e.Message}");
                        if (e.Message.Contains("requires a user session"))
                        {
                            Log("websocket OnError - resetting saved session and reconnecting");
                            lock(this)
                            {
                                ResetSavedSession();
                                _webSocket.Close();
                            }
                        }
                    };
                    _webSocket.OnClose += (object source, CloseEventArgs e) =>
                    {
                        Log($"Websocket.OnClose - {e.Reason}");
                    };

                    _webSocket.SetCookie(new Cookie(MessageConstants.SessionCookieName, _currentPlayer.SessionId));
                    _webSocket.Connect();
                }
            }
            catch (Exception e)
            {
                ResetSavedSession();
                Log(e.Message);
                return;
            }

            Log("Waiting for socket to open...");
            int tries = 40;
            while (ReadyState != WebSocketState.Open && tries>0)
            {
                Thread.Sleep(25);
                tries--;
            }

            if(ReadyState != WebSocketState.Open)
                Log("Gave up waiting for socket to open...");
            else
            {
                Log("Socket open! Working from request queue...");
            }


            while (!ShouldExit && ReadyState == WebSocketState.Open)
            {

                try
                {
                    Message message;
                    if (_requests.TryDequeue(out message))
                    {
                        _webSocket.Send(JsonConvert.SerializeObject(message));
                    }
                }
                catch (Exception e)
                {
                    Log(e.Message);
                }
            }
            Log("Stopped working from request queue...");

            try
            {
                if (ReadyState == WebSocketState.Open)
                {
                    Log("Closing an open socket");

                    lock(this)
                    {
                        _webSocket.Close();
                        _webSocket = null;
                    }
                }
            }
            catch (Exception e)
            {
                Log(e.Message);
            }
            Log("Exiting ProcessMessages");
        }

        private void ProcessMessagesHandler(object source, MessageEventArgs e)
        {
            string msgTxt = e.Data.ToString();
            Message responseMessage = JsonConvert.DeserializeObject<Message>(msgTxt);
            Message mappedMessage = null;

            if (responseMessage.MessageType == MessageTypeNames.ChatSaid)
            {
                mappedMessage = JsonConvert.DeserializeObject<ChatSaidMessage>(msgTxt);
            }
            else if (responseMessage.MessageType == MessageTypeNames.PlayerListing)
            {
                mappedMessage = JsonConvert.DeserializeObject<PlayerListingMessage>(msgTxt);
            }
            else if (responseMessage.MessageType == MessageTypeNames.GameInstanceCreated)
            {
                mappedMessage = JsonConvert.DeserializeObject<GameInstanceCreatedMessage>(msgTxt);
            }
            else if (responseMessage.MessageType == MessageTypeNames.JoinedGameInstance)
            {
                mappedMessage = JsonConvert.DeserializeObject<JoinedGameInstanceMessage>(msgTxt);
            }
            else if (responseMessage.MessageType == MessageTypeNames.GameInstanceListing)
            {
                mappedMessage = JsonConvert.DeserializeObject<GameInstanceListingMessage>(msgTxt);
            }
            else if (responseMessage.MessageType == MessageTypeNames.RequestFullState)
            {
                mappedMessage = JsonConvert.DeserializeObject<RequestFullStateMessage>(msgTxt);
            }
            else if (responseMessage.MessageType == MessageTypeNames.StateChanged)
            {
                mappedMessage = JsonConvert.DeserializeObject<StateChangedMessage>(msgTxt);
            }
            else if (responseMessage.MessageType == MessageTypeNames.ServerError)
            {
                ServerErrorMessage error = JsonConvert.DeserializeObject<ServerErrorMessage>(msgTxt);
                OnServerError(error);
                mappedMessage = error;
            }

            if (mappedMessage != null)
            {
                _responses.Enqueue(mappedMessage);
            }
        }

        private void OnServerError(ServerErrorMessage serverErrorMessage)
        {
            lock(this)
            {
                if (serverErrorMessage.Error.Contains("Requires a user session"))
                {
                    ResetSavedSession();
                }
                if (_webSocket!=null) _webSocket.Close();
            }
            Log($"{DateTime.Now}: server error '{serverErrorMessage.Error}'!");
        }

        private bool Authenticate()
        {
            try
            {
                lock(this)
                {
                    if(string.IsNullOrWhiteSpace(RequestedUsername))
                    {
                        return false;
                    }
                    _webSocket = new WebSocket(Endpoint);
                    _webSocket.OnMessage += HandleAuthResponseMessage;
                    _webSocket.Connect();
                }

                int tries = 40; // wait for 10 seconds
                while(ReadyState != WebSocketState.Open && tries > 0)
                {
                    Thread.Sleep(250);
                    tries--;
                }

                if (ReadyState != WebSocketState.Open)
                {
                    throw new Exception("Websocket Timeout");
                }

                AuthenticateMessage requestMessage = new AuthenticateMessage();
                requestMessage.PlayerName = RequestedUsername;
                string msg = JsonConvert.SerializeObject(requestMessage);
                lock(this)
                {
                    if(_webSocket != null)
                    {
                        _webSocket.Send(msg);
                    }
                }

                // wait for the delegate function to set _currentPlayer
                tries = 10;
                while (_currentPlayer == null && tries > 0)
                {
                    Thread.Sleep(1000);
                    tries--;
                }
            }
            catch (Exception e)
            {
                Log(e.ToString());
                ResetSavedSession();
            }

            lock(this)
            {
                _webSocket.Close();
                _webSocket = null;
            }

            return (_currentPlayer != null);
        }

        private void HandleAuthResponseMessage(object source, MessageEventArgs e)
        {
            AuthenticatedMessage authenticated = JsonConvert.DeserializeObject<AuthenticatedMessage>(e.Data.ToString());

            if (authenticated.MessageType != MessageTypeNames.Authenticated)
            {
                ResetSavedSession();
                return;
            }

            if (authenticated.Status != AuthenticationStatus.Authenticated)
            {
                Log($"Player login rejected {authenticated.Status} : {authenticated.StatusMessage}");
                ResetSavedSession();
                return;
            }

            _currentPlayer = new PlayerClientInfo
            {
                Name = authenticated.PlayerName,
                SessionId = authenticated.SessionId
            };

            SaveSession();
            Log($"Player is authenticated as {_currentPlayer.Name} with sessionId = {_currentPlayer.SessionId}");
        }

        private void SaveSession()
        {
            File.WriteAllText(_sessionFilePath, JsonConvert.SerializeObject(_currentPlayer, Formatting.Indented));
        }

        private bool HasSavedSession()
        {
            return File.Exists(_sessionFilePath);
        }

        private PlayerClientInfo LoadSession()
        {
            return JsonConvert.DeserializeObject<PlayerClientInfo>(File.ReadAllText(_sessionFilePath));
        }

        private void ResetSavedSession()
        {
            File.Delete(_sessionFilePath);
            _currentPlayer = null;
        }

        private void Log(string msg)
        {
            Debug.Log(msg);
        }
    }
}
