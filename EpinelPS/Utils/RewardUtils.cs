using EpinelPS.Data;
using EpinelPS.Database;
using Google.Protobuf.WellKnownTypes;
using Org.BouncyCastle.Ocsp;
using System.IO.Pipelines;
using System.Xml;
using static Google.Rpc.Context.AttributeContext.Types;

namespace EpinelPS.Utils
{
    // Calculate rewards for various messages
    public class RewardUtils
    {
        public static NetRewardData RegisterRewardsForUser(User user, int rewardId)
        {
            RewardRecord rewardData = GameData.Instance.GetRewardTableEntry(rewardId) ?? throw new Exception($"unknown reward Id {rewardId}");
            return RegisterRewardsForUser(user, rewardData);
        }
        public static NetRewardData RegisterRewardsForUser(User user, RewardRecord rewardData)
        {
            NetRewardData ret = new()
            {
                PassPoint = new()
            };
            if (rewardData.Rewards == null) return ret;

            if (rewardData.UserExp != 0)
            {
                

                int newXp = rewardData.UserExp + user.userPointData.ExperiencePoint;

                int newLevelExp = GameData.Instance.GetUserMinXpForLevel(user.userPointData.UserLevel);
                int newLevel = user.userPointData.UserLevel;

                if (newLevelExp == -1)
                {
                    Console.WriteLine("Unknown user level value for xp " + newXp);
                }

                int newGems = 0;

                while (newXp >= newLevelExp)
                {
                    newLevel++;
                    user.AddTrigger(Trigger.UserLevel, newLevel, 0);
                    newGems += 30;
                    newXp -= newLevelExp;
                    if (user.Currency.ContainsKey(CurrencyType.FreeCash))
                        user.Currency[CurrencyType.FreeCash] += 30;
                    else
                        user.Currency.Add(CurrencyType.FreeCash, 30);

                    newLevelExp = GameData.Instance.GetUserMinXpForLevel(newLevel);
                }


                // TODO: what is the difference between IncreaseExp and GainExp
                // NOTE: Current Exp/Lv refers to after XP was added.

                ret.UserExp = new NetIncreaseExpData()
                {
                    BeforeExp = user.userPointData.ExperiencePoint,
                    BeforeLv = user.userPointData.UserLevel,

                    // IncreaseExp = rewardData.UserExp,
                    CurrentExp = newXp,
                    CurrentLv = newLevel,

                    GainExp = rewardData.UserExp,

                };
                user.userPointData.ExperiencePoint = newXp;

                user.userPointData.UserLevel = newLevel;

                Console.WriteLine($"[ClearTower] 奖励经验 : {newXp}");
            }

            foreach (var item in rewardData.Rewards)
            {
                if (item.RewardType != RewardType.None)
                {
                    if (item.RewardPercent != 1000000)
                    {
                        Logging.WriteLine("WARNING: ignoring percent: " + item.RewardPercent / 10000.0 + ", item will be added anyways", LogType.Warning);
                    }

                    AddSingleObject(user, ref ret, item.RewardId, item.RewardType, item.RewardValue);
                }
            }

            return ret;
        }
        public static void AddSingleCurrencyObject(User user, ref NetRewardData ret, CurrencyType currencyType, long rewardCount)
        {
            bool found = user.Currency.Any(pair => pair.Key == currencyType);

            if (found)
            {
                user.Currency[currencyType] += rewardCount;
            }
            else
            {
                user.Currency.Add(currencyType, rewardCount);
            }
            ret.Currency.Add(new NetCurrencyData()
            {
                FinalValue = found ? user.GetCurrencyVal(currencyType) : rewardCount,
                Value = rewardCount,
                Type = (int)currencyType
            });
        }
        /// <summary>
        /// Adds a single item to users inventory, and also adds it to ret parameter.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="ret"></param>
        /// <param name="rewardId"></param>
        /// <param name="rewardType"></param>
        /// <param name="rewardCount"></param>
        /// <exception cref="Exception"></exception>
        public static void AddSingleObject(User user, ref NetRewardData ret, int rewardId, RewardType rewardType, int rewardCount)
        {
            if (rewardType == RewardType.None) return;
            Logging.WriteLine($"[DEBUG]奖励类型{rewardType}",LogType.Info);
            if (rewardType == RewardType.Currency)
            {
                AddSingleCurrencyObject(user, ref ret, (CurrencyType)rewardId, rewardCount);
            }
            else if (rewardType == RewardType.Item || rewardType.ToString().StartsWith("Equipment"))
            {

                int corpId = 0; // Default to 0 (None)

                if (rewardType.ToString().StartsWith("Equipment"))
                {
                    var corpSetting = GameData.Instance.ItemEquipCorpSettingTable.Values.FirstOrDefault(x => x.Key == rewardType);

                    if (corpSetting != null)
                    {
                        if (corpSetting.CorpType == CorporationType.RANDOM)
                        {
                            // Use weighted random selection - all corporations have equal chance
                            // Weights: MISSILIS(1)=20%, ELYSION(2)=20%, TETRA(3)=20%, PILGRIM(4)=20%, ABNORMAL(7)=20%
                            int[] corpIds = { 1, 2, 3, 4, 7 }; // All corporations have equal chance
                            corpId = corpIds[Rng.Next(0, corpIds.Length)];
                        }
                        else
                        {
                            // Directly use the CorpType enum value as integer
                            corpId = (int)corpSetting.CorpType;
                        }
                    }

                }



                // Check if user already has said item. If it is level 1, increase item count.
                ItemData? existingItem = user.Items.FirstOrDefault(x => x.ItemType == rewardId && x.Level == 0 && x.Corp == corpId);

                if (existingItem != null && !rewardType.ToString().StartsWith("Equipment"))
                {
                    existingItem.Count += rewardCount;

                    // Tell the client the reward and its amount
                    ret.Item.Add(new NetItemData()
                    {
                        Count = rewardCount,
                        Tid = rewardId,
                        Corporation = corpId
                    });

                    // Tell the client the new amount of this item
                    ret.UserItems.Add(new NetUserItemData()
                    {
                        Isn = existingItem.Isn,
                        Tid = existingItem.ItemType,
                        Count = existingItem.Count,
                        Corporation = existingItem.Corp
                    });
                }
                else if (rewardType.ToString().StartsWith("Equipment"))
                {

                    Console.WriteLine($"[UseBundleBox] 装备物品 Id{rewardId} ，添加新装备。");

                    int level = 0; // Default to 0
                    ItemSubType itemSubType = GameData.Instance.GetItemSubType(rewardId);

                    // Check if Harmony Cube set level to 1
                    if (itemSubType == ItemSubType.HarmonyCube)
                    {
                        level = 1;
                    }

                    for (int i = 0; i < rewardCount; i++)
                    {
                        int id = user.GenerateUniqueItemId();
                        var newItem = new ItemData() { ItemType = rewardId, Isn = id, Level = level, Exp = 0, Count = 1, Corp = corpId };
                        user.Items.Add(newItem);

                        ret.Item.Add(new NetItemData()
                        {
                            Count = 1,
                            Tid = rewardId,
                            Corporation = corpId
                        });

                        // Tell the client the new amount of this item
                        ret.UserItems.Add(new NetUserItemData()
                        {
                            Isn = newItem.Isn,
                            Tid = newItem.ItemType,
                            Count = newItem.Count,
                            Corporation = newItem.Corp
                        });
                    }

                }
                else
                {
                    int id = user.GenerateUniqueItemId();
                    int level = 0; // Default to 0
                    ItemSubType itemSubType = GameData.Instance.GetItemSubType(rewardId);

                    // Check if Harmony Cube set level to 1
                    if (itemSubType == ItemSubType.HarmonyCube)
                    {
                        level = 1;
                    }
                    var newItem = new ItemData() { ItemType = rewardId, Isn = id, Level = level, Exp = 0, Count = rewardCount, Corp = corpId };
                    user.Items.Add(newItem);

                    ret.Item.Add(new NetItemData()
                    {
                        Count = rewardCount,
                        Tid = rewardId,
                        Corporation = corpId
                    });

                    // Tell the client the new amount of this item
                    ret.UserItems.Add(new NetUserItemData()
                    {
                        Isn = newItem.Isn,
                        Tid = newItem.ItemType,
                        Count = newItem.Count,
                        Corporation = newItem.Corp
                    });
                }
            }
            else if (rewardType == RewardType.Memorial)
            {
                if (!user.Memorial.Contains(rewardId))
                {
                    ret.Memorial.Add(rewardId);
                    user.Memorial.Add(rewardId);
                }
            }
            else if (rewardType == RewardType.Bgm)
            {
                if (!user.JukeboxBgm.Contains(rewardId))
                {
                    ret.JukeboxBgm.Add(rewardId);
                    user.JukeboxBgm.Add(rewardId);
                    var jukebox = GameData.Instance.jukeboxListDataRecords.Values.FirstOrDefault(x => x.Id == rewardId);
                    user.AddTrigger(Trigger.ObtainJukeboxTheme,1,jukebox.Theme);
                }
            }
            else if (rewardType == RewardType.InfraCoreExp)
            {
                int beforeLv = user.InfraCoreLvl;
                int beforeExp = user.InfraCoreExp;

                user.InfraCoreExp += rewardCount;
                user.InfraCoreLvl = GameData.Instance.GetInfraCoreLev(user.InfraCoreExp);
                

                /*// Check for level ups
                Dictionary<int, InfraCoreGradeRecord> gradeTable = GameData.Instance.InfracoreTable;
                int newLevel = user.InfraCoreLvl;

                foreach (var grade in gradeTable.Values.OrderBy(g => g.Grade))
                {
                    if (user.InfraCoreExp >= grade.InfraCoreExp)
                    {
                        newLevel = grade.Grade + 1;
                    }
                    else
                    {
                        break;
                    }
                }

                if (newLevel > user.InfraCoreLvl)
                {
                    user.InfraCoreLvl = newLevel;
                }*/
                Logging.WriteLine($"基础核心经验 {user.InfraCoreExp} ,等级 {user.InfraCoreLvl}");
                ret.InfraCoreExp = new NetIncreaseExpData()
                {
                    BeforeLv = beforeLv,
                    BeforeExp = beforeExp,
                    CurrentLv = user.InfraCoreLvl,
                    CurrentExp = user.InfraCoreExp,
                    GainExp = rewardCount,
                    IncreaseExp = rewardCount
                };
            }
            else if (rewardType == RewardType.ItemRandomBox)
            {
                ItemConsumeRecord? cItem = GameData.Instance.ConsumableItems.Where(x => x.Value.Id == rewardId).FirstOrDefault().Value;

                if (cItem.ItemSubType == ItemSubType.ItemRandomBoxList)
                {
                    NetRewardData reward = NetUtils.UseLootBox(user, rewardId, rewardCount);

                    ret = NetUtils.MergeRewards([ret, reward], user);
                }
                else
                {
                    NetItemData itm = new()
                    {
                        Count = rewardCount,
                        Tid = cItem.Id,
                        Isn = user.GenerateUniqueItemId()
                    };
                    ret.Item.Add(itm);

                    user.Items.Add(new ItemData() { Count = rewardCount, Isn = itm.Isn, ItemType = itm.Tid });
                }
            }
            else if (rewardType == RewardType.FavoriteItem)
            {

                NetUserFavoriteItemData newFavoriteItem = new NetUserFavoriteItemData
                {
                    FavoriteItemId = user.GenerateUniqueItemId(),
                    Tid = rewardId,
                    Csn = 0,
                    Lv = 0,
                    Exp = 0
                };
                user.FavoriteItems.Add(newFavoriteItem);

                ret.UserFavoriteItems.Add(newFavoriteItem);

                NetFavoriteItemData favoriteItemData = new NetFavoriteItemData
                {
                    FavoriteItemId = newFavoriteItem.FavoriteItemId,
                    Tid = newFavoriteItem.Tid,
                    Csn = newFavoriteItem.Csn,
                    Lv = newFavoriteItem.Lv,
                    Exp = newFavoriteItem.Exp
                };
                ret.FavoriteItems.Add(favoriteItemData);

            }
            else if (rewardType == RewardType.Character)
            {

                int totalBodyLabels = 0;
                CharacterRecord? character = GameData.Instance.CharacterTable.Where(x => x.Value.Id == rewardId).FirstOrDefault().Value;
                if (character == null)
                    throw new Exception($"cannot find character record for id {rewardId}");
               

                if (user.GetCharacter(rewardId) is CharacterModel ownedCharacter)
                {
                    ItemData? spareItem = user.Items.FirstOrDefault(i => i.ItemType == character.PieceId);
                    int maxLimitBroken = GetValueByRarity(character.OriginalRare, 0, 2, 11) - 1;
                    Logging.WriteLine($"[UseRandomBox] 角色最大碎片: {maxLimitBroken}，现有碎片数量 {spareItem.Count}");

                    bool canIncreaseItem = character.OriginalRare != OriginalRareType.R && ownedCharacter.Grade + (spareItem?.Count ?? 0) < maxLimitBroken;
                    (int newSpareItemCount, int dissoluteCharacterCount) = canIncreaseItem ? (1, 0) : (0, 1);
                    if (canIncreaseItem)
                    {
                        if (spareItem != null)
                        {
                            //Console.WriteLine($"[UseSelectBox] 增加碎片: {newSpareItemCount}");
                            spareItem.Count += newSpareItemCount;
                        }
                        else
                        {
                            //Console.WriteLine($"[UseSelectBox] 新建碎片: {newSpareItemCount}");
                            spareItem = new()
                            {
                                ItemType = character.PieceId,
                                Csn = 0,
                                Count = newSpareItemCount,
                                Level = 0,
                                Exp = 0,
                                Position = 0,
                                Corp = 0,
                                Isn = user.GenerateUniqueItemId()
                            };
                            user.Items.Add(spareItem);
                        }

                        ret.Item.Add(new NetItemData()
                        {
                            Count = spareItem.Count,
                            Tid = spareItem.ItemType,
                            Corporation = spareItem.Corp
                        });


                        //ret.UserItems.Add(NetUtils.UserItemDataToNet(spareItem));
                        //ret.Character.Add(GetNetCharacterData(ownedCharacter));

                        // Tell the client the new amount of this item
                        ret.UserItems.Add(new NetUserItemData()
                        {
                            Isn = spareItem.Isn,
                            Tid = spareItem.ItemType,
                            Count = spareItem.Count,
                            Corporation = spareItem.Corp
                        });
                    }
                    else
                    {
                        // If we cannot increase the item, we give body label instead
                        //如果无法增加项目，我们改为提供主体标签

                        int bodyLabel = GetValueByRarity(character.OriginalRare, 150, 200, 6000);

                        //Console.WriteLine($"[UseSelectBox] 碎片数量已满，只能加主体标签: {bodyLabel} 个");

                        totalBodyLabels += bodyLabel * dissoluteCharacterCount;
                        ret.Character.Add(GetNetCharacterData(ownedCharacter, bodyLabel));
                        ret.Currency.Add(new NetCurrencyData() { Type = (int)CurrencyType.DissolutionPoint, Value = totalBodyLabels });
                        user.AddCurrency(CurrencyType.DissolutionPoint, totalBodyLabels);
                    }
                }
                else
                {
                    //Console.WriteLine($"[UseSelectBox] 角色不存在，添加角色。");
                    int csn = user.GenerateUniqueCharacterId();
                    ret.UserCharacters.Add(new NetUserCharacterDefaultData
                    {
                        CostumeId = 0,
                        Csn = csn,
                        Grade = 0,
                        Lv = 1,
                        Skill1Lv = 1,
                        Skill2Lv = 1,
                        Tid = character.Id,
                        UltiSkillLv = 1
                    });
                    ret.Character.Add(new NetCharacterData
                    {
                        Csn = user.GenerateUniqueCharacterId(),
                        Tid = character.Id,
                    });
                    user.Characters.Add(new CharacterModel
                    {
                        CostumeId = 0,
                        Csn = csn,
                        Grade = 0,
                        Level = 1,
                        Skill1Lvl = 1,
                        Skill2Lvl = 1,
                        Tid = character.Id,
                        UltimateLevel = 1
                    });

                    // Add "New Character" Badge
                    user.AddBadge(BadgeContents.NikkeNew, character.NameCode.ToString());
                    user.AddTrigger(Trigger.ObtainCharacter, 1, character.NameCode);
                    if (character.OriginalRare == OriginalRareType.SR)
                    {
                        user.AddTrigger(Trigger.ObtainCharacterSSR, 1);
                    }
                    else
                    {
                        user.AddTrigger(Trigger.ObtainCharacterNew, 1, 0);
                    }

                    if (character.OriginalRare == OriginalRareType.SSR || character.OriginalRare == OriginalRareType.SR)
                    {
                        user.BondInfo.Add(new() { NameCode = character.NameCode, Lv = 1 });
                    }
                }
            }
            else if (rewardType == RewardType.UserTitle)
            {
                UserTitleRecord? record = GameData.Instance.userTitleRecords.Values.FirstOrDefault(x => x.Id == rewardId);
                if (record != null)
                {
                    ret.UserTitleList.Add(record.Id);
                    user.TitleList.Add(record.Id);
                }
            }
            else if (rewardType == RewardType.Frame)
            {
                Logging.WriteLine($"添加边框{rewardId}",LogType.Info);
                UserFrameRecord? record = GameData.Instance.userFrameTable.Values.FirstOrDefault(x => x.Id == rewardId);
                if (record != null)
                {
                    NetProfileFrameData frameData = new NetProfileFrameData
                    {
                        FrameTid = record.Id,
                        AcquiredAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
                    };

                    ret.ProfileFrame.Add(frameData);
                    user.AddUnique(user.FrameList,record.Id);
                    
                }
            }
            else if (rewardType == RewardType.CharacterCostume)
            {
                Logging.WriteLine($"添加服装{rewardId}", LogType.Info);
                var record = GameData.Instance.CharacterCostumeTable.Values.FirstOrDefault(x => x.Id == rewardId);
                if (record != null)
                {
                    ret.CharacterCostume.Add(record.Id);
                    user.AddUnique(user.CostumeList, record.Id);
                }
            }
            else
            {
                Logging.WriteLine("TODO: Reward type " + rewardType, LogType.Warning);
            }
        }
        /// <summary>
        /// 添加可选择物品
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="ret"></param>
        /// <param name="rewardId">物品ID</param>
        /// <param name="rewardType">物品类型</param>
        /// <param name="rewardCount">物品数量</param>
        /// <exception cref="Exception"></exception>
        public static void AddSelectObject(User user, ref NetRewardData ret, int rewardId, RewardType rewardType, int rewardCount)
        {

            if (rewardId == 0 || rewardType == RewardType.None) return;

            if (rewardType == RewardType.Item || rewardType.ToString().StartsWith("Equipment"))
            {

                int corpId = 0; // Default to 0 (None)

                if (rewardType.ToString().StartsWith("Equipment"))
                {
                    var corpSetting = GameData.Instance.ItemEquipCorpSettingTable.Values.FirstOrDefault(x => x.Key == rewardType);

                    if (corpSetting != null)
                    {
                        if (corpSetting.CorpType == CorporationType.RANDOM)
                        {
                            // Use weighted random selection - all corporations have equal chance
                            // Weights: MISSILIS(1)=20%, ELYSION(2)=20%, TETRA(3)=20%, PILGRIM(4)=20%, ABNORMAL(7)=20%
                            int[] corpIds = { 1, 2, 3, 4, 7 }; // All corporations have equal chance
                            corpId = corpIds[Rng.Next(0, corpIds.Length)];
                        }
                        else
                        {
                            // Directly use the CorpType enum value as integer
                            corpId = (int)corpSetting.CorpType;
                        }
                    }

                }

                var isEquipRecord = GameData.Instance.ItemEquipTable.Values.FirstOrDefault(x => x.Id == rewardId);

                if (isEquipRecord != null)
                {
                    int[] corpIds = { 1, 2, 3, 4, 7 }; // All corporations have equal chance
                    corpId = corpIds[Rng.Next(0, corpIds.Length)];

                    Console.WriteLine($"[UseSelectBox] 检测到装备物品 Id{rewardId} ，添加新装备。");

                    int level = 0; // Default to 0
                    ItemSubType itemSubType = GameData.Instance.GetItemSubType(rewardId);

                    // Check if Harmony Cube set level to 1
                    if (itemSubType == ItemSubType.HarmonyCube)
                    {
                        level = 1;
                    }

                    for (int i = 0; i < rewardCount; i++)
                    {
                        int id = user.GenerateUniqueItemId();
                        var newItem = new ItemData()
                            { ItemType = rewardId, Isn = id, Level = level, Exp = 0, Count = 1, Corp = corpId };
                        user.Items.Add(newItem);

                        ret.Item.Add(new NetItemData()
                        {
                            Count = 1,
                            Tid = rewardId,
                            Corporation = corpId
                        });

                        // Tell the client the new amount of this item
                        ret.UserItems.Add(new NetUserItemData()
                        {
                            Isn = newItem.Isn,
                            Tid = newItem.ItemType,
                            Count = newItem.Count,
                            Corporation = newItem.Corp
                        });
                    }

                }
                else
                {


                    // Check if user already has said item. If it is level 1, increase item count.
                    ItemData? existingItem = user.Items.FirstOrDefault(x => x.ItemType == rewardId && x.Corp == corpId);

                    if (existingItem != null && !rewardType.ToString().StartsWith("Equipment"))
                    {

                        Console.WriteLine($"[UseSelectBox] 发现已存在物品 Id{existingItem.ItemType}，添加到已有物品 {rewardCount} 个。");
                        existingItem.Count += rewardCount;

                        // Tell the client the reward and its amount
                        ret.Item.Add(new NetItemData()
                        {
                            Count = rewardCount,
                            Tid = rewardId,
                            Corporation = corpId
                        });

                        // Tell the client the new amount of this item
                        ret.UserItems.Add(new NetUserItemData()
                        {
                            Isn = existingItem.Isn,
                            Tid = existingItem.ItemType,
                            Count = existingItem.Count,
                            Corporation = existingItem.Corp
                        });
                    }
                    else if (rewardType.ToString().StartsWith("Equipment"))
                    {

                        Console.WriteLine($"[UseSelectBox] 装备物品 Id{rewardId} ，添加新装备。");

                        int level = 0; // Default to 0
                        ItemSubType itemSubType = GameData.Instance.GetItemSubType(rewardId);

                        // Check if Harmony Cube set level to 1
                        if (itemSubType == ItemSubType.HarmonyCube)
                        {
                            level = 1;
                        }

                        for (int i = 0; i < rewardCount; i++)
                        {
                            int id = user.GenerateUniqueItemId();
                            var newItem = new ItemData()
                                { ItemType = rewardId, Isn = id, Level = level, Exp = 0, Count = 1, Corp = corpId };
                            user.Items.Add(newItem);

                            ret.Item.Add(new NetItemData()
                            {
                                Count = 1,
                                Tid = rewardId,
                                Corporation = corpId
                            });

                            // Tell the client the new amount of this item
                            ret.UserItems.Add(new NetUserItemData()
                            {
                                Isn = newItem.Isn,
                                Tid = newItem.ItemType,
                                Count = newItem.Count,
                                Corporation = newItem.Corp
                            });
                        }

                    }
                    else
                    {
                        Console.WriteLine($"[UseSelectBox] 物品 Id{rewardId} 不存在，添加新物品物品 {rewardCount} 个。");
                        int id = user.GenerateUniqueItemId();
                        int level = 0; // Default to 0
                        ItemSubType itemSubType = GameData.Instance.GetItemSubType(rewardId);

                        // Check if Harmony Cube set level to 1
                        if (itemSubType == ItemSubType.HarmonyCube)
                        {
                            level = 1;
                        }

                        var newItem = new ItemData()
                        {
                            ItemType = rewardId, Isn = id, Level = level, Exp = 0, Count = rewardCount, Corp = corpId
                        };
                        user.Items.Add(newItem);

                        ret.Item.Add(new NetItemData()
                        {
                            Count = rewardCount,
                            Tid = rewardId,
                            Corporation = corpId
                        });

                        // Tell the client the new amount of this item
                        ret.UserItems.Add(new NetUserItemData()
                        {
                            Isn = newItem.Isn,
                            Tid = newItem.ItemType,
                            Count = newItem.Count,
                            Corporation = newItem.Corp
                        });
                    }
                }
            }
            else if (rewardType == RewardType.Character)
            {
                for (int i = 0; i < rewardCount; i++)
                {
                    Logging.WriteLine($"[UseSelectBox] 自选角色 - Id: {rewardId}, 角色数量 {1}");

                    RewardUtils.AddSelectRowObject(user, ref ret, rewardId, rewardType, 1);
                }


            }

        }

