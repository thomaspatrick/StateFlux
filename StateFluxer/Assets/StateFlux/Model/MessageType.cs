﻿
namespace StateFlux.Model
{
    public class MessageTypeNames
    {
        // login request messages
        public const string Authenticate = "Authenticate";
        // login response message
        public const string Authenticated = "Authenticated";

        // lobby request messages
        public static string ChatSay = "ChatSay";
        public static string PlayerList = "PlayerList";
        public static string PlayerRename = "PlayerRename";

        // lobby broadcast response messages
        public static string ChatSaid = "ChatSaid";
        public static string PlayerListing = "PlayerListing";

        // gameinstance request messages
        public static string CreateGameInstance = "CreateGameInstance";
        public static string JoinGameInstance = "JoinGameInstance";
        public static string LeaveGameInstance = "LeaveGameInstance";
        public static string GameInstanceList = "GameInstanceList";

        // gameinstance response messages
        public static string GameInstanceListing = "GameInstanceListing";

        // gameinstance broadcast messages
        public static string GameInstanceCreated = "GameInstanceCreated";
        public static string GameInstanceJoined = "GameInstanceJoined";
        public static string GameInstanceLeft = "GameInstanceLeft";
        public static string GameInstanceGetReady = "GameInstanceGetReady";
        public static string GameInstanceStart = "GameInstanceStart";
        public static string GameInstanceStopped = "GameInstanceStopped";

        public static string MiceChange = "MiceChange";
        public static string MiceChanged = "MiceChanged";

        // state request messages
        public static string HostStateChange = "HostStateChange";
        public static string HostCommandChange = "HostCommandChange";
        public static string GuestCommandChange = "GuestCommandChange";
        public static string GuestInputChange = "GuestInputChange";
        public static string GuestRequestFullState = "GuestRequestFullState";

        // state broadcast response messages
        public static string HostStateChanged = "HostStateChanged";
        public static string HostCommandChanged = "HostCommandChanged";
        public static string GuestCommandChanged = "GuestCommandChanged";
        public static string GuestInputChanged = "GuestInputChanged";

        // error message
        public static string ServerError = "ServerError";
    }

    public class MessageConstants
    {
        public static string SessionCookieName = "SFSession";
    }
}
