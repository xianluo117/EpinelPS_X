using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop
{

    [PacketPath("/shop/trade")]
    public class ShopTrade : LobbyMsgHandler
    {
        //商店物品贸易？
        protected override async Task HandleAsync()
        {
            ReqTradeShopBuyProduct req = await ReadData<ReqTradeShopBuyProduct>();

            Logging.WriteLine($"[ShopTrade]{req},id- {req.ShopProductId},Count-{req.Count}", LogType.Warning);

            ResTradeShopBuyProduct response = new();

            await WriteDataAsync(response);
        }
    }
}
