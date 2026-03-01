using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.LobbyUser.ProfileCard
{
    [PacketPath("/ProfileCard/Possession/Get")]
    public class GetProfileCardPossession : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqProfileCardObjectList req = await ReadData<ReqProfileCardObjectList>();
           
            ResProfileCardObjectList response = new();
            User user = GetUser();

            response.BackgroundIds.AddRange(user.BackgroundList);
            response.StickerIds.AddRange(user.StickerList);

            // TODO
            await WriteDataAsync(response);
        }
    }
}