        /// <summary>
        /// 添加指定物品
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="ret"></param>
        /// <param name="rewardId">物品ID</param>
        /// <param name="rewardType">物品类型</param>
        /// <param name="rewardCount">物品数量</param>
        /// <exception cref="Exception"></exception>
        public static void AddSpecifyObject(User user, ref NetRewardData ret, int rewardId, RewardType rewardType, int rewardCount)
        {
            if (rewardId == 0 || rewardType == RewardType.None) return;

            if (rewardType == RewardType.Item)
            {

                int corpId = 0; // Default to 0 (None)

                // Check if user already has said item. If it is level 1, increase item count.
                ItemData? existingItem = user.Items.FirstOrDefault(x => x.ItemType == rewardId && x.Corp == corpId);

                if (existingItem != null)
                {

                    Console.WriteLine($"[AddSpecifyObject] 发现已存在物品 Id{existingItem.ItemType}，添加到已有物品 {rewardCount} 个。");
                    existingItem.Count += rewardCount;
                    
                    // Tell the client the reward and its amount
                    ret.Item.Add(new NetItemData()
                    {
                        Count = rewardCount,
                        Tid = rewardId,
                        Corporation = corpId
                    });

                    // Tell the client the new amount of this item
                    ret.UserItems.Add(new NetUserItemData()
                    {
                        Isn = existingItem.Isn,
                        Tid = existingItem.ItemType,
                        Count = existingItem.Count,
                        Corporation = existingItem.Corp
                    });
                }
                else
                {
                    Console.WriteLine($"[AddSpecifyObject] 物品 Id{rewardId} 不存在，添加新物品物品 {rewardCount} 个。");
                    int id = user.GenerateUniqueItemId();
                    int level = 1; // Default to 0

                    var newItem = new ItemData() { ItemType = rewardId, Isn = id, Level = level, Exp = 1, Count = rewardCount, Corp = corpId };
                    user.Items.Add(newItem);

                    ret.Item.Add(new NetItemData()
                    {
                        Count = rewardCount,
                        Tid = rewardId,
                        Corporation = corpId
                    });

                    // Tell the client the new amount of this item
                    ret.UserItems.Add(new NetUserItemData()
                    {
                        Isn = newItem.Isn,
                        Tid = newItem.ItemType,
                        Lv = newItem.Level,
                        Exp = newItem.Exp,
                        Count = newItem.Count,
                        Corporation = newItem.Corp
                    });
                }
            }

        }


