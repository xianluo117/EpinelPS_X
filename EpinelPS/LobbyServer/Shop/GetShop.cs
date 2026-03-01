using EpinelPS.Data;
using EpinelPS.Utils;
using System;
using System.Security.Policy;

namespace EpinelPS.LobbyServer.Shop
{
    [PacketPath("/shop/get")]
    public class GetShop : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetShop x = await ReadData<ReqGetShop>();
            Logging.WriteLine($"Get Shop: {x.ShopCategory}", LogType.Info);
            User user = GetUser();
            
            var shoplist = GameData.Instance.ContentsShopTable.Values.FirstOrDefault(se => (int)se.ShopCategory == x.ShopCategory);


            ResGetShop response = new();


            NetShopProductData tShopProductData = new NetShopProductData();

            tShopProductData.ShopTid = shoplist.Id;
            tShopProductData.ShopCategory = (int)shoplist.ShopCategory;
            tShopProductData.RenewAt = DateTime.Now.AddDays(-5).Ticks;
            tShopProductData.NextRenewAt = DateTime.Now.AddDays(13).Ticks;
            tShopProductData.FreeRenewCount = 5;
            tShopProductData.RenewCount = 5;
            GetInfoData(user, x.ShopCategory, ref tShopProductData, shoplist.BundleId);


            response.Shop = tShopProductData;

            // TODO

            await WriteDataAsync(response);
        }

        private void GetInfoData(User user,int ShopCategory, ref NetShopProductData nspddata, int bundleId)
        {
            // 创建临时列表
            List<NetShopProductInfoData> tempList = new List<NetShopProductInfoData>();
            int dateDay = user.GetDateDay();

            var products = GameData.Instance.ContentsShopProductTable.Values.Where(csp => csp.BundleId == bundleId);

            var userBuyCounts = new List<EventShopProductData>();

            if (user.ShopBuyCountInfo.TryGetValue(ShopCategory, out var userBuyCountInfo))
            {
                userBuyCounts = userBuyCountInfo.datas;
                foreach (var csp in products)
                {
                    int buyCount = 0;
                    if (userBuyCountInfo.LastDay == dateDay)
                    {
                        buyCount = userBuyCounts.FirstOrDefault(x => x.ProductTid == csp.Id)?.BuyCount ?? 0;
                    }


                    tempList.Add(new NetShopProductInfoData()
                    {
                        Order = csp.ProductOrder,
                        ProductId = csp.Id,
                        BuyLimitCount = csp.BuyLimitCount,
                        BuyCount = buyCount,
                        Discount = csp.DiscountProbId
                    });
                }
            }
            else
            {
                foreach (var csp in products)
                {
                    int buyCount = 0;
                    tempList.Add(new NetShopProductInfoData()
                    {
                        Order = csp.ProductOrder,
                        ProductId = csp.Id,
                        BuyLimitCount = csp.BuyLimitCount,
                        BuyCount = buyCount,
                        Discount = csp.DiscountProbId
                    });
                }
            }


           

            // 将临时列表添加到原对象
            nspddata.List.AddRange(tempList);
        }
    }
}
