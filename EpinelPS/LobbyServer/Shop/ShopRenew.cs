using EpinelPS.Data;
using EpinelPS.Database;
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
            User user = GetUser();
            int dateDay = user.GetDateDay();
            Random random = new Random();
            Logging.WriteLine($"[ShopRenew]{req},id- {req.ShopCategory}", LogType.Warning);

            ContentsShopRecord? shop = GameData.Instance.ContentsShopTable.Values.FirstOrDefault(se => (int)se.ShopCategory == req.ShopCategory);

            ResShopRenew response = new();
            NetShopProductData tShopProductData = new NetShopProductData();

            List<ContentsShopRecord> shoplist = GameData.Instance.ContentsShopTable.Values
                .Where(x => x.ShopCategory == (ShopCategoryType)req.ShopCategory).ToList();
            int randomNumber = random.Next(0, shoplist.Count); // 包含0，不包含10

            // tShopProductData.ShopTid = shoplist[randomNumber].Id;
            // tShopProductData.ShopCategory = (int)shoplist[randomNumber].ShopCategory;
            // tShopProductData.RenewAt = DateTime.Now.AddDays(-5).Ticks;
            // tShopProductData.NextRenewAt = DateTime.Now.AddDays(13).Ticks;
            // tShopProductData.FreeRenewCount = 5;
            // tShopProductData.RenewCount = 5;
            // ShopHelper.GetInfoData(user, req.ShopCategory, ref tShopProductData, shoplist[randomNumber].BundleId);

            tShopProductData = ShopHelper.RenewShopByCategory(dateDay, user, shoplist[randomNumber], req.ShopCategory);
            response.Shop = tShopProductData;

            // if (user.CurrentShopDate.ShopProduct.TryGetValue(req.ShopCategory, out NetShopProductData oldProduct))
            // {
            //     // 更新为新值
            //     user.CurrentShopDate.ShopProduct[req.ShopCategory] = tShopProductData;
            // }
            // else
            // {
            //     user.CurrentShopDate.ShopProduct.TryAdd(req.ShopCategory, tShopProductData);
            // }



            if (user?.ResetableData?.RenewDatas?.TryGetValue(req.ShopCategory, out RenewData renew) != true)
            {
                renew = new RenewData
                {
                    ShopCategory = req.ShopCategory,
                    LastDay = dateDay,
                    Count = 1
                };
                // Create a new default entry for the missing EventId
                user.ResetableData.RenewDatas[req.ShopCategory] = renew;
                
            }
            else
            {
                renew.Count++;
            }

            if (User.ShouldResetUser())
            {
                user.ResetableData.RenewDatas[req.ShopCategory].Count = 1;
            }

            int step = user.ResetableData.RenewDatas[req.ShopCategory].Count;

            ContentsShopRenewRecord? renewTable = GameData.Instance.ContentsShopRenewTable.Values.Where(x => x.RenewGroupId == shop.RenewGroupId && x.RenewStep == step).FirstOrDefault();
            NetUserCurrencyData currency = new();
            if (renewTable!=null)
            {
                currency = new NetUserCurrencyData
                {
                    Type =(int) renewTable.PriceId,
                    Value = renewTable.PriceValue
                };
            }
            else
            {
                currency = new NetUserCurrencyData
                {
                    Type = (int)CurrencyType.FreeCash,
                    Value = 1000
                };
            }

            response.Currencies.Add(currency);

            JsonDb.Save();
            await WriteDataAsync(response);
        }
    }
}
