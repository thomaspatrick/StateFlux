using System.Collections.Generic;

namespace StateFlux.Model
{
    public class Message
    {
        public string MessageType { get; set; }
    }

    public class AuthenticateMessage : Message
    {
        public AuthenticateMessage()
        {
            MessageType = MessageTypeNames.Authenticate;
        }

        public string PlayerName { get; set; }
    }

    public enum AuthenticationStatus { Authenticated, BadUser, BadPassword };
    public class AuthenticatedMessage : Message
    {
        public AuthenticatedMessage()
        {
            MessageType = MessageTypeNames.Authenticated;
        }

        public AuthenticationStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public string PlayerName { get; set; }
        public string SessionId { get; set; }
    }

    public class ChatSayMessage : Message
    {
        public ChatSayMessage()
        {
            MessageType = MessageTypeNames.ChatSay;
        }

        public string say { get; set; }
    }

    public class ChatSaidMessage : Message
    {
        public ChatSaidMessage()
        {
            MessageType = MessageTypeNames.ChatSaid;
        }

        public string PlayerName { get; set; }
        public string Say { get; set; }
    }

    public class PlayerListMessage : Message
    {
        public PlayerListMessage()
        {
            MessageType = MessageTypeNames.PlayerList;
        }
    }

    public class PlayerListingMessage : Message
    {
        public PlayerListingMessage()
        {
            MessageType = MessageTypeNames.PlayerListing;
        }

        public List<Player> Players { get; set; }
    }

    public class PlayerRenameMessage : Message
    {
        public PlayerRenameMessage()
        {
            MessageType = MessageTypeNames.PlayerRename;
        }

        public string Name { get; set; }
    }

    public class StateChangeMessage : Message
    {
        public StateChangeMessage()
        {
            MessageType = MessageTypeNames.StateChange;
        }

        public StateChange Payload { get; set; }
    }

    public class StateChangedMessage : Message
    {
        public StateChangedMessage()
        {
            MessageType = MessageTypeNames.StateChanged;
        }

        public StateChange Payload { get; set; }
    }

    public class RequestFullStateMessage : Message
    {
        public RequestFullStateMessage()
        {
            MessageType = MessageTypeNames.RequestFullState;
        }
    }

    public class CreateGameInstanceMessage : Message
    {
        public CreateGameInstanceMessage()
        {
            MessageType = MessageTypeNames.CreateGameInstance;
        }

        public string GameName;
        public string InstanceName;
    }

    public class GameInstanceCreatedMessage : Message
    {
        public GameInstanceCreatedMessage()
        {
            MessageType = MessageTypeNames.GameInstanceCreated;
        }

        public GameInstance GameInstance { get; set; }
    }

    public class GameInstanceListMessage : Message
    {
        public GameInstanceListMessage()
        {
            MessageType = MessageTypeNames.GameInstanceList;
        }
    }

    public class GameInstanceListingMessage : Message
    {
        public GameInstanceListingMessage()
        {
            MessageType = MessageTypeNames.GameInstanceListing;
        }

        public List<GameInstance> GameInstances { get; set; }
    }

    public class JoinGameInstanceMessage : Message
    {
        public JoinGameInstanceMessage()
        {
            MessageType = MessageTypeNames.JoinGameInstance;
        }

        public string GameName { get; set; }
        public string InstanceName { get; set; }
    }

    public class JoinedGameInstanceMessage : Message
    {
        public JoinedGameInstanceMessage()
        {
            MessageType = MessageTypeNames.JoinedGameInstance;
        }

        public Player Player { get; set; } 
    }

    public class LeaveGameInstanceMessage : Message
    {
        public LeaveGameInstanceMessage()
        {
            MessageType = MessageTypeNames.LeaveGameInstance;
        }

        public string GameName { get; set; }
        public string InstanceName { get; set; }
    }

    public class LeftGameInstanceMessage : Message
    {
        public LeftGameInstanceMessage()
        {
            MessageType = MessageTypeNames.LeftGameInstance;
        }

        public Player Player { get; set; }
    }

    public class StartGameInstanceMessage : Message
    {
        public StartGameInstanceMessage()
        {
            MessageType = MessageTypeNames.StartGameInstance;
        }

        public GameInstance GameInstance { get; set; }
    }

    public class ServerErrorMessage : Message
    {
        public ServerErrorMessage()
        {
            MessageType = MessageTypeNames.ServerError;
        }

        public string Error { get; set; }
    }
}
