using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop.InApp
{
    [PacketPath("/inappshop/jupiter/getmarketingdetail")]
    public class GetMarketingDetail : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetJupiterMarketingDetail req = await ReadData<ReqGetJupiterMarketingDetail>();

            Logging.WriteLine($"GetMarketingDetail: {req.Language}", LogType.Warning);

            ResGetJupiterMarketingDetail response = new()
            {
                MarketingDetail = "{}"
            };

            

            await WriteDataAsync(response);
        }
    }
}
