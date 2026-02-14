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

            User user = GetUser();

            int sc = x.ShopCategory;

            Logging.WriteLine($"Get Shop: {x.ShopCategory}", LogType.Info);

            var Shop = new NetShopProductData { ShopCategory = x.ShopCategory };

            ResGetShop response = new();
            
            ContentsShopRecord[] ShopTable = GameData.Instance.ContentsShopTable.Values
                .Where(tx => tx.ShopCategory == (ShopCategoryType)sc)
                .ToArray();

            if (ShopTable.Length <= 0)
            {
                throw new Exception($"未找到记录: ShopCategory = {(ShopCategoryType)sc}");
            }
            else if (ShopTable.Length == 1)
            {
                Shop.ShopTid = ShopTable[0].Id;

                GameData.Instance.ContentsShopProductTable.Values
                    .Where(csp => csp.BundleId == ShopTable[0].BundleId).ToList().ForEach(csp =>
                    {

                        Shop.List.Add(new NetShopProductInfoData()
                        {

                            Order = csp.ProductOrder,
                            ProductId = csp.Id,
                            BuyLimitCount = csp.BuyLimitCount+1,
                            BuyCount = 1,
                            // Discount = csp.DiscountProbId,
                        });
                    });

            }
            else if(ShopTable.Length > 1)
            {
                Random random = new Random();
                int index = random.Next(0, ShopTable.Length);

                Shop.ShopTid = ShopTable[index].Id;

                GameData.Instance.ContentsShopProductTable.Values
                    .Where(csp => csp.BundleId == ShopTable[index].BundleId).ToList().ForEach(csp =>
                    {

                        Shop.List.Add(new NetShopProductInfoData()
                        {

                            Order = csp.ProductOrder,
                            ProductId = csp.Id,
                            BuyLimitCount = csp.BuyLimitCount,
                            BuyCount = 1,
                            // Discount = csp.DiscountProbId,
                        });
                    });


            }

            Logging.WriteLine($"Shop: {Shop.ShopCategory},{Shop.ShopTid}", LogType.Info);

            response.Shop = Shop;


            // TODO

            await WriteDataAsync(response);
        }
    }
}
