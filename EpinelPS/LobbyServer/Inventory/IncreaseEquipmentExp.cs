using EpinelPS.Database;
using EpinelPS.Data;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Inventory
{
    [PacketPath("/inventory/increaseexpequipment")]
    public class IncreaseEquipmentExp : LobbyMsgHandler
    {
        // readonly Dictionary<int, int> boostExpTable = new()
        // {
        //     { 7010001, 100 },
        //     { 7010002, 1000 },
        //     { 7010003, 10000 }
        // };

        readonly Dictionary<int, int> boostExpTable = new[] { 7010001, 7010002, 7010003 }
            .ToDictionary(id => id, id => GameData.Instance.itemMaterialTable[id].ItemValue);

        protected override async Task HandleAsync()
        {
            ReqIncreaseExpEquip req = await ReadData<ReqIncreaseExpEquip>();
            User user = GetUser();
            ResIncreaseExpEquip response = new();
            ItemData destItem = user.Items.FirstOrDefault(x => x.Isn == req.Isn) ?? throw new NullReferenceException();;
            int goldCost = 0;
            int modules = 0;

            foreach (NetItemData? srcItem in req.ItemList)
            {
                ItemData item = user.Items.FirstOrDefault(x => x.Isn == srcItem.Isn) ?? throw new NullReferenceException();;
                item.Count -= srcItem.Count;

                (int addedExp, int addedModules) = AddExp(srcItem, destItem);
                goldCost += addedExp;
                modules += addedModules;

                response.Items.Add(NetUtils.ToNet(item));
            }

            response.Currency = new NetUserCurrencyData
            {
                Type = (int)CurrencyType.Gold,
                Value = user.GetCurrencyVal(CurrencyType.Gold) - MathUtils.Clamp(goldCost, 0, CalcTotalExp(destItem))
            };

            // TODO: need reward handling function first
            NetRewardData ret = new() { PassPoint = new() };
            if (modules > 0)
            {
                (int t1, int t2, int t3) = CalcModules(modules);

                //Console.WriteLine($"[IncreaseEquipmentExp] ÓÐÊ£Óà¾­Ñé t1 {t1}£¬t2 {t2} £¬t3 {t3} ¸ö¡£");

                if (t1 > 0)
                {
                    RewardUtils.AddSpecifyObject(user, ref ret, 7010001, RewardType.Item, t1);
                }
                if (t2 > 0)
                {
                    RewardUtils.AddSpecifyObject(user, ref ret, 7010002, RewardType.Item, t2);
                }
                if (t3 > 0)
                {
                    RewardUtils.AddSpecifyObject(user, ref ret, 7010003, RewardType.Item, t3);
                }


                response.Reward = ret;

            }

            // we NEED to make sure the target item itself is in the delta list, or the UI won't update!
            response.Items.Add(NetUtils.ToNet(destItem));

            // Add trigger for equipment level count - 升级装备次数
            user.AddTrigger(Trigger.EquipItemLevelCount, 1);

            JsonDb.Save();

            await WriteDataAsync(response);
        }

        (int exp, int modules) AddExp(NetItemData srcItem, ItemData destItem)
        {
            int[] maxLevel = [0, 0, 3, 3, 4, 4, 5, 5, 5, 5];
            ItemEquipRecord? srcEquipRecord = GameData.Instance.ItemEquipTable.Values.FirstOrDefault(x => x.Id == srcItem.Tid);
            ItemEquipRecord destEquipRecord = GameData.Instance.ItemEquipTable.Values.FirstOrDefault(x => x.Id == destItem.ItemType) ?? throw new NullReferenceException();;
            int[] expNextTable = [.. GameData.Instance.itemEquipExpTable.Values
                    .Where(x => x.ItemRare == destEquipRecord.ItemRare)
                    .Select(x => x.Exp)
                    .OrderBy(x => x)];
            int exp = 0;
            int modules = 0;

            if (srcEquipRecord != null)
            {
                ItemEquipGradeExpRecord levelRecord = GameData.Instance.ItemEquipGradeExpTable.Values.FirstOrDefault(x => x.GradeCoreId == srcEquipRecord.GradeCoreId) ?? throw new NullReferenceException();;

                exp = srcItem.Count * levelRecord.Exp;

                destItem.Exp += exp;
            }
            else // if the record is null, boost modules are being used
            {
                foreach (KeyValuePair<int, int> entry in boostExpTable)
                {
                    if (entry.Key == srcItem.Tid)
                    {
                        exp = srcItem.Count * entry.Value;
                        break;
                    }
                }

                destItem.Exp += exp;
            }

            // TODO: double-check this. is this a thing?
            // destItem.Exp += GetSourceExp(srcItem);

            while (destItem.Level < maxLevel[destEquipRecord.GradeCoreId - 1] && destItem.Exp >= expNextTable[destItem.Level + 1] && destItem.Level < maxLevel[destEquipRecord.GradeCoreId - 1])
            {
                destItem.Exp -= expNextTable[destItem.Level + 1];
                destItem.Level++;
            }

            if (destItem.Level >= maxLevel[destEquipRecord.GradeCoreId - 1])
            {
                modules = destItem.Exp; // TODO: check this. is the ratio actually 1:1?
                destItem.Exp = 0;
            }

            return (exp, modules);
        }

        private int GetSourceExp(NetItemData srcItem)
        {
            ItemData item = GetUser().Items.FirstOrDefault(x => x.Isn == srcItem.Isn) ?? throw new NullReferenceException();
            ItemEquipRecord equipRecord = GameData.Instance.ItemEquipTable.Values.FirstOrDefault(x => x.Id == item.ItemType) ?? throw new NullReferenceException();
            ItemEquipGradeExpRecord? levelRecord = GameData.Instance.ItemEquipGradeExpTable.Values.FirstOrDefault(x => x.GradeCoreId == equipRecord.GradeCoreId);
            int[] expNextTable = [.. GameData.Instance.itemEquipExpTable.Values
                .Where(x => x.ItemRare == equipRecord.ItemRare)
                .Select(x => x.Exp)];
            int level = item.Level;
            int exp = item.Exp;

            while (level-- > 0)
            {
                exp += expNextTable[level];
            }

            return exp;
        }

        private static int CalcTotalExp(ItemData destItem)
        {
            int exp = 0;
            ItemEquipRecord equipRecord = GameData.Instance.ItemEquipTable.Values.FirstOrDefault(x => x.Id == destItem.ItemType) ?? throw new NullReferenceException();
            ItemEquipGradeExpRecord? levelRecord = GameData.Instance.ItemEquipGradeExpTable.Values.FirstOrDefault(x => x.GradeCoreId == equipRecord.GradeCoreId);
            int[] expNextTable = [.. GameData.Instance.itemEquipExpTable.Values
                .Where(x => x.ItemRare == equipRecord.ItemRare)
                .Select(x => x.Exp)];

            // skip the first level, it's unused
            for (int i = 1; i < expNextTable.Length; i++)
            {
                exp += expNextTable[i];
            }

            return exp;
        }

        (int t1, int t2, int t3) CalcModules(int exp)
        {
            int t3 = exp / boostExpTable[7010003]; // 最高面值（10000）的物品数量
            exp -= t3 * boostExpTable[7010003];     // 剩余经验

            int t2 = exp / boostExpTable[7010002]; // 次高面值（1000）的物品数量
            exp -= t2 * boostExpTable[7010002];     // 剩余经验

            int t1 = exp / boostExpTable[7010001]; // 最低面值（100）的物品数量

            return (t1, t2, t3);
        }

    }
}
