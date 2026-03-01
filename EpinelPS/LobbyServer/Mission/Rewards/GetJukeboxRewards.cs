using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Mission.Rewards
{
    [PacketPath("/mission/getrewarded/jukebox")]
    public class GetJukeboxRewards : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetJukeboxRewardedData req = await ReadData<ReqGetJukeboxRewardedData>();
            User user = GetUser();
            // TODO: save these things
            ResGetJukeboxRewardedData response = new();
            response.JukeboxMissionTidList.Add(user.JukeboxMissionList);
            await WriteDataAsync(response);
        }
    }
}
