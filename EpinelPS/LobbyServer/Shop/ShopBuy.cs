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
                response.Result = ShopBuyProductResult.Success;
            }
            catch (Exception ex)
            {
                Logging.WriteLine($"Error buying shop product: {ex.Message}", LogType.Error);
            }

            var userBuyCounts = new List<EventShopProductData>();
            if (user.ShopBuyCountInfo.TryGetValue(req.ShopCategory, out var userBuyCountInfo))
            {
                userBuyCounts = userBuyCountInfo.datas;
                EventShopProductData? productcountData =
                    userBuyCounts.FirstOrDefault(x => x.ProductTid == req.ShopProductTid);
                if (productcountData == null)
                {
                    // 记录不存在：新建
                    productcountData = new EventShopProductData
                    {
                        ProductTid = req.ShopProductTid, BuyCount = req.Quantity
                    };
                    userBuyCounts.Add(productcountData);
                    userBuyCountInfo.LastDay = dateDay;
                }
                else if (userBuyCountInfo.LastDay == dateDay)
                {
                    // 记录存在且是今天：累加
                    productcountData.BuyCount += req.Quantity;
                }
                else
                {
                    // 记录存在但不是今天：重置
                    productcountData.BuyCount = req.Quantity;
                    userBuyCountInfo.LastDay = dateDay;
                }
            }
            else
            {
                user.ShopBuyCountInfo.Add(req.ShopCategory, new()
                {
                    ShopCategory = req.ShopCategory,
                    LastDay = dateDay,
                    datas = new List<EventShopProductData>
                    {
                        new()
                        {
                            ProductTid = req.ShopProductTid,
                            BuyCount = req.Quantity
                        }
                    }
                });

            }

            user.AddTrigger(Trigger.MainShopBuy, req.Quantity);

            JsonDb.Save();
            await WriteDataAsync(response);
        }
    }
}
