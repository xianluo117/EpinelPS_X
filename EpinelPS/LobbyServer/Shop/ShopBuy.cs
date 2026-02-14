using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop
{
    [PacketPath("/shop/buy")]
    public class ShopBuy : LobbyMsgHandler
    {
        //商店购买物品
        protected override async Task HandleAsync()
        {
            ReqShopBuyProduct req = await ReadData<ReqShopBuyProduct>();

            Logging.WriteLine($"[ShopBuy]{req},id- {req.ShopProductTid},Order-{req.Order},ShopCategory-{req.ShopCategory},Quantity-{req.Quantity}", LogType.Warning);

            ResShopBuyProduct response = new();

            await WriteDataAsync(response);
        }
    }
}
