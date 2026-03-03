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
                    ItemData newItem = new()
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
            ItemData itemData = new() { ItemType = tid, Count = count, Isn = isn ,Corp = 0};
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

            ItemData? spareItem = user.Items.FirstOrDefault(i => i.ItemType == characterRecord.PieceId);
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

            userCharacter = user.GetCharacter(characterTid);
        }
        
        // Calculate character material num
        int characterMaterialNum = isAddNewCharacter ? goodsValue * quantity - 1 : goodsValue * quantity;

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


            response.Product.Character.Add(GetNetCharacter(userCharacter, addMaterialNum, addCurrencyNum));
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
}