using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.LobbyServer.Event.Shop;
using EpinelPS.Utils;
using log4net;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Org.BouncyCastle.Ocsp;
using System.Collections.Concurrent;
using System.Data.Common;
using EpinelPS.LobbyServer.LobbyUser;
using static EpinelPS.Database.SqliteQueryHelper;

namespace EpinelPS.LobbyServer.Shop;

public class ShopHelper
{
    public static ConcurrentDictionary<int, ShopDate> _shopDateCache;

    public static ResShopBuyProduct BuyShopProduct(User user, ReqShopBuyProduct req)
    {
        ResShopBuyProduct response = new();
        ResShopBuyMultipleProduct MultipleResponse = new();
        List<NetBuyProductRequestData> buyProducts =
            [new() { ShopProductTid = req.ShopProductTid, Quantity = req.Quantity, Order = req.Order }];
        bool isSuccess = ExecuteBuyProduct(user, ref MultipleResponse, buyProducts);

        if (!isSuccess)
        {
            return response;
        }
        else
        {
            AddShopBuyCount(user, ref MultipleResponse, req.ShopCategory, req.ShopProductTid, req.Quantity, req.Order);
            // Update currency data
            if (MultipleResponse.Currencies.Count > 0)
                response.Currencies.AddRange(MultipleResponse.Currencies);
            // Update item data
            if (MultipleResponse.Items.Count > 0)
                response.Item = MultipleResponse.Items[0];

            // Update product data
            response.Product = new();
            if (MultipleResponse.Product.UserItems.Count > 0)
                response.Product.UserItems.AddRange(MultipleResponse.Product.UserItems);

            if (MultipleResponse.Product.Item.Count > 0) 
                response.Product.Item.AddRange(MultipleResponse.Product.Item);

            if (MultipleResponse.Product.Currency.Count > 0)
                response.Product.Currency.AddRange(MultipleResponse.Product.Currency);

            if (MultipleResponse.Product.BuyCounts.Count > 0)
                response.Product.BuyCount = MultipleResponse.Product.BuyCounts[0].BuyCount;

            if (MultipleResponse.Product.Character.Count > 0)
                response.Product.Character.AddRange(MultipleResponse.Product.Character);

            if (MultipleResponse.Product.UserCharacters.Count > 0)
                response.Product.UserCharacters.AddRange(MultipleResponse.Product.UserCharacters);

            if (MultipleResponse.Product.AutoCharge.Count > 0)
                response.Product.AutoCharge.AddRange(MultipleResponse.Product.AutoCharge);

            // user.AddTrigger(Trigger.MainShopBuy, req.Quantity);

            // Save changes to the database
            JsonDb.Save();
            Logging.WriteLine($"BuyShop 输出response： {response}！", LogType.Debug);
            return response;
        }
    }


    public static List<ContentsShopProductRecord> SelectRandomItems(List<ContentsShopProductRecord> shoplist)
    {
       

        if (!shoplist.Any())
            return new List<ContentsShopProductRecord>();

        // 1. 按 ProductOrder 类型分组
        var groupedByOrder = shoplist
            .GroupBy(x => x.ProductOrder)
            .ToDictionary(g => g.Key, g => g.ToList());

        var selectedItems = new List<ContentsShopProductRecord>();

        // 2. 对每个 ProductOrder 类型进行抽取
        foreach (var orderGroup in groupedByOrder)
        {
            var itemsInGroup = orderGroup.Value;

            // 计算该组的总概率
            double totalProb = itemsInGroup.Sum(x => x.ProductProb);

            if (totalProb <= 0)
                continue;

            // 3. 根据概率选择物品
            ContentsShopProductRecord selectedItem = SelectItemByProbability(itemsInGroup, totalProb);

            if (selectedItem != null)
            {
                selectedItems.Add(selectedItem);
            }
        }

        return selectedItems;
    }


    // 根据概率选择物品
    public static ContentsShopProductRecord SelectItemByProbability(List<ContentsShopProductRecord> items, double totalProb)
    {
        if (!items.Any() || totalProb <= 0)
            return null;

        // 生成随机数
        Random random = new Random();
        double randomValue = random.NextDouble() * totalProb;

        double cumulativeProb = 0;

        foreach (var item in items)
        {
            cumulativeProb += item.ProductProb;

            if (randomValue <= cumulativeProb)
            {
                return item;
            }
        }

        // 如果由于浮点数精度问题没选中，返回最后一个
        return items.Last();
    }

    // 计算折扣价格
    public static int CalculateDiscounted(int discountProbId)
    {

        // DiscountProbId 小于等于 100，直接折扣
        if (discountProbId <= 100)
        {
            return discountProbId;
        }

        // DiscountProbId 为 1000，从 10%、20%、40% 中随机
        if (discountProbId == 1000)
        {
            int[] discounts = { 10, 20, 40 };
            Random random = new Random();
            int index = random.Next(discounts.Length);
            return discounts[index];
        }

        // DiscountProbId 为 1001，从 10%、20%、30% 中随机
        if (discountProbId == 1001)
        {
            int[] discounts = { 10, 20, 30 };
            Random random = new Random();
            int index = random.Next(discounts.Length);
            return discounts[index];
        }

        // 其他情况，没有折扣
        return 0;
    }

