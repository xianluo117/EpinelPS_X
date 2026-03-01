using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop.InApp
{
    [PacketPath("/inappshop/getmonthlysubscriptionreward")]
    public class GetMonthlySubscriptionReward : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetMonthlySubscriptionReward req = await ReadData<ReqGetMonthlySubscriptionReward>();

            Logging.WriteLine($"GetMonthlySubscriptionReward: {req}", LogType.Warning);

            ResGetMonthlySubscriptionReward response = new();

            // TODO: ValIdate response from real server
            await WriteDataAsync(response);
        }
    }
}
