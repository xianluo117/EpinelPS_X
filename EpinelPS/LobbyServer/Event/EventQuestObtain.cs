using EpinelPS.Database;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Event
{
    [PacketPath("/eventquest/obtain")]
    public class EventQuestObtain : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqObtainEventQuestReward req = await ReadData<ReqObtainEventQuestReward>();

            Logging.WriteLine($"[debug] EventQuestTidList {req.EventQuestTidList}",LogType.Debug);

            ResObtainEventQuestReward response = new();
            
            // TODO reward

            await WriteDataAsync(response);
        }
    }
}
