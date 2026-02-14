using EpinelPS.Database;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Event
{
    [PacketPath("/event/boxgacha/get")]
    public class GetEventBoxGacha : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            // from client: {"EventId":10051}
            ReqGetEventBoxGacha req = await ReadData<ReqGetEventBoxGacha>();
            User user = GetUser();

            Logging.WriteLine($"EventId-{req.EventId}",LogType.Warning);

            int gCount = 0;

            // Check if eventgacha exists
            if (!user.EventGachaCount.TryGetValue(req.EventId, out var gachacount))
            {
                user.EventGachaCount.Add(req.EventId, 0);
                JsonDb.Save();
            }
            else
            {
                gCount = gachacount;
            }


            ResGetEventBoxGacha response = new()
            {
                GachaCount = gCount,
               
            };

            

            await WriteDataAsync(response);
        }
    }
}