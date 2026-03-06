using EpinelPS.Data;
using EpinelPS.Database;
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
            User user = GetUser();
            List<ContentsShopRecord> shoplist = GameData.Instance.ContentsShopTable.Values.Where(x => x.ShopType == ShopType.MainShop).ToList();
            List<NetShopProductData> netShopDatas = new();

            int dateDay = user.GetDateDay();

            netShopDatas = ShopHelper.LoadCurShop(dateDay, user, shoplist);

            /*if (user.CurrentShopDate.LastDay != dateDay)
            {
                user.CurrentShopDate.LastDay = dateDay;
                user.CurrentShopDate.ShopProduct = new ();
                foreach (var shop in shoplist)
                {
                    NetShopProductData tShopProductData = new NetShopProductData();

                    tShopProductData.ShopTid = shop.Id;
                    tShopProductData.ShopCategory = (int)shop.ShopCategory;
                    tShopProductData.RenewAt = DateTime.Now.AddDays(-5).Ticks;
                    tShopProductData.NextRenewAt = DateTime.Now.AddDays(13).Ticks;
                    tShopProductData.FreeRenewCount = 5;
                    tShopProductData.RenewCount = 5;
                    ShopHelper.GetInfoData(user, shop.Id, ref tShopProductData, shop.BundleId);
                    netShopDatas.Add(tShopProductData);
                    user.CurrentShopDate.ShopProduct.TryAdd(tShopProductData.ShopCategory, tShopProductData);
                }
               
            }
            else
            {
                var allKeys = user.CurrentShopDate.ShopProduct.Keys.ToList();
                foreach (int key in allKeys)
                {
                    if (user.CurrentShopDate.ShopProduct.TryGetValue(key, out NetShopProductData product))
                    {
                        netShopDatas.Add(product);
                    }
                }
            }*/

            response.Shops.AddRange(netShopDatas);

            JsonDb.Save();
            await WriteDataAsync(response);
        }

       
    }
}