        /// <summary>
        /// 添加礼包物品
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="ret"></param>
        /// <param name="rewardId">物品ID</param>
        /// <param name="rewardType">物品类型</param>
        /// <param name="rewardCount">物品数量</param>
        /// <exception cref="Exception"></exception>
        public static void AddBundleObject(User user, ref NetRewardData ret, int rewardId, RewardType rewardType, int rewardCount)
        {
            if (rewardId == 0 || rewardType == RewardType.None) return;

            if (rewardType == RewardType.Item || rewardType.ToString().StartsWith("Equipment"))
            {

                int corpId = 0; // Default to 0 (None)

                if (rewardType.ToString().StartsWith("Equipment"))
                {
                    var corpSetting = GameData.Instance.ItemEquipCorpSettingTable.Values.FirstOrDefault(x => x.Key == rewardType);

                    if (corpSetting != null)
                    {
                        if (corpSetting.CorpType == CorporationType.RANDOM)
                        {
                            // Use weighted random selection - all corporations have equal chance
                            // Weights: MISSILIS(1)=20%, ELYSION(2)=20%, TETRA(3)=20%, PILGRIM(4)=20%, ABNORMAL(7)=20%
                            int[] corpIds = { 1, 2, 3, 4, 7 }; // All corporations have equal chance
                            corpId = corpIds[Rng.Next(0, corpIds.Length)];
                        }
                        else
                        {
                            // Directly use the CorpType enum value as integer
                            corpId = (int)corpSetting.CorpType;
                        }
                    }

                }

                var isEquipRecord = GameData.Instance.ItemEquipTable.Values.FirstOrDefault(x => x.Id == rewardId);

                if (isEquipRecord != null)
                {
                    int[] corpIds = { 1, 2, 3, 4, 7 }; // All corporations have equal chance
                    corpId = corpIds[Rng.Next(0, corpIds.Length)];

                    Console.WriteLine($"[UseBundleBox] 检测到装备物品 Id{rewardId} ，添加新装备。");

                    int level = 0; // Default to 0
                    ItemSubType itemSubType = GameData.Instance.GetItemSubType(rewardId);

                    // Check if Harmony Cube set level to 1
                    if (itemSubType == ItemSubType.HarmonyCube)
                    {
                        level = 1;
                    }

                    for (int i = 0; i < rewardCount; i++)
                    {
                        int id = user.GenerateUniqueItemId();
                        var newItem = new ItemData()
                            { ItemType = rewardId, Isn = id, Level = level, Exp = 0, Count = 1, Corp = corpId };
                        user.Items.Add(newItem);

                        ret.Item.Add(new NetItemData()
                        {
                            Count = 1,
                            Tid = rewardId,
                            Corporation = corpId
                        });

                        // Tell the client the new amount of this item
                        ret.UserItems.Add(new NetUserItemData()
                        {
                            Isn = newItem.Isn,
                            Tid = newItem.ItemType,
                            Count = newItem.Count,
                            Corporation = newItem.Corp
                        });
                    }

                }
                else
                {

                    // Check if user already has said item. If it is level 1, increase item count.
                    ItemData? existingItem = user.Items.FirstOrDefault(x => x.ItemType == rewardId && x.Corp == corpId);

                    if (existingItem != null && !rewardType.ToString().StartsWith("Equipment"))
                    {
                        Console.WriteLine($"[UseBundleBox] 发现已存在物品 Id{existingItem.ItemType}，添加到已有物品 {rewardCount} 个。");
                        existingItem.Count += rewardCount;

                        // Tell the client the reward and its amount
                        ret.Item.Add(new NetItemData()
                        {
                            Count = rewardCount,
                            Tid = rewardId,
                            Corporation = corpId
                        });

                        // Tell the client the new amount of this item
                        ret.UserItems.Add(new NetUserItemData()
                        {
                            Isn = existingItem.Isn,
                            Tid = existingItem.ItemType,
                            Count = existingItem.Count,
                            Corporation = existingItem.Corp
                        });
                    }
                    else if (rewardType.ToString().StartsWith("Equipment"))
                    {

                        Console.WriteLine($"[UseBundleBox] 装备物品 Id{rewardId} ，添加新装备。");

                        int level = 0; // Default to 0
                        ItemSubType itemSubType = GameData.Instance.GetItemSubType(rewardId);

                        // Check if Harmony Cube set level to 1
                        if (itemSubType == ItemSubType.HarmonyCube)
                        {
                            level = 1;
                        }

                        for (int i = 0; i < rewardCount; i++)
                        {
                            int id = user.GenerateUniqueItemId();
                            var newItem = new ItemData()
                                { ItemType = rewardId, Isn = id, Level = level, Exp = 0, Count = 1, Corp = corpId };
                            user.Items.Add(newItem);

                            ret.Item.Add(new NetItemData()
                            {
                                Count = 1,
                                Tid = rewardId,
                                Corporation = corpId
                            });

                            // Tell the client the new amount of this item
                            ret.UserItems.Add(new NetUserItemData()
                            {
                                Isn = newItem.Isn,
                                Tid = newItem.ItemType,
                                Count = newItem.Count,
                                Corporation = newItem.Corp
                            });
                        }

                    }
                    else
                    {
                        Console.WriteLine($"[UseBundleBox] 物品 Id{rewardId} 不存在，添加新物品物品 {rewardCount} 个。");
                        int id = user.GenerateUniqueItemId();
                        int level = 0; // Default to 0
                        ItemSubType itemSubType = GameData.Instance.GetItemSubType(rewardId);

                        // Check if Harmony Cube set level to 1
                        if (itemSubType == ItemSubType.HarmonyCube)
                        {
                            level = 1;
                        }

                        var newItem = new ItemData()
                        {
                            ItemType = rewardId, Isn = id, Level = level, Exp = 0, Count = rewardCount, Corp = corpId
                        };
                        user.Items.Add(newItem);

                        ret.Item.Add(new NetItemData()
                        {
                            Count = rewardCount,
                            Tid = rewardId,
                            Corporation = corpId
                        });

                        // Tell the client the new amount of this item
                        ret.UserItems.Add(new NetUserItemData()
                        {
                            Isn = newItem.Isn,
                            Tid = newItem.ItemType,
                            Count = newItem.Count,
                            Corporation = newItem.Corp
                        });
                    }
                }
            }
            
        }