    public static void LoadShopDate()
    {
        _shopDateCache = new ConcurrentDictionary<int, ShopDate>();
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM ShopDate";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var info = new ShopDate
            {
                ShopCategory = reader.GetInt32(0),
                LastDay = reader.GetInt32(1)
            };

            _shopDateCache[info.ShopCategory] = info;
        }
    }

    public static void SaveShopDate(ShopDate info)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
        INSERT OR REPLACE INTO ShopDate (ShopCategory, LastDay) 
        VALUES (@shopCategory, @lastday)
    ";

        command.Parameters.AddWithValue("@shopCategory", info.ShopCategory);
        command.Parameters.AddWithValue("@lastday", info.LastDay);
        command.ExecuteNonQuery();
    }

    public static Dictionary<int, List<CurrentShopInfo>> GetCurrentShopInfo()
    {
        var result = new Dictionary<int, List<CurrentShopInfo>>();

        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM CurrentShopInfo ORDER BY ShopCategory";

        using var reader = command.ExecuteReader();
        // 验证是否有数据
        if (!reader.HasRows)
        {
            return result; // 返回空字典
        }

        while (reader.Read())
        {
            var shopInfo = new CurrentShopInfo
            {
                ShopCategory = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                ShopTid = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                RenewCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                RenewAt = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                NextRenewAt = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                FreeRenewCount = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                ProductId = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                ProductOrder = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                BuyLimitCount = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                BuyCount = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                CorporationType = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                Discount = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                EndAt = reader.IsDBNull(13) ? 0 : reader.GetInt64(13),
                UseDateCondition = reader.IsDBNull(14) ? false : reader.GetBoolean(14)
            };

            // 按ShopCategory分组
            if (!result.ContainsKey(shopInfo.ShopCategory))
            {
                result[shopInfo.ShopCategory] = new List<CurrentShopInfo>();
            }

            result[shopInfo.ShopCategory].Add(shopInfo);
        }

        return result;
    }

    public static CurrentShopInfo GetCurrentShopInfo(int shopCategory,int productId)
    {
        var result = new CurrentShopInfo();

        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM CurrentShopInfo WHERE ShopCategory = @shopCategory AND ProductId = @productId";
        command.Parameters.AddWithValue("@shopCategory", shopCategory);
        command.Parameters.AddWithValue("@productId", productId);
        using var reader = command.ExecuteReader();
        // 验证是否有数据
        if (!reader.HasRows)
        {
            return result; // 返回空字典
        }

        while (reader.Read())
        {
            var shopInfo = new CurrentShopInfo
            {
                ShopCategory = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                ShopTid = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                RenewCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                RenewAt = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                NextRenewAt = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                FreeRenewCount = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                ProductId = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                ProductOrder = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                BuyLimitCount = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                BuyCount = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                CorporationType = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                Discount = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                EndAt = reader.IsDBNull(13) ? 0 : reader.GetInt64(13),
                UseDateCondition = reader.IsDBNull(14) ? false : reader.GetBoolean(14)
            };

            result = shopInfo;
        }

        return result;
    }


    public static void SaveShopInfo(CurrentShopInfo shopInfo)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO CurrentShopInfo (
                ShopCategory, ShopTid, RenewCount, RenewAt, 
                NextRenewAt, FreeRenewCount, ProductId, ProductOrder, 
                BuyLimitCount, BuyCount, CorporationType, Discount, 
                EndAt, UseDateCondition
            ) VALUES (
                @ShopCategory, @ShopTid, @RenewCount, @RenewAt,
                @NextRenewAt, @FreeRenewCount, @ProductId, @ProductOrder,
                @BuyLimitCount, @BuyCount, @CorporationType, @Discount,
                @EndAt, @UseDateCondition
            )";

        
        command.Parameters.AddWithValue("@ShopCategory", shopInfo.ShopCategory);
        command.Parameters.AddWithValue("@ShopTid", shopInfo.ShopTid);
        command.Parameters.AddWithValue("@RenewCount", shopInfo.RenewCount);
        command.Parameters.AddWithValue("@RenewAt", shopInfo.RenewAt);
        command.Parameters.AddWithValue("@NextRenewAt", shopInfo.NextRenewAt);
        command.Parameters.AddWithValue("@FreeRenewCount", shopInfo.FreeRenewCount);
        command.Parameters.AddWithValue("@ProductId", shopInfo.ProductId);
        command.Parameters.AddWithValue("@ProductOrder", shopInfo.ProductOrder);
        command.Parameters.AddWithValue("@BuyLimitCount", shopInfo.BuyLimitCount);
        command.Parameters.AddWithValue("@BuyCount", shopInfo.BuyCount);
        command.Parameters.AddWithValue("@CorporationType", shopInfo.CorporationType);
        command.Parameters.AddWithValue("@Discount", shopInfo.Discount);
        command.Parameters.AddWithValue("@EndAt", shopInfo.EndAt);
        command.Parameters.AddWithValue("@UseDateCondition", shopInfo.UseDateCondition);
        command.ExecuteNonQuery();
    }

    public static void ClearCurrentShopInfo()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM CurrentShopInfo";
        command.ExecuteNonQuery();
    }

    public static int DeleteCurrentShopInfoByCategory(int shopCategory)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM CurrentShopInfo WHERE ShopCategory = @shopCategory";
        command.Parameters.AddWithValue("@shopCategory", shopCategory);

        int rowsAffected = command.ExecuteNonQuery();

        return rowsAffected; // 返回删除的行数
    }

    public static NetShopProductData RenewShopByCategory(int dateDay, User user, ContentsShopRecord shop,
        int shopCategory)
    {
        NetShopProductData tShopProductData = new NetShopProductData();

        LoadShopDate();
        SaveShopDate(new() { LastDay = dateDay, ShopCategory = shopCategory });
        DeleteCurrentShopInfoByCategory(shopCategory);

        tShopProductData.ShopTid = shop.Id;
        tShopProductData.ShopCategory = (int)shop.ShopCategory;
        tShopProductData.RenewAt = DateTime.Now.AddDays(-5).Ticks;
        tShopProductData.NextRenewAt = DateTime.Now.AddDays(13).Ticks;
        tShopProductData.FreeRenewCount = 5;
        tShopProductData.RenewCount = 5;
        GetInfoData(user, shop.Id, ref tShopProductData, shop.BundleId);

        var productlist = tShopProductData.List.ToList();

        foreach (var product in productlist)
        {
            CurrentShopInfo cinfo = new CurrentShopInfo();
            cinfo.ShopTid = tShopProductData.ShopTid;
            cinfo.ShopCategory = tShopProductData.ShopCategory;
            cinfo.RenewAt = tShopProductData.RenewAt;
            cinfo.NextRenewAt = tShopProductData.NextRenewAt;
            cinfo.FreeRenewCount = tShopProductData.FreeRenewCount;
            cinfo.RenewCount = tShopProductData.RenewCount;
            cinfo.ProductId = product.ProductId;
            cinfo.ProductOrder = product.Order;
            cinfo.BuyCount = product.BuyCount;
            cinfo.BuyLimitCount = product.BuyLimitCount;
            cinfo.CorporationType = product.CorporationType;
            cinfo.Discount = product.Discount;
            cinfo.EndAt = product.EndAt;
            cinfo.UseDateCondition = product.UseDateCondition;
            SaveShopInfo(cinfo);
        }
        return tShopProductData;
    }


    public static NetShopProductData LoadCurShopByCategory(int dateDay, User user, ContentsShopRecord shop,int shopCategory)
    {
        NetShopProductData tShopProductData = new NetShopProductData();

        LoadShopDate();

        if (_shopDateCache.TryGetValue(shopCategory, out ShopDate? info))
        {
            if (info.LastDay != dateDay)
            {
                info.LastDay = dateDay;
                SaveShopDate(info);
                DeleteCurrentShopInfoByCategory(shopCategory);

                tShopProductData.ShopTid = shop.Id;
                tShopProductData.ShopCategory = (int)shop.ShopCategory;
                tShopProductData.RenewAt = DateTime.Now.AddDays(-5).Ticks;
                tShopProductData.NextRenewAt = DateTime.Now.AddDays(13).Ticks;
                tShopProductData.FreeRenewCount = 5;
                tShopProductData.RenewCount = 5;
                GetInfoData(user, shop.Id, ref tShopProductData, shop.BundleId);

                var productlist = tShopProductData.List.ToList();

                foreach (var product in productlist)
                {
                    CurrentShopInfo cinfo = new CurrentShopInfo();
                    cinfo.ShopTid = tShopProductData.ShopTid;
                    cinfo.ShopCategory = tShopProductData.ShopCategory;
                    cinfo.RenewAt = tShopProductData.RenewAt;
                    cinfo.NextRenewAt = tShopProductData.NextRenewAt;
                    cinfo.FreeRenewCount = tShopProductData.FreeRenewCount;
                    cinfo.RenewCount = tShopProductData.RenewCount;
                    cinfo.ProductId = product.ProductId;
                    cinfo.ProductOrder = product.Order;
                    cinfo.BuyCount = product.BuyCount;
                    cinfo.BuyLimitCount = product.BuyLimitCount;
                    cinfo.CorporationType = product.CorporationType;
                    cinfo.Discount = product.Discount;
                    cinfo.EndAt = product.EndAt;
                    cinfo.UseDateCondition = product.UseDateCondition;
                    SaveShopInfo(cinfo);
                }


                return tShopProductData;
            }
            else
            {


                var allinfo = GetCurrentShopInfo();
                if (allinfo.Count > 0)
                {
                    if (allinfo.ContainsKey(shopCategory))
                    {
                        Console.WriteLine($"分类 {shopCategory} 有 {allinfo[shopCategory].Count} 条数据");
                        List<NetShopProductInfoData> tempList = new List<NetShopProductInfoData>();
                        foreach (var item in allinfo[shopCategory])
                        {
                            tShopProductData.ShopTid = item.ShopTid;
                            tShopProductData.ShopCategory = item.ShopCategory;
                            tShopProductData.RenewAt = item.RenewAt;
                            tShopProductData.NextRenewAt = item.NextRenewAt;
                            tShopProductData.FreeRenewCount = item.FreeRenewCount;
                            tShopProductData.RenewCount = item.RenewCount;

                            tempList.Add(new NetShopProductInfoData()
                            {
                                Order = item.ProductOrder,
                                ProductId = item.ProductId,
                                BuyLimitCount = item.BuyLimitCount,
                                BuyCount = item.BuyCount,
                                Discount = item.Discount,
                                CorporationType = item.CorporationType,
                                EndAt = item.EndAt,
                                UseDateCondition = item.UseDateCondition
                            });
                        }
                        tShopProductData.List.AddRange(tempList);
                        
                    }
                }
                return tShopProductData;

            }

        }
        else
        {
            SaveShopDate(new() { LastDay = dateDay, ShopCategory = shopCategory });

            tShopProductData.ShopTid = shop.Id;
            tShopProductData.ShopCategory = (int)shop.ShopCategory;
            tShopProductData.RenewAt = DateTime.Now.AddDays(-5).Ticks;
            tShopProductData.NextRenewAt = DateTime.Now.AddDays(13).Ticks;
            tShopProductData.FreeRenewCount = 5;
            tShopProductData.RenewCount = 5;
            GetInfoData(user, shop.Id, ref tShopProductData, shop.BundleId);

            var productlist = tShopProductData.List.ToList();
            foreach (var product in productlist)
            {
                CurrentShopInfo cinfo = new CurrentShopInfo();

                cinfo.ShopTid = tShopProductData.ShopTid;
                cinfo.ShopCategory = tShopProductData.ShopCategory;
                cinfo.RenewAt = tShopProductData.RenewAt;
                cinfo.NextRenewAt = tShopProductData.NextRenewAt;
                cinfo.FreeRenewCount = tShopProductData.FreeRenewCount;
                cinfo.RenewCount = tShopProductData.RenewCount;
                cinfo.ProductId = product.ProductId;
                cinfo.ProductOrder = product.Order;
                cinfo.BuyCount = product.BuyCount;
                cinfo.BuyLimitCount = product.BuyLimitCount;
                cinfo.CorporationType = product.CorporationType;
                cinfo.Discount = product.Discount;
                cinfo.EndAt = product.EndAt;
                cinfo.UseDateCondition = product.UseDateCondition;
                SaveShopInfo(cinfo);
            }


            return tShopProductData;
        }

    }

    public static List<NetShopProductData> LoadCurShop(int dateDay, User user, List<ContentsShopRecord> shoplist)
    {
        List<NetShopProductData> netShopDatas = new();
        LoadShopDate();
        if (_shopDateCache.TryGetValue(0, out ShopDate? info))
        {
            if (info.LastDay != dateDay)
            {
                info.LastDay = dateDay;
                SaveShopDate(info);
                ClearCurrentShopInfo();
                foreach (var shop in shoplist)
                {
                    NetShopProductData tShopProductData = new NetShopProductData();
                    tShopProductData.ShopTid = shop.Id;
                    tShopProductData.ShopCategory = (int)shop.ShopCategory;
                    tShopProductData.RenewAt = DateTime.Now.AddDays(-5).Ticks;
                    tShopProductData.NextRenewAt = DateTime.Now.AddDays(13).Ticks;
                    tShopProductData.FreeRenewCount = 5;
                    tShopProductData.RenewCount = 5;
                    GetInfoData(user, shop.Id, ref tShopProductData, shop.BundleId);
                    SaveShopDate(new() { LastDay = dateDay, ShopCategory = tShopProductData.ShopCategory });
                    var productlist = tShopProductData.List.ToList();
                    foreach (var product in productlist)
                    {
                        CurrentShopInfo cinfo = new CurrentShopInfo();
                        cinfo.ShopTid = tShopProductData.ShopTid;
                        cinfo.ShopCategory = tShopProductData.ShopCategory;
                        cinfo.RenewAt = tShopProductData.RenewAt;
                        cinfo.NextRenewAt = tShopProductData.NextRenewAt;
                        cinfo.FreeRenewCount = tShopProductData.FreeRenewCount;
                        cinfo.RenewCount = tShopProductData.RenewCount;
                        cinfo.ProductId = product.ProductId;
                        cinfo.ProductOrder = product.Order;
                        cinfo.BuyCount = product.BuyCount;
                        cinfo.BuyLimitCount = product.BuyLimitCount;
                        cinfo.CorporationType = product.CorporationType;
                        cinfo.Discount = product.Discount;
                        cinfo.EndAt = product.EndAt;
                        cinfo.UseDateCondition = product.UseDateCondition;
                        SaveShopInfo(cinfo);
                    }

                    netShopDatas.Add(tShopProductData);
                }

                return netShopDatas;
            }
            else
            {
                var allinfo = GetCurrentShopInfo();
                if (allinfo.Count > 0)
                {
                    foreach (var category in allinfo.Keys)
                    {
                        Console.WriteLine($"分类 {category} 有 {allinfo[category].Count} 条数据");
                        NetShopProductData tShopProductData = new NetShopProductData();
                        List<NetShopProductInfoData> tempList = new List<NetShopProductInfoData>();
                        foreach (var item in allinfo[category])
                        {
                            tShopProductData.ShopTid = item.ShopTid;
                            tShopProductData.ShopCategory = item.ShopCategory;
                            tShopProductData.RenewAt = item.RenewAt;
                            tShopProductData.NextRenewAt = item.NextRenewAt;
                            tShopProductData.FreeRenewCount = item.FreeRenewCount;
                            tShopProductData.RenewCount = item.RenewCount;
                            tempList.Add(new NetShopProductInfoData()
                            {
                                Order = item.ProductOrder,
                                ProductId = item.ProductId,
                                BuyLimitCount = item.BuyLimitCount,
                                BuyCount = item.BuyCount,
                                Discount = item.Discount,
                                CorporationType = item.CorporationType,
                                EndAt = item.EndAt,
                                UseDateCondition = item.UseDateCondition
                            });
                        }

                        tShopProductData.List.AddRange(tempList);
                        netShopDatas.Add(tShopProductData);
                    }
                }

                return netShopDatas;
            }
        }
        else
        {
            ClearCurrentShopInfo();
            foreach (var shop in shoplist)
            {
                NetShopProductData tShopProductData = new NetShopProductData();
                tShopProductData.ShopTid = shop.Id;
                tShopProductData.ShopCategory = (int)shop.ShopCategory;
                tShopProductData.RenewAt = DateTime.Now.AddDays(-5).Ticks;
                tShopProductData.NextRenewAt = DateTime.Now.AddDays(13).Ticks;
                tShopProductData.FreeRenewCount = 5;
                tShopProductData.RenewCount = 5;
                GetInfoData(user, shop.Id, ref tShopProductData, shop.BundleId);
                SaveShopDate(new() { LastDay = dateDay, ShopCategory = tShopProductData.ShopCategory });
                var productlist = tShopProductData.List.ToList();
                foreach (var product in productlist)
                {
                    CurrentShopInfo cinfo = new CurrentShopInfo();
                    cinfo.ShopTid = tShopProductData.ShopTid;
                    cinfo.ShopCategory = tShopProductData.ShopCategory;
                    cinfo.RenewAt = tShopProductData.RenewAt;
                    cinfo.NextRenewAt = tShopProductData.NextRenewAt;
                    cinfo.FreeRenewCount = tShopProductData.FreeRenewCount;
                    cinfo.RenewCount = tShopProductData.RenewCount;
                    cinfo.ProductId = product.ProductId;
                    cinfo.ProductOrder = product.Order;
                    cinfo.BuyCount = product.BuyCount;
                    cinfo.BuyLimitCount = product.BuyLimitCount;
                    cinfo.CorporationType = product.CorporationType;
                    cinfo.Discount = product.Discount;
                    cinfo.EndAt = product.EndAt;
                    cinfo.UseDateCondition = product.UseDateCondition;
                    SaveShopInfo(cinfo);
                }

                netShopDatas.Add(tShopProductData);
                user.CurrentShopDate.ShopProduct.TryAdd(tShopProductData.ShopCategory, tShopProductData);
            }

            return netShopDatas;
        }
    }

    public static void UpCountSql(int shopCategory, int shopProductTid, int count)
    {

        LoadShopDate();
        int dateDay = GetDay();
        if (_shopDateCache.TryGetValue(shopCategory, out ShopDate? info))
        {
            var proinfo = GetCurrentShopInfo(shopCategory, shopProductTid);
            if (proinfo.BuyCount <= 0)
            {
                proinfo.BuyCount = count;
                SaveShopInfo(proinfo);
            }
            else if (info.LastDay == dateDay)
            {
                // 记录存在且是今天：累加
                proinfo.BuyCount += count;
                SaveShopInfo(proinfo);
            }
            else
            {

                proinfo.BuyCount = count;
                SaveShopInfo(proinfo);
            }
        }
    }

    public static void UpCount(User user, int shopCategory, int shopProductTid, int count)
    {
        var userBuyCounts = new List<EventShopProductData>();
        int dateDay = user.GetDateDay();
        if (user.ShopBuyCountInfo.TryGetValue(shopCategory, out var userBuyCountInfo))
        {
            userBuyCounts = userBuyCountInfo.datas;
            EventShopProductData? productcountData =
                userBuyCounts.FirstOrDefault(x => x.ProductTid == shopProductTid);
            if (productcountData == null)
            {
                // 记录不存在：新建
                productcountData = new EventShopProductData
                {
                    ProductTid = shopProductTid,
                    BuyCount = count
                };
                userBuyCounts.Add(productcountData);
                userBuyCountInfo.LastDay = dateDay;
            }
            else if (userBuyCountInfo.LastDay == dateDay)
            {
                // 记录存在且是今天：累加
                productcountData.BuyCount += count;
            }
            else
            {
                // 记录存在但不是今天：重置
                productcountData.BuyCount = count;
                userBuyCountInfo.LastDay = dateDay;
            }
        }
        else
        {
            user.ShopBuyCountInfo.Add(shopCategory, new()
            {
                ShopCategory = shopCategory,
                LastDay = dateDay,
                datas = new List<EventShopProductData>
                {
                    new()
                    {
                        ProductTid = shopProductTid,
                        BuyCount = count
                    }
                }
            });
        }
    }


    public static void GetInfoData(User user, int ShopCategory, ref NetShopProductData nspddata, int bundleId)
    {
        // 创建临时列表
        List<NetShopProductInfoData> tempList = new List<NetShopProductInfoData>();
        int dateDay = user.GetDateDay();

        var products = GameData.Instance.ContentsShopProductTable.Values.Where(csp => csp.BundleId == bundleId).ToList();
        var userBuyCounts = new List<EventShopProductData>();
        List<ContentsShopProductRecord> fanilproducts = SelectRandomItems(products);




        if (user.ShopBuyCountInfo.TryGetValue(ShopCategory, out var userBuyCountInfo))
        {
            userBuyCounts = userBuyCountInfo.datas;
            foreach (var csp in fanilproducts)
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
                    Discount = CalculateDiscounted(csp.DiscountProbId)
                });
            }
        }
        else
        {
            foreach (var csp in fanilproducts)
            {
                int buyCount = 0;
                tempList.Add(new NetShopProductInfoData()
                {
                    Order = csp.ProductOrder,
                    ProductId = csp.Id,
                    BuyLimitCount = csp.BuyLimitCount,
                    BuyCount = buyCount,
                    Discount = CalculateDiscounted(csp.DiscountProbId)
                });
            }
        }




        // 将临时列表添加到原对象
        nspddata.List.AddRange(tempList);
    }

    public static void BuyShopMultipleProduct(User user, ref ResShopBuyMultipleProduct response, ReqShopBuyMultipleProduct req)
    {
        bool isSuccess = ExecuteBuyProduct(user, ref response, [.. req.Products]);

        if (!isSuccess) return;
        foreach (var item in req.Products)
        {
            AddShopBuyCount(user, ref response, req.ShopCategory, item.ShopProductTid, item.Quantity, item.Order);
        }
        JsonDb.Save();

        Logging.WriteLine($"BuyShopMultiple 输出response： {response}！", LogType.Debug);
    }

    private static void AddShopBuyCount(User user, ref ResShopBuyMultipleProduct response, int ShopCategory,
        int productTid, int quantity, int order)
    {
        int dateDay = GetDateDay();
        if (!user.ShopBuyCountInfo.TryGetValue(ShopCategory, out var buyCountInfo))
        {
            buyCountInfo = new() { ShopCategory = ShopCategory, LastDay = dateDay, datas = [] };
            buyCountInfo.datas.Add(new() { ProductTid = productTid, BuyCount = quantity });
            user.ShopBuyCountInfo.TryAdd(ShopCategory, buyCountInfo);
        }
        else
        {
            var index = buyCountInfo.datas.FindIndex(x => x.ProductTid == productTid);
            if (index >= 0)
            {
                if (buyCountInfo.LastDay != dateDay) //每天重置购买限制
                {
                    buyCountInfo.datas[index].BuyCount = quantity;
                    response.Product.BuyCounts.Add(new NetBuyCountData
                    {
                        Order = order, BuyCount = buyCountInfo.datas[index].BuyCount
                    });
                }
                else
                {
                    buyCountInfo.datas[index].BuyCount += quantity;
                    response.Product.BuyCounts.Add(new NetBuyCountData
                    {
                        Order = order, BuyCount = buyCountInfo.datas[index].BuyCount
                    });
                }
            }
            else
            {
                buyCountInfo.datas.Add(new() { ProductTid = productTid, BuyCount = quantity });
                response.Product.BuyCounts.Add(new NetBuyCountData { Order = order, BuyCount = quantity });
            }

            user.ShopBuyCountInfo[ShopCategory] = buyCountInfo;
        }

        Logging.WriteLine($"AddShopBuy输出response： {response}！", LogType.Debug);
    }



    private static bool ExecuteBuyProduct(User user, ref ResShopBuyMultipleProduct response,
        List<NetBuyProductRequestData> buyProducts)
    {
        if (buyProducts == null || buyProducts.Count == 0) return false;

        response.Product = new();


        var productTids = buyProducts.Select(p => p.ShopProductTid).ToList();
        var shopProducts = GameData.Instance.ContentsShopProductTable.Values.Where(x => productTids.Contains(x.Id))
            .ToList();



        // Check user currency and item balance
        if (CheckUserCurrencyAndItemBalance(user, shopProducts, buyProducts,
                out Dictionary<int, int> totalCurrencyPrice, out Dictionary<int, int> totalItemPrice))
        {
            Logging.WriteLine($"已完成资源检查！",LogType.Debug);
            // Deduct user currency and item
            DeductUserCurrencyAndItems(user, totalCurrencyPrice, totalItemPrice, ref response);
        }
        else
        {
            Logging.WriteLine($"资源检查出错！", LogType.Debug);
            return false;
        }

        // Process each shopProduct
        foreach (var shopProduct in shopProducts)
        {
            var buyProduct = buyProducts.FirstOrDefault(bp => bp.ShopProductTid == shopProduct.Id);
            int quantity = buyProduct.Quantity;
            int order = buyProduct.Order;
            Logging.WriteLine($"商品类型为{shopProduct.GoodsType}！", LogType.Debug);
            if (shopProduct.GoodsType == RewardType.Item || shopProduct.GoodsType.ToString().StartsWith("Equipment"))
            {
                
                AddItemById(user, ref response, itemId: shopProduct.GoodsId, RewardType.Item, shopProduct.GoodsValue,
                    quantity, order);
            }
            else if (shopProduct.GoodsType == RewardType.Currency)
            {
                long val = shopProduct.GoodsValue * quantity;
                user.AddCurrency((CurrencyType)shopProduct.GoodsId, val);
                // buyCounts.Add(new() { Order = order, BuyCount = quantity });
                response.Product.Currency.Add(new NetCurrencyData()
                {
                    Type = shopProduct.GoodsId, Value = val,
                    FinalValue = user.GetCurrencyVal((CurrencyType)shopProduct.GoodsId)
                });
            }
            else if (shopProduct.GoodsType == RewardType.Character)
            {
                AddCharacterByCharacterTid(user, ref response, shopProduct.GoodsId, shopProduct.GoodsValue, quantity,
                    order);
            }
            else if (shopProduct.GoodsType == RewardType.UserTitle)
            {
                user.AddUnique(user.TitleList, shopProduct.GoodsId);
                response.Product.UserTitleList.Add(shopProduct.GoodsId);
            }
            else if (shopProduct.GoodsType == RewardType.LiveWallpaper)
            {
                user.AddUnique(user.LiveWallpaperList, shopProduct.GoodsId);
                response.Product.LiveWallPapers.Add(shopProduct.GoodsId);
            }
            else
            {
                Logging.WriteLine($"Unsupported GoodsType: {shopProduct.GoodsType}");
            }
        }

        Logging.WriteLine($"ExecuteBuy输出response： {response}！", LogType.Debug);
        return true;
    }

    /// <summary>
    /// 按id添加物品
    /// </summary>
    /// <param name="user"></param>
    /// <param name="response"></param>
    /// <param name="itemId"></param>
    /// <param name="itemType"></param>
    /// <param name="goodsValue"></param>
    /// <param name="quantity"></param>
    /// <param name="order"></param>
    public static void AddItemById(User user, ref ResShopBuyMultipleProduct response,
        int itemId, RewardType itemType, int goodsValue, int quantity, int order)
    {
        var userItemIndex = user.Items.FindIndex(i => i.ItemType == itemId);
        var isEquip = GameData.Instance.ItemEquipTable.TryGetValue(itemId, out var equip);
        if (userItemIndex >= 0)
        {
            Logging.WriteLine($"已有物品 {itemId}！", LogType.Debug);
            if (isEquip)
            {
                // the item is not stackable, we need to create new entries for each quantity
                for (int i = 0; i < goodsValue * quantity; i++)
                {
                    var (tid, pos, isn) = (itemId, GetItemPos(equip.ItemSubType), user.GenerateUniqueItemId());
                    DbItemData newItem = new()
                        { ItemType = tid, Count = 1, Position = pos, Isn = isn, Corp = GetEquipCorp(itemType) };
                    user.Items.Add(newItem);
                    response.Product.Item.Add(NetUtils.ItemDataToNet(newItem));
                    response.Product.UserItems.Add(NetUtils.UserItemDataToNet(newItem));
                }
            }
            else
            {
                Logging.WriteLine($"开始添加物品 {itemId} ，{goodsValue * quantity}个！", LogType.Debug);
                user.Items[userItemIndex].Count += goodsValue * quantity;
                response.Product.UserItems.Add(NetUtils.UserItemDataToNet(user.Items[userItemIndex]));
                var (tid, count, isn) = (itemId, goodsValue * quantity, user.Items[userItemIndex].Isn);
                bool isAddAutoCharge = AddAutoChargeByTid(ref response, itemId: itemId, value: count,
                    finalValue: user.Items[userItemIndex].Count);
                if (!isAddAutoCharge)
                {
                    response.Product.Item.Add(new NetItemData() { Tid = tid, Count = count, Isn = isn ,Corporation = user.Items[userItemIndex].Corp });
                }
            }
        }
        else
        {
            Logging.WriteLine($"未找到物品 {itemId} ，开始新建 {goodsValue * quantity}个！", LogType.Debug);
            var (tid, count, isn) = (itemId, goodsValue * quantity, user.GenerateUniqueItemId());
            DbItemData itemData = new() { ItemType = tid, Count = count, Isn = isn ,Corp = 0};
            user.Items.Add(itemData);
            response.Product.UserItems.Add(NetUtils.UserItemDataToNet(itemData));
            bool isAddAutoCharge = AddAutoChargeByTid(ref response, itemId: itemId, value: count, finalValue: count);
            if (!isAddAutoCharge)
            {
                response.Product.Item.Add(NetUtils.ItemDataToNet(itemData));
            }
        }

        Logging.WriteLine($"AddItem输出response： {response}！", LogType.Debug);
    }


    /// <summary>
    /// 按ID添加人物
    /// </summary>
    /// <param name="user"></param>
    /// <param name="response"></param>
    /// <param name="characterTid"></param>
    /// <param name="goodsValue"></param>
    /// <param name="quantity"></param>
    /// <param name="order"></param>
    public static void AddCharacterByCharacterTid0(User user, ref ResShopBuyMultipleProduct response, int characterTid,
        int goodsValue, int quantity, int order)
    {

        // Get character data from GameData.Instance.CharacterTable
        if (!GameData.Instance.CharacterTable.TryGetValue(characterTid, out var characterRecord))
        {
            return; // Character data not found, return
        }
        int totalBodyLabels = 0;
        
        // Check if character already exists in user.Characters
        var userCharacter = user.GetCharacter(characterTid);
        bool isAddNewCharacter = userCharacter == null;

        if (isAddNewCharacter)
        {
            Logging.WriteLine($"未发现拥有角色 {characterTid} {characterRecord.NameCode},开始新建角色！", LogType.Debug);
            int csn = user.GenerateUniqueCharacterId();
            response.Product.UserCharacters.Add(new NetUserCharacterDefaultData
            {
                CostumeId = 0,
                Csn = csn,
                Grade = 0,
                Lv = 1,
                Skill1Lv = 1,
                Skill2Lv = 1,
                Tid = characterRecord.Id,
                UltiSkillLv = 1
            });
            response.Product.Character.Add(new NetCharacterData
            {
                Csn = user.GenerateUniqueCharacterId(),
                Tid = characterRecord.Id,
            });
            user.Characters.Add(new CharacterModel
            {
                CostumeId = 0,
                Csn = csn,
                Grade = 0,
                Level = 1,
                Skill1Lvl = 1,
                Skill2Lvl = 1,
                Tid = characterRecord.Id,
                UltimateLevel = 1
            });

            // Add "New Character" Badge
            user.AddBadge(BadgeContents.NikkeNew, characterRecord.NameCode.ToString());
            user.AddTrigger(Trigger.ObtainCharacter, 1, characterRecord.NameCode);
            if (characterRecord.OriginalRare == OriginalRareType.SR)
            {
                user.AddTrigger(Trigger.ObtainCharacterSSR, 1);
            }
            else
            {
                user.AddTrigger(Trigger.ObtainCharacterNew, 1, 0);
            }

            if (characterRecord.OriginalRare == OriginalRareType.SSR || characterRecord.OriginalRare == OriginalRareType.SR)
            {
                user.BondInfo.Add(new() { NameCode = characterRecord.NameCode, Lv = 1 });
            }

           

            //Logging.WriteLine($"AddCharacter输出response： {response}！", LogType.Debug);
        }
        else //尚有问题
        {
            Logging.WriteLine($"已拥有角色 {characterTid} {characterRecord.NameCode},开始添加碎片{characterRecord.PieceId}！", LogType.Debug);

            DbItemData? spareItem = user.Items.FirstOrDefault(i => i.ItemType == characterRecord.PieceId);
            if (spareItem == null)
            {
                Logging.WriteLine($"未发现角色 id {characterRecord.PieceId} 的角色碎片", LogType.Warning);
            }
            else
            {
                Logging.WriteLine($"已拥有角色 {characterTid} 碎片{characterRecord.PieceId} - {spareItem.Count}个！", LogType.Debug);
            }

            // If the character already exists, we can increase its piece count
            //如果该角色已存在，我们可以增加其碎片数量
            int maxLimitBroken = GetValueByRarity(characterRecord.OriginalRare, 0, 2, 11) - 1;
            Logging.WriteLine($"maxLimitBroken {maxLimitBroken} ", LogType.Debug);
            int makeadd = maxLimitBroken - (spareItem?.Count ?? 0);
            Logging.WriteLine($"makeadd {makeadd} ", LogType.Debug);
            bool canIncreaseItem = characterRecord.OriginalRare != OriginalRareType.R &&
                                                                  userCharacter.Grade + (spareItem?.Count ?? 0) < maxLimitBroken;
            Logging.WriteLine($"canIncreaseItem {canIncreaseItem} ", LogType.Debug);
            if (canIncreaseItem)
            {
                Logging.WriteLine($"开始添加角色 {characterTid} 碎片{characterRecord.PieceId} - {goodsValue * quantity}个！", LogType.Debug);
                response.Product.Character.Add(GetNetCharacter(userCharacter, goodsValue * quantity));
                AddItemById(user, ref response, itemId: characterRecord.PieceId, RewardType.Item, goodsValue, quantity, order);
            }
            else
            {
                if (makeadd > 0)
                {

                    Logging.WriteLine($"开始添加角色 {characterTid} 碎片{characterRecord.PieceId} - {makeadd}个，其余转化为主体标签！", LogType.Debug);
                    response.Product.Character.Add(GetNetCharacter(userCharacter, makeadd));
                    AddItemById(user, ref response, itemId: characterRecord.PieceId, RewardType.Item, makeadd, 1, order);

                    int bodyLabel = GetValueByRarity(characterRecord.OriginalRare, 150, 200, 6000);

                    //Console.WriteLine($"碎片数量已满，只能加主体标签: {bodyLabel} 个");

                    totalBodyLabels += bodyLabel * (goodsValue * quantity - makeadd);
                    response.Product.Character.Add(GetNetCharacter(userCharacter,0, bodyLabel));
                    response.Product.Currency.Add(new NetCurrencyData() { Type = (int)CurrencyType.DissolutionPoint, Value = totalBodyLabels });
                    user.AddCurrency(CurrencyType.DissolutionPoint, totalBodyLabels);
                }
                else
                {
                    //如果无法增加项目，我们改为提供主体标签
                    int bodyLabel = GetValueByRarity(characterRecord.OriginalRare, 150, 200, 6000);

                    //Console.WriteLine($"碎片数量已满，只能加主体标签: {bodyLabel} 个");

                    totalBodyLabels += bodyLabel * goodsValue * quantity;
                    response.Product.Character.Add(GetNetCharacter(userCharacter,0, bodyLabel));
                    response.Product.Currency.Add(new NetCurrencyData() { Type = (int)CurrencyType.DissolutionPoint, Value = totalBodyLabels });
                    user.AddCurrency(CurrencyType.DissolutionPoint, totalBodyLabels);
                }
               
            }

            Logging.WriteLine($"AddCharacter输出response： {response}！", LogType.Debug);

        }
    }


    public static void AddCharacterByCharacterTid(User user, ref ResShopBuyMultipleProduct response, int characterTid, int goodsValue, int quantity, int order)
    {
        // Get character data from GameData.Instance.CharacterTable
        if (!GameData.Instance.CharacterTable.TryGetValue(characterTid, out var characterRecord))
        {
            return; // Character data not found, return
        }
        // Check if character already exists in user.Characters
        var userCharacter = user.GetCharacter(characterTid);
        bool isAddNewCharacter = userCharacter == null;

        // Calculate character material num
        int characterMaterialNum = isAddNewCharacter ? goodsValue * quantity - 1 : goodsValue * quantity;

        NetCharacterData characterData = new NetCharacterData();

        if (isAddNewCharacter)
        {
            Logging.WriteLine($"未发现拥有角色 {characterTid} {characterRecord.NameCode},开始新建角色！", LogType.Debug);
            int csn = user.GenerateUniqueCharacterId();
            response.Product.UserCharacters.Add(new NetUserCharacterDefaultData
            {
                CostumeId = 0,
                Csn = csn,
                Grade = 0,
                Lv = 1,
                Skill1Lv = 1,
                Skill2Lv = 1,
                Tid = characterRecord.Id,
                UltiSkillLv = 1
            });


            characterData = new NetCharacterData
            {
                Csn = user.GenerateUniqueCharacterId(),
                Tid = characterRecord.Id
            };

            
            user.Characters.Add(new CharacterModel
            {
                CostumeId = 0,
                Csn = csn,
                Grade = 0,
                Level = 1,
                Skill1Lvl = 1,
                Skill2Lvl = 1,
                Tid = characterRecord.Id,
                UltimateLevel = 1
            });

            // Add "New Character" Badge
            user.AddBadge(BadgeContents.NikkeNew, characterRecord.NameCode.ToString());
            user.AddTrigger(Trigger.ObtainCharacter, 1, characterRecord.NameCode);
            if (characterRecord.OriginalRare == OriginalRareType.SR)
            {
                user.AddTrigger(Trigger.ObtainCharacterSSR, 1);
            }
            else
            {
                user.AddTrigger(Trigger.ObtainCharacterNew, 1, 0);
            }

            if (characterRecord.OriginalRare == OriginalRareType.SSR || characterRecord.OriginalRare == OriginalRareType.SR)
            {
                user.BondInfo.Add(new() { NameCode = characterRecord.NameCode, Lv = 1 });
            }


            

            userCharacter = user.GetCharacter(characterTid);
        }

        characterData.Tid = userCharacter.Tid;
        characterData.Csn = userCharacter.Csn;

        if (characterMaterialNum > 0)
        {
           
            // Get max core num
            //int maxCoreNum = currentOriginalRare == OriginalRareType.SSR ? 11 : currentOriginalRare == OriginalRareType.SR ? 3 : 1;
            int maxCoreNum = GetValueByRarity(characterRecord.OriginalRare, 0, 2, 10);//最大可拥有碎片数量
            // Get current core num
            int currentCoreNum =  userCharacter.Grade;
            Logging.WriteLine($"当前角色 {characterTid} 核心等级 {currentCoreNum}！", LogType.Info);

            // If current core num is greater than max core num, set current core num to max core num
            if (currentCoreNum > maxCoreNum) currentCoreNum = maxCoreNum;
            int currentMaterialNum = user.Items.FirstOrDefault(x => x.ItemType == characterRecord.PieceId)?.Count ?? 0;
            Logging.WriteLine($"当前角色 {characterTid} 拥有的核心碎片 {currentMaterialNum} 个！", LogType.Info);
            
            int addMaterialNum = characterMaterialNum; //需要添加的碎片数量

            int addCurrencyNum = 0;

            bool isAddCurrency = currentCoreNum + currentMaterialNum + addMaterialNum > maxCoreNum; //是否需要添加主体标签

            if (isAddCurrency)//若需要添加主体标签，则购买的碎片数量超过最大可拥有核心数
            {
                Logging.WriteLine($"当前角色 {characterTid} 拥有的核心碎片 {currentMaterialNum} 个！ 需要添加标签 ", LogType.Info);
                int MaterialCurrencyNum = GetValueByRarity(characterRecord.OriginalRare, 150, 200, 6000); //当前每个碎片的主体标签数量
                
                addCurrencyNum = (currentCoreNum + currentMaterialNum + addMaterialNum - maxCoreNum) * MaterialCurrencyNum;//计算需要添加的主体标签数

                addMaterialNum = maxCoreNum - currentCoreNum - currentMaterialNum;//碎片可添加数量
            }

            if (addMaterialNum > 0 )
            {
                Logging.WriteLine($"需要添加碎片数量  {addMaterialNum}", LogType.Info);
                AddItemById(user, ref response, itemId: characterRecord.PieceId, RewardType.Item, addMaterialNum, 1, order);
            }

            if (addCurrencyNum > 0)
            {
                Logging.WriteLine($"需要添加标签数量  {addCurrencyNum}", LogType.Info);
                response.Product.Currency.Add(new NetCurrencyData() { Type = (int)CurrencyType.DissolutionPoint, Value = addCurrencyNum });
                user.AddCurrency(CurrencyType.DissolutionPoint, addCurrencyNum);
            }

            characterData.PieceCount = addMaterialNum;
            characterData.CurrencyValue = addCurrencyNum;
        }

        response.Product.Character.Add(characterData);

    }


    public static bool AddAutoChargeByTid(ref ResShopBuyMultipleProduct response, int itemId, int value, int finalValue)
    {
        var autoCharge = GameData.Instance.AutoChargeTable.Values.FirstOrDefault(x => x.ItemId == itemId);
        if (autoCharge is null) return false;
        response.Product.AutoCharge.Add(new NetAutoChargeData()
            { AutoChargeId = autoCharge.Id, Value = value, FinalValue = finalValue });
        return true;
    }

    public static int GetDiscountByProduct(User user, ContentsShopProductRecord sp)
    {
        var shop = GameData.Instance.ContentsShopTable.Values.Where(x => x.BundleId == sp.BundleId).FirstOrDefault();

        int shopCategory = (int)shop.ShopCategory;

        var product = user.CurrentShopDate?.ShopProduct?.GetValueOrDefault(shopCategory)?.List
            ?.Where(dd => dd.ProductId == sp.GoodsId)
            .FirstOrDefault();

        if (product == null)
        {
            return 0;
        }
        else
        {
            return product.Discount;
        }
    }

    /// <summary>
    /// 检查用户资金和物品
    /// </summary>
    /// <param name="user"></param>
    /// <param name="shopProducts"></param>
    /// <param name="buyProducts"></param>
    /// <param name="totalCurrencyPrices"></param>
    /// <param name="totalItemPrices"></param>
    /// <returns></returns>
    private static bool CheckUserCurrencyAndItemBalance(User user, List<ContentsShopProductRecord> shopProducts,
        List<NetBuyProductRequestData> buyProducts,
        out Dictionary<int, int> totalCurrencyPrices, out Dictionary<int, int> totalItemPrices)
    {
       

        totalCurrencyPrices = shopProducts
            .Where(sp => sp.PriceType == PriceType.Currency)
            .GroupBy(sp => sp.PriceId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(sp =>
                {
                    int discount = GetDiscountByProduct(user, sp); // 根据商品获取折扣
                    return sp.PriceValue * ((100 - discount) / 100) *
                        buyProducts.FirstOrDefault(bp => bp.ShopProductTid == sp.Id)?.Quantity ?? 0;
                })
            );
        

        totalItemPrices = shopProducts
            .Where(sp => sp.PriceType == PriceType.Item)
            .GroupBy(sp => sp.PriceId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(sp => sp.PriceValue * buyProducts.FirstOrDefault(bp => bp.ShopProductTid == sp.Id).Quantity)
            );

        Logging.WriteLine($"全部价钱: {JsonConvert.SerializeObject(totalCurrencyPrices)}", LogType.Debug);
        foreach (int currencyType in totalCurrencyPrices.Keys)
        {
            var userCurrency = user.Currency.FirstOrDefault(x => x.Key == (CurrencyType)currencyType).Value;
            if (userCurrency < totalCurrencyPrices[currencyType])
            {
                Logging.WriteLine($"资金不足: 拥有 {userCurrency}, 需要 {totalCurrencyPrices[currencyType]}");
                return false;
            }
        }

        Logging.WriteLine($"全部所需物品: {JsonConvert.SerializeObject(totalItemPrices)}", LogType.Debug);
        foreach (int tid in totalItemPrices.Keys)
        {
            var item = user.Items.FirstOrDefault(i => i.ItemType == tid);
            if (item == null || item.Count < totalItemPrices[tid])
            {
                Logging.WriteLine($"物品不足: 拥有 {item?.Count ?? 0}, 需要 {totalItemPrices[tid]}");
                return false;
            }
        }

        return true;
    }

   


    /// <summary>
    /// 扣除用户资金和物品
    /// </summary>
    /// <param name="user"></param>
    /// <param name="totalCurrencyPrice"></param>
    /// <param name="totalItemPrice"></param>
    /// <param name="response"></param>
    private static void DeductUserCurrencyAndItems(User user, Dictionary<int, int> totalCurrencyPrice,
        Dictionary<int, int> totalItemPrice,
        ref ResShopBuyMultipleProduct response)
    {
        foreach (int key in totalCurrencyPrice.Keys)
        {
            CurrencyType currencyType = (CurrencyType)key;
            user.SubtractCurrency(currencyType, totalCurrencyPrice[key]);
            response.Currencies.Add(new NetUserCurrencyData()
                { Type = key, Value = user.GetCurrencyVal(currencyType) });
        }

        foreach (int tid in totalItemPrice.Keys)
        {
            var item = user.Items.FirstOrDefault(i => i.ItemType == tid);
            user.RemoveItemBySerialNumber(item.Isn, totalItemPrice[tid]);
            response.Items.Add(new NetUserItemData()
                { Tid = tid, Count = user.Items.FirstOrDefault(i => i.ItemType == tid).Count, Isn = item.Isn });
        }

        Logging.WriteLine($"已完成资源扣除！", LogType.Debug);
    }


    public static int GetDateDay()
    {
        // +4 每天4点重新计算 yyyyMMdd
        DateTime dateTime = DateTime.UtcNow.AddHours(4);
        return dateTime.Year * 10000 + dateTime.Month * 100 + dateTime.Day;
    }

    public static int GetItemPos(ItemSubType subType)
    {
        return subType switch
        {
            ItemSubType.ModuleA => 0,
            ItemSubType.ModuleB => 1,
            ItemSubType.ModuleC => 2,
            ItemSubType.ModuleD => 3,
            _ => 0,
        };
    }

    public static int GetEquipCorp(RewardType subType)
    {
        return subType switch
        {
            RewardType.EquipmentELYSION => 1,
            RewardType.EquipmentMISSILIS => 2,
            RewardType.EquipmentTETRA => 3,
            RewardType.EquipmentPILGRIM => 4,
            RewardType.EquipmentABNORMAL => 7,
            _ => 0,
        };
    }

    public static NetUserCharacterDefaultData ToNetUserCharacter(CharacterModel character)
    {
        return new()
        {
            CostumeId = character.CostumeId,
            Csn = character.Csn,
            Grade = character.Grade,
            Lv = character.Level,
            UltiSkillLv = character.UltimateLevel,
            Skill1Lv = character.Skill1Lvl,
            Skill2Lv = character.Skill2Lvl,
            Tid = character.Tid,
        };
    }

   

    public static NetCharacterData GetNetCharacter(CharacterModel character, int pieceCount = 0, int bodyLabel = 0)
    {
        return new NetCharacterData
        {
            Csn = character.Csn,
            Tid = character.Tid,
            PieceCount = pieceCount,
            CurrencyValue = bodyLabel
        };
    }

    public static int GetValueByRarity(OriginalRareType rarity, int rValue, int srValue, int ssrValue) => rarity switch
    {
        OriginalRareType.R => rValue,
        OriginalRareType.SR => srValue,
        OriginalRareType.SSR => ssrValue,
        _ => throw new Exception($"Unknown character rarity: {rarity}")
    };

    public static int GetDay()
    {
        // +4 每天4点重新计算 yyyyMMdd
        DateTime dateTime = DateTime.UtcNow.AddHours(4);
        return dateTime.Year * 10000 + dateTime.Month * 100 + dateTime.Day;
    }
}