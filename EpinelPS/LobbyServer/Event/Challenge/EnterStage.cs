using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Event.Challenge
{
    [PacketPath("/event/challengestage/enter")]
    public class EnterChallengeStage : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqEnterChallengeEventStage req = await ReadData<ReqEnterChallengeEventStage>();
            User user = GetUser();
            ResEnterChallengeEventStage response = new();

            await WriteDataAsync(response);
        }
    }
}
