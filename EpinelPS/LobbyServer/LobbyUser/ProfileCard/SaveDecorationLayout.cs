using EpinelPS.Utils;
using Google.Protobuf.WellKnownTypes;

namespace EpinelPS.LobbyServer.LobbyUser.ProfileCard
{
    [PacketPath("/ProfileCard/DecorationLayout/Save")]
    public class SaveDecorationLayout : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqSaveProfileCardDecorationLayout req = await ReadData<ReqSaveProfileCardDecorationLayout>();

            ResSaveProfileCardDecorationLayout response = new();
            User user = GetUser();

            user.DecorationLayout = req.Layout;

            //response.EditingBannedUntil = Timestamp.FromDateTime(DateTime.UtcNow);

            // TODO
            await WriteDataAsync(response);


        }
    }
}
