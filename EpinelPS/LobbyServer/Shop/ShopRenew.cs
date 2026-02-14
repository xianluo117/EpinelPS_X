using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop
{

    [PacketPath("/shop/renew")]
    public class ShopRenew : LobbyMsgHandler
    {
        //刷新商店
        protected override async Task HandleAsync()
        {
            ReqShopRenew req = await ReadData<ReqShopRenew>();

            Logging.WriteLine($"[ShopRenew]{req},id- {req.ShopCategory}", LogType.Warning);

            ResShopRenew response = new();

            await WriteDataAsync(response);
        }
    }
}
