using EpinelPS.Data;
using EpinelPS.Utils;
using System;
using System.Security.Policy;
using EpinelPS.Database;

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
            
            ContentsShopRecord? shoplist = GameData.Instance.ContentsShopTable.Values.FirstOrDefault(se => (int)se.ShopCategory == x.ShopCategory);
            ResGetShop response = new();
            NetShopProductData tShopProductData = new NetShopProductData();

            int dateDay = user.GetDateDay();
            tShopProductData = ShopHelper.LoadCurShopByCategory(dateDay, user, shoplist, x.ShopCategory);

            /*if (user.CurrentShopDate.LastDay != dateDay)
            {
                user.CurrentShopDate.LastDay = dateDay;

                tShopProductData.ShopTid = shoplist.Id;
                tShopProductData.ShopCategory = (int)shoplist.ShopCategory;
                tShopProductData.RenewAt = DateTime.Now.AddDays(-5).Ticks;
                tShopProductData.NextRenewAt = DateTime.Now.AddDays(13).Ticks;
                tShopProductData.FreeRenewCount = 5;
                tShopProductData.RenewCount = 5;
                ShopHelper.GetInfoData(user, x.ShopCategory, ref tShopProductData, shoplist.BundleId);
                
                if (user.CurrentShopDate.ShopProduct.TryGetValue(x.ShopCategory, out NetShopProductData oldProduct))
                {
                    // 更新为新值
                    user.CurrentShopDate.ShopProduct[x.ShopCategory] = tShopProductData;
                }
                else
                {
                    user.CurrentShopDate.ShopProduct.TryAdd(x.ShopCategory, tShopProductData);
                }
            }
            else
            {
                if (user.CurrentShopDate.ShopProduct.TryGetValue(x.ShopCategory, out NetShopProductData oldProduct))
                {
                    tShopProductData = oldProduct;
                }

            }*/

            response.Shop = tShopProductData;


            // TODO
            JsonDb.Save();
            await WriteDataAsync(response);
        }

        
    }
}
