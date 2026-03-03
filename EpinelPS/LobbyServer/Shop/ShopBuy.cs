using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.LobbyServer.Event.Shop;
using EpinelPS.Utils;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Ocsp;

namespace EpinelPS.LobbyServer.Shop
{
    [PacketPath("/shop/buy")]
    public class ShopBuy : LobbyMsgHandler
    {
        //商店购买物品
        protected override async Task HandleAsync()
        {
            ReqShopBuyProduct req = await ReadData<ReqShopBuyProduct>();
            User user = GetUser();
            int dateDay = user.GetDateDay();
            Logging.WriteLine($"[ShopBuy]{req},id- {req.ShopProductTid},Order-{req.Order},ShopCategory-{req.ShopCategory},Quantity-{req.Quantity}", LogType.Warning);
            
            ResShopBuyProduct response = new();

            try
            {
                response = ShopHelper.BuyShopProduct(user, req);
                ShopHelper.UpCount(user,req.ShopCategory,req.ShopProductTid,req.Quantity);
                response.Result = ShopBuyProductResult.Success;
            }
            catch (Exception ex)
            {
                Logging.WriteLine($"Error buying shop product: {ex.Message}", LogType.Error);
            }

           

            user.AddTrigger(Trigger.MainShopBuy, req.Quantity);

            JsonDb.Save();
            await WriteDataAsync(response);
        }
    }
}
