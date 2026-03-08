using EpinelPS.Data;
using EpinelPS.LobbyServer.Event.Shop;
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
            User user = GetUser();


            try
            {
                ShopHelper.BuyShopMultipleProduct(user, ref response, req);
                response.Result = ShopBuyProductResult.Success;
            }
            catch (Exception ex)
            {
                Logging.WriteLine($"Error buying shop product: {ex.Message}", LogType.Error);
            }

            foreach (var item in req.Products)
            {
                ShopHelper.UpCount(user, req.ShopCategory, item.ShopProductTid, item.Quantity);
            }

            user.AddTrigger(Trigger.MainShopBuy, req.Products.Count);
            await WriteDataAsync(response);
        }
    }
}
