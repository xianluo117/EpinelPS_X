using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Event
{
    [PacketPath("/event/boxgacha/execute")]
    public class ExecuteEventBoxGacha : LobbyMsgHandler
    {
        private static readonly Random random = new();
        protected override async Task HandleAsync()
        {


            // from client: {"EventId":10051,"CurrentCount":1}
            ReqExecuteEventBoxGacha req = await ReadData<ReqExecuteEventBoxGacha>();
            User user = GetUser();
            
            Logging.WriteLine($"[info] 活动抽奖 {req}:活动ID- {req.EventId},数量-{req.CurrentCount}", LogType.Error);

            ResExecuteEventBoxGacha response = new();


            if (req.CurrentCount==0)
            {

                
            }
            else if (req.CurrentCount>0)
            {

                
            }






            await WriteDataAsync(response);
        }
    }
}