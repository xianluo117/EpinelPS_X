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

            User user = GetUser();

            Logging.WriteLine($"ReqShopProductList: {req}", LogType.Info);

            ResShopProductList response = new();

            int[] inlist = new[] { 100101, 100201, 100301, 100401, 100501, 100601, 100701, 100801, 700101, 1200101, 1300101, 1300201, 1300301, 1300401, 1300501, 1300601, 1300701, 1400101, 1500101, 1700101 };




            foreach (var id in inlist)
            {
                var item =  GameData.Instance.ContentsShopTable.Where(x => x.Value.Id == id).FirstOrDefault().Value;
                NetShopProductData tShopProductData = new NetShopProductData();
            
                tShopProductData.ShopTid = item.Id;
                tShopProductData.ShopCategory = (int)item.ShopCategory;
                tShopProductData.RenewAt = DateTime.Now.AddDays(-5).Ticks;
                tShopProductData.NextRenewAt = DateTime.Now.AddDays(13).Ticks;
                tShopProductData.FreeRenewCount = 5;
                tShopProductData.RenewCount =5;
                GetInfoData(item.Id, ref tShopProductData, item.BundleId);
                response.Shops.Add(tShopProductData);
            }

            await WriteDataAsync(response);
        }

        private void GetInfoData(int shopId, ref NetShopProductData nspddata, int bundleId)
        {
            // 创建临时列表
            List<NetShopProductInfoData> tempList = new List<NetShopProductInfoData>();

            var products = GameData.Instance.ContentsShopProductTable.Values
                .Where(csp => csp.BundleId == bundleId);

            foreach (var csp in products)
            {
                tempList.Add(new NetShopProductInfoData()
                {
                    Order = csp.ProductOrder,
                    ProductId = csp.Id,
                    BuyLimitCount = csp.BuyLimitCount+1,
                    BuyCount = 1,
                });
            }

            // 将临时列表添加到原对象
            nspddata.List.AddRange(tempList);
        }
    }
}
