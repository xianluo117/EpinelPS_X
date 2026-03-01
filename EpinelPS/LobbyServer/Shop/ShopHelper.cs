using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.LobbyServer.Event.Shop;
using EpinelPS.Utils;
using log4net;
using Newtonsoft.Json;
using Org.BouncyCastle.Ocsp;

namespace EpinelPS.LobbyServer.Shop;

public class ShopHelper
{

    private static readonly ILog log = LogManager.GetLogger(typeof(ShopHelper));


    public static void BuyShopProduct(User user, ref ResShopBuyProduct response, ReqShopBuyProduct req)
    {
        ResShopBuyMultipleProduct MultipleResponse = new();
        List<NetBuyProductRequestData> buyProducts =
            [new() { ShopProductTid = req.ShopProductTid, Quantity = req.Quantity, Order = req.Order }];
        bool isSuccess = ExecuteBuyProduct(user, ref MultipleResponse, buyProducts);

        if (!isSuccess) return;
        AddShopBuyCount(user, ref MultipleResponse, req.ShopCategory, req.ShopProductTid, req.Quantity, req.Order);
        JsonDb.Save();

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

        if (MultipleResponse.Product.Item.Count > 0) response.Product.Item.AddRange(MultipleResponse.Product.Item);

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
            // Deduct user currency and item
            DeductUserCurrencyAndItems(user, totalCurrencyPrice, totalItemPrice, ref response);
        }
        else
        {
            return false;
        }

        // Process each shopProduct
        foreach (var shopProduct in shopProducts)
        {
            var buyProduct = buyProducts.FirstOrDefault(bp => bp.ShopProductTid == shopProduct.Id);
            int quantity = buyProduct.Quantity;
            int order = buyProduct.Order;
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

        JsonDb.Save();

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
            if (isEquip)
            {
                // the item is not stackable, we need to create new entries for each quantity
                for (int i = 0; i < goodsValue * quantity; i++)
                {
                    var (tid, pos, isn) = (itemId, GetItemPos(equip.ItemSubType), user.GenerateUniqueItemId());
                    ItemData newItem = new()
                        { ItemType = tid, Count = 1, Position = pos, Isn = isn, Corp = GetEquipCorp(itemType) };
                    user.Items.Add(newItem);
                    response.Product.Item.Add(NetUtils.ItemDataToNet(newItem));
                    response.Product.UserItems.Add(NetUtils.UserItemDataToNet(newItem));
                }
            }
            else
            {
                user.Items[userItemIndex].Count += goodsValue * quantity;
                response.Product.UserItems.Add(NetUtils.UserItemDataToNet(user.Items[userItemIndex]));
                var (tid, count, isn) = (itemId, goodsValue * quantity, user.Items[userItemIndex].Isn);
                bool isAddAutoCharge = AddAutoChargeByTid(ref response, itemId: itemId, value: count,
                    finalValue: user.Items[userItemIndex].Count);
                if (!isAddAutoCharge)
                {
                    response.Product.Item.Add(new NetItemData() { Tid = tid, Count = count, Isn = isn });
                }
            }
        }
        else
        {
            var (tid, count, isn) = (itemId, goodsValue * quantity, user.GenerateUniqueItemId());
            ItemData itemData = new() { ItemType = tid, Count = count, Isn = isn };
            user.Items.Add(itemData);
            response.Product.UserItems.Add(NetUtils.UserItemDataToNet(itemData));
            bool isAddAutoCharge = AddAutoChargeByTid(ref response, itemId: itemId, value: count, finalValue: count);
            if (!isAddAutoCharge)
            {
                response.Product.Item.Add(NetUtils.ItemDataToNet(itemData));
            }
        }
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
        if (isAddNewCharacter)
        {
            // Add new character to user.Characters
            userCharacter = new CharacterModel()
            {
                Csn = user.GenerateUniqueCharacterId(),
                Grade = 1,
                Tid = characterRecord.Id,
            };
            user.Characters.Add(userCharacter);

            user.AddBadge(BadgeContents.NikkeNew, characterRecord.NameCode.ToString());
            user.AddTrigger(Trigger.ObtainCharacter, 1, characterRecord.NameCode);
            user.AddTrigger(Trigger.ObtainCharacterNew, 1, 0);

            response.Product.UserCharacters.Add(ToNetUserCharacter(userCharacter));
        }
        NetCharacterData netCharacter = new() { Csn = userCharacter.Csn, Tid = userCharacter.Tid };

        // Calculate character material num
        int characterMaterialNum = isAddNewCharacter ? goodsValue * quantity - 1 : goodsValue * quantity;
        if (characterMaterialNum > 0)
        {
            var currentOriginalRare = characterRecord.OriginalRare;
            // Get max core num
            int maxCoreNum = currentOriginalRare == OriginalRareType.SSR ? 11 : currentOriginalRare == OriginalRareType.SR ? 3 : 1;

            // Get current core num
            int currentCoreNum = currentOriginalRare == OriginalRareType.SSR ? userCharacter.Grade : currentOriginalRare == OriginalRareType.SR ? userCharacter.Grade % 100 : 1;
            // If current core num is greater than max core num, set current core num to max core num
            if (currentCoreNum > maxCoreNum) currentCoreNum = maxCoreNum;
            int currentMaterialNum = user.Items.FirstOrDefault(x => x.ItemType == characterRecord.PieceId)?.Count ?? 0;
            bool isAddMaterial = currentCoreNum < maxCoreNum;
            int addMaterialNum = characterMaterialNum - currentMaterialNum;
            int addCurrencyNum = 0;
            bool isAddCurrency = currentCoreNum + addMaterialNum > maxCoreNum;
            if (isAddCurrency)
            {
                int MaterialCurrencyNum = currentOriginalRare == OriginalRareType.SSR ? 6000 : currentOriginalRare == OriginalRareType.SR ? 200 : 150;
                addCurrencyNum = (currentCoreNum + addMaterialNum - maxCoreNum) * MaterialCurrencyNum;
                addMaterialNum = maxCoreNum - currentCoreNum;
            }
            Dictionary<CurrencyType, long> currency = [];
            if (addCurrencyNum > 0)
            {
                netCharacter.CurrencyValue = addCurrencyNum;
                user.AddCurrency(CurrencyType.DissolutionPoint, addCurrencyNum);
                currency.Add(CurrencyType.DissolutionPoint, addCurrencyNum);
            }
            List<ItemData> items = [];
            List<ItemData> userItems = [];
            if (addMaterialNum > 0)
            {
                netCharacter.PieceCount = addMaterialNum;
                AddItemById(user, ref response, characterRecord.PieceId, RewardType.Item, goodsValue, addMaterialNum, order);
            }
            response.Product.Character.Add(netCharacter);
        }

    }


    public static bool AddAutoChargeByTid(ref ResShopBuyMultipleProduct response, int itemId, int value, int finalValue)
    {
        var autoCharge = GameData.Instance.AutoChargeTable.Values.FirstOrDefault(x => x.ItemId == itemId);
        if (autoCharge is null) return false;
        response.Product.AutoCharge.Add(new NetAutoChargeData()
            { AutoChargeId = autoCharge.Id, Value = value, FinalValue = finalValue });
        return true;
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
                    sp.PriceValue * ((100 - sp.DiscountProbId) / 100) *
                    buyProducts.FirstOrDefault(bp => bp.ShopProductTid == sp.Id).Quantity)
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
}