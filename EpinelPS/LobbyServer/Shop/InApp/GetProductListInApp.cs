using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop.InApp
{
    [PacketPath("/inappshop/getdata")]
    public class GetProductData : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetInAppShopData x = await ReadData<ReqGetInAppShopData>();

            Logging.WriteLine($"inappshop GetProductData{x}");

            ResGetInAppShopData response = new();
            
            response.InAppShopDataList.Add(new NetInAppShopData() { Id = 20002, StartDate = DateTime.Now.AddDays(-5).Ticks, EndDate = DateTime.Now.AddDays(10).Ticks });
            
            await WriteDataAsync(response);
        }
    }
}
