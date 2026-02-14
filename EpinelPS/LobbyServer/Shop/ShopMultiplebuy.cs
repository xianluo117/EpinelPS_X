using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop
{

    [PacketPath("/shop/multiple-buy")]
    public class ShopMultipleBuy : LobbyMsgHandler
    {
        //商店全部购买
        protected override async Task HandleAsync()
        {
            ReqShopBuyMultipleProduct req = await ReadData<ReqShopBuyMultipleProduct>();

            Logging.WriteLine($"[ShopMultipleBuy]{req},ShopCategory - {req.ShopCategory},Products - {req.Products}", LogType.Warning);

            ResShopBuyMultipleProduct response = new();

            await WriteDataAsync(response);
        }
    }
}
