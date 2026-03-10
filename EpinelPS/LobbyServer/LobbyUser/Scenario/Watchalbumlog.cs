using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.LobbyUser.Scenario
{
    [PacketPath("/user/scenario/watchalbumlog")]
    public class WatchAlbumScenarioLog : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqWatchAlbumScenarioLog req = await ReadData<ReqWatchAlbumScenarioLog>();

            Logging.WriteLine($"[info]{req},id -{req.AlbumResourceId},type-{req.ScenarioDataType}", LogType.Error);

            User user = GetUser();
                       
            ResWatchAlbumScenarioLog response = new();



            await WriteDataAsync(response);
        }
    }
}
