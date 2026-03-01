using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Event
{
    [PacketPath("/cheat/event/dailymission/setcompletable")]
    public class EventDailySetCompletable : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqSetCompletableDailyEventMissions req = await ReadData<ReqSetCompletableDailyEventMissions>();

            Logging.WriteLine($"[debug] Days {req.Days},EventId {req.EventId}",LogType.Debug);

            ResSetCompletableDailyEventMissions response = new();

            

            await WriteDataAsync(response);
        }
    }
}
