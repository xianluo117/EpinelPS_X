using EpinelPS.Data;
using EpinelPS.LobbyServer.Event.Shop;
using EpinelPS.Models;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop
{
    [PacketPath("/shop/productlist")]
    public class GetShopProductList : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqShopProductList req = await ReadData<ReqShopProductList>();
            Logging.WriteLine($"ReqShopProductList: {req}", LogType.Info);
            ResShopProductList response = new();

            var shoplist = GameData.Instance.ContentsShopTable.Values.Where(x => x.ShopType == ShopType.MainShop);

            foreach (var shop in shoplist)
            {
                NetShopProductData tShopProductData = new NetShopProductData();

                tShopProductData.ShopTid = shop.Id;
                tShopProductData.ShopCategory = (int)shop.ShopCategory;
                tShopProductData.RenewAt = DateTime.Now.AddDays(-5).Ticks;
                tShopProductData.NextRenewAt = DateTime.Now.AddDays(13).Ticks;
                tShopProductData.FreeRenewCount = 5;
                tShopProductData.RenewCount = 5;
                GetInfoData(shop.Id, ref tShopProductData, shop.BundleId);
                response.Shops.Add(tShopProductData);
            }

            await WriteDataAsync(response);
        }

        private void GetInfoData(int shopId, ref NetShopProductData nspddata, int bundleId)
        {
            // 创建临时列表
            List<NetShopProductInfoData> tempList = new List<NetShopProductInfoData>();

            var products = GameData.Instance.ContentsShopProductTable.Values.Where(csp => csp.BundleId == bundleId);

            foreach (var csp in products)
            {
                tempList.Add(new NetShopProductInfoData()
                {
                    Order = csp.ProductOrder,
                    ProductId = csp.Id,
                    BuyLimitCount = csp.BuyLimitCount,
                    Discount = csp.DiscountProbId
                });
            }

            // 将临时列表添加到原对象
            nspddata.List.AddRange(tempList);
        }
    }
}
