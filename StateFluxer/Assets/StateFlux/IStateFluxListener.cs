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
        void OnStateFluxGameInstanceCreatedMessage(GameInstanceCreatedMessage message);
        void OnStateFluxGameInstanceListing(GameInstanceListingMessage message);
        void OnStateFluxChatSaid(ChatSaidMessage message);
        void OnStateFluxOtherMessage(Message message);
    }

}