        public static void AddSelectCharacter(User user,ref ResExecuteGacha response, int characterId)
        {
            int totalBodyLabels = 0;

            CharacterRecord? character = GameData.Instance.CharacterTable.Where(x => x.Value.Id == characterId).FirstOrDefault().Value;
            if (character == null)
                throw new Exception($"cannot find character record for id {characterId}");

            

            if (user.GetCharacter(characterId) is CharacterModel ownedCharacter)
            {
                 Console.WriteLine($"[UsePiece] 角色已存在，添加碎片。");
                ItemData? spareItem = user.Items.FirstOrDefault(i => i.ItemType == character.PieceId);
                int maxLimitBroken = GetValueByRarity(character.OriginalRare, 0, 2, 11) - 1;
                switch (spareItem)
                {
                    case null:
                        Console.WriteLine($"[UsePiece] 角色最大碎片: {maxLimitBroken}，现有碎片数量 无 ");
                        //throw new Exception($"cannot find item record for id {spareItem}");
                        break;
                    default:
                        Console.WriteLine($"[UsePiece] 角色最大碎片: {maxLimitBroken}，现有碎片数量 {spareItem.Count}");
                        break;
                }


                bool canIncreaseItem = character.OriginalRare != OriginalRareType.R && ownedCharacter.Grade + (spareItem?.Count ?? 0) < maxLimitBroken;

                //Console.WriteLine($"[UseSelectBox] 是否可以增加碎片: {canIncreaseItem}");

                (int newSpareItemCount, int dissoluteCharacterCount) = canIncreaseItem ? (1, 0) : (0, 1);
                if (canIncreaseItem)
                {
                    if (spareItem != null)
                    {
                        Console.WriteLine($"[UsePiece] 增加碎片: {newSpareItemCount}");
                        spareItem.Count += newSpareItemCount;

                        // ret.Item.Add(new NetItemData()
                        // {
                        //     Count = spareItem.Count,
                        //     Tid = spareItem.ItemType,
                        //     Corporation = spareItem.Corp
                        // });

                        // Send the updated item in the response
                        response.Items.Add(new NetUserItemData()
                        {
                            Tid = spareItem.ItemType,
                            Csn = spareItem.Csn,
                            Count = spareItem.Count,
                            Lv = spareItem.Level,
                            Exp = spareItem.Exp,
                            Position = spareItem.Position,
                            Isn = spareItem.Isn
                        });

                    }
                    else
                    {
                        Console.WriteLine($"[UsePiece] 新建碎片: {newSpareItemCount}");
                        spareItem = new()
                        {
                            ItemType = character.PieceId,
                            Csn = 0,
                            Count = newSpareItemCount,
                            Level = 0,
                            Exp = 0,
                            Position = 0,
                            Corp = 0,
                            Isn = user.GenerateUniqueItemId()
                        };
                        user.Items.Add(spareItem);

                        // ret.Item.Add(new NetItemData()
                        // {
                        //     Count = spareItem.Count,
                        //     Tid = spareItem.ItemType,
                        //     Corporation = spareItem.Corp
                        // });

                        // Tell the client the new amount of this item
                        response.Items.Add(new NetUserItemData()
                        {
                            Tid = spareItem.ItemType,
                            Csn = spareItem.Csn,
                            Count = spareItem.Count,
                            Lv = spareItem.Level,
                            Exp = spareItem.Exp,
                            Position = spareItem.Position,
                            Isn = spareItem.Isn
                        });


                    }

                   

                    Console.WriteLine($"[UsePiece] 增加碎片: {spareItem.ItemType}-{spareItem.Count}");
                }
                else
                {
                    // If we cannot increase the item, we give body label instead
                    //如果无法增加项目，我们改为提供主体标签

                    int bodyLabel = GetValueByRarity(character.OriginalRare, 150, 200, 6000);

                    Console.WriteLine($"[UsePiece] 碎片数量已满，只能加主体标签: {bodyLabel} 个");

                    totalBodyLabels += bodyLabel * dissoluteCharacterCount;
                    response.Reward.Character.Add(GetNetCharacterData(ownedCharacter, bodyLabel));
                    response.Reward.Currency.Add(new NetCurrencyData() { Type = (int)CurrencyType.DissolutionPoint, Value = totalBodyLabels });
                    user.AddCurrency(CurrencyType.DissolutionPoint, totalBodyLabels);
                }
            }
            else
            {
                Console.WriteLine($"[UsePiece] 角色不存在，添加角色。");
                int csn = user.GenerateUniqueCharacterId();
                response.Characters.Add(new NetUserCharacterDefaultData
                {
                    CostumeId = 0,
                    Csn = csn,
                    Grade = 0,
                    Lv = 1,
                    Skill1Lv = 1,
                    Skill2Lv = 1,
                    Tid = character.Id,
                    UltiSkillLv = 1
                });
                
                user.Characters.Add(new CharacterModel
                {
                    CostumeId = 0,
                    Csn = csn,
                    Grade = 0,
                    Level = 1,
                    Skill1Lvl = 1,
                    Skill2Lvl = 1,
                    Tid = character.Id,
                    UltimateLevel = 1
                });

                // Add "New Character" Badge
                user.AddBadge(BadgeContents.NikkeNew, character.NameCode.ToString());
                user.AddTrigger(Trigger.ObtainCharacter, 1, character.NameCode);
                if (character.OriginalRare == OriginalRareType.SR)
                {
                    user.AddTrigger(Trigger.ObtainCharacterSSR, 1);
                }
                else
                {
                    user.AddTrigger(Trigger.ObtainCharacterNew, 1, 0);
                }

                if (character.OriginalRare == OriginalRareType.SSR || character.OriginalRare == OriginalRareType.SR)
                {
                    user.BondInfo.Add(new() { NameCode = character.NameCode, Lv = 1 });
                }
            }


            
        }

