using StateFlux.Model;

namespace StateFlux.Client
{
    public interface IStateFluxListener
    {
        void OnStateFluxInitialize();
        void OnStateFluxWaitingToConnect();
        void OnStateFluxConnect();
        void OnStateFluxDisconnect();
        void OnStateFluxServerError(ServerErrorMessage message);
        void OnStateFluxStateChanged(StateChangedMessage message);
        void OnStateFluxPlayerListing(PlayerListingMessage message);
        void OnStateFluxGameInstanceCreated(GameInstanceCreatedMessage message);
        void OnStateFluxGameInstanceJoined(GameInstanceJoinedMessage message);
        void OnStateFluxGameInstanceListing(GameInstanceListingMessage message);
        void OnStateFluxGameInstanceStart(GameInstanceStartMessage message);
        void OnStateFluxChatSaid(ChatSaidMessage message);
        void OnStateFluxOtherMessage(Message message);
    }

}
