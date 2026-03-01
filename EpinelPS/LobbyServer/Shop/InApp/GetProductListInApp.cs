using EpinelPS.Data;
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

            var shoplist = GameData.Instance.InAppShopManagerRecords.Values.Where(x => x.EndDate > DateTime.UtcNow);

            foreach (var shop in shoplist)
            {
                response.InAppShopDataList.Add(new NetInAppShopData()
                {
                    Id = shop.Id, StartDate = DateTime.Now.AddDays(-5).Ticks, EndDate = DateTime.Now.AddDays(10).Ticks
                });
            }

            //todo
            response.BuyDataList.Add(new NetInAppShopBuyData()
            {
                
            });
            
            
            await WriteDataAsync(response);
        }
    }
}