        public static void AddSelectRowObject(User user, ref NetRewardData ret, int characterId, RewardType rewardType, int rewardValue)
        {
            int totalBodyLabels = 0;
            CharacterRecord? character = GameData.Instance.CharacterTable.Where(x => x.Value.Id == characterId).FirstOrDefault().Value;
            if (character == null)
                throw new Exception($"cannot find character record for id {characterId}");

            // Console.WriteLine($"[UseSelectBox] 自选角色 - Id: {character.Id}");

            if (user.GetCharacter(characterId) is CharacterModel ownedCharacter)
            {
                Console.WriteLine($"[UseSelectBox] 角色已存在，获取碎片 - Id: {character.PieceId}");
                ItemData? spareItem = user.Items.FirstOrDefault(i => i.ItemType == character.PieceId);
                if (spareItem == null)
                {
                    Logging.WriteLine($"未发现角色 id {character.PieceId} 的角色碎片",LogType.Warning);
                }

                // If the character already exists, we can increase its piece count
                //如果该角色已存在，我们可以增加其碎片数量
                int maxLimitBroken = GetValueByRarity(character.OriginalRare, 0, 2, 11) - 1;
                Console.WriteLine($"[UseSelectBox] 角色最大碎片: {maxLimitBroken}，现有碎片数量 {spareItem?.Count ?? 0}");

                bool canIncreaseItem = character.OriginalRare != OriginalRareType.R &&
                                       ownedCharacter.Grade + (spareItem?.Count ?? 0) < maxLimitBroken;

                Console.WriteLine($"[UseSelectBox] 是否可以增加碎片: {canIncreaseItem}");

                (int newSpareItemCount, int dissoluteCharacterCount) = canIncreaseItem ? (1, 0) : (0, 1);
                if (canIncreaseItem)
                {
                    if (spareItem != null)
                    {
                        Console.WriteLine($"[UseSelectBox] 增加碎片: {newSpareItemCount}");
                        spareItem.Count += newSpareItemCount;
                    }
                    else
                    {
                        Console.WriteLine($"[UseSelectBox] 新建碎片: {newSpareItemCount}");
                        spareItem = new()
                        {
                            ItemType = character.PieceId,
                            Csn = 0,
                            Count = newSpareItemCount,
                            Level = 0,
                            Exp = 0,
                            Position = 0,
                            Corp = 0,
                            Isn = user.GenerateUniqueItemId()
                        };
                        user.Items.Add(spareItem);
                    }

                    ret.Item.Add(new NetItemData()
                    {
                        Count = spareItem.Count,
                        Tid = spareItem.ItemType,
                        Corporation = spareItem.Corp
                    });


                    //ret.UserItems.Add(NetUtils.UserItemDataToNet(spareItem));
                    //ret.Character.Add(GetNetCharacterData(ownedCharacter));

                    // Tell the client the new amount of this item
                    ret.UserItems.Add(new NetUserItemData()
                    {
                        Isn = spareItem.Isn,
                        Tid = spareItem.ItemType,
                        Count = spareItem.Count,
                        Corporation = spareItem.Corp
                    });
                }
                else
                {
                    // If we cannot increase the item, we give body label instead
                    //如果无法增加项目，我们改为提供主体标签
                   
                    int bodyLabel = GetValueByRarity(character.OriginalRare, 150, 200, 6000);

                    //Console.WriteLine($"[UseSelectBox] 碎片数量已满，只能加主体标签: {bodyLabel} 个");

                    totalBodyLabels += bodyLabel * dissoluteCharacterCount;
                    ret.Character.Add(GetNetCharacterData(ownedCharacter, bodyLabel));
                    ret.Currency.Add(new NetCurrencyData() { Type = (int)CurrencyType.DissolutionPoint, Value = totalBodyLabels });
                    user.AddCurrency(CurrencyType.DissolutionPoint, totalBodyLabels);
                }
            }
            else
            {
                //Console.WriteLine($"[UseSelectBox] 角色不存在，添加角色。");
                int csn = user.GenerateUniqueCharacterId();
                ret.UserCharacters.Add(new NetUserCharacterDefaultData
                {
                    CostumeId = 0,
                    Csn = csn,
                    Grade = 0,
                    Lv = 1,
                    Skill1Lv = 1,
                    Skill2Lv = 1,
                    Tid = character.Id,
                    UltiSkillLv = 1
                });
                ret.Character.Add(new NetCharacterData
                {
                    Csn = user.GenerateUniqueCharacterId(),
                    Tid = character.Id,
                });
                user.Characters.Add(new CharacterModel
                {
                    CostumeId = 0,
                    Csn = csn,
                    Grade = 0,
                    Level = 1,
                    Skill1Lvl = 1,
                    Skill2Lvl = 1,
                    Tid = character.Id,
                    UltimateLevel = 1
                });

                // Add "New Character" Badge
                user.AddBadge(BadgeContents.NikkeNew, character.NameCode.ToString());
                user.AddTrigger(Trigger.ObtainCharacter, 1, character.NameCode);
                if (character.OriginalRare == OriginalRareType.SR)
                {
                    user.AddTrigger(Trigger.ObtainCharacterSSR, 1);
                }
                else
                {
                    user.AddTrigger(Trigger.ObtainCharacterNew, 1, 0);
                }

                if (character.OriginalRare == OriginalRareType.SSR || character.OriginalRare == OriginalRareType.SR)
                {
                    user.BondInfo.Add(new() { NameCode = character.NameCode, Lv = 1 });
                }
            }

           
            
        }

        public static int GetValueByRarity(OriginalRareType rarity, int rValue, int srValue, int ssrValue) => rarity switch
        {
            OriginalRareType.R => rValue,
            OriginalRareType.SR => srValue,
            OriginalRareType.SSR => ssrValue,
            _ => throw new Exception($"Unknown character rarity: {rarity}")
        };

        public static NetCharacterData GetNetCharacterData(CharacterModel character, int bodyLabel = 0)
        {
            return new NetCharacterData
            {
                Csn = character.Csn,
                Tid = character.Tid,
                PieceCount = bodyLabel == 0 ? 1 : 0,
                CurrencyValue = bodyLabel
            };
        }
    }
}
