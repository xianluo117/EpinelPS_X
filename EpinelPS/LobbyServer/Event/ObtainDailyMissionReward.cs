using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.LobbyServer.Event.Mission;
using EpinelPS.LobbyServer.Pass;
using EpinelPS.Utils;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace EpinelPS.LobbyServer.Event
{
    [PacketPath("/event/dailymission/obtainreward")]
    public class ObtainDailyEventReward : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
           
            ReqObtainDailyEventReward req = await ReadData<ReqObtainDailyEventReward>();
           
            User user = GetUser();

            Logging.WriteLine($"[ObtainDailyEventReward] 获取的奖励信息：{req.DailyEventId},{req.EventId}", LogType.Warning);

            ResObtainDailyEventReward response = new(); // field Reward

            NetRewardData rewards = new()
            {
                PassPoint = new()
            };

            EventMissionData userEvent = GetUserEventMissionData(user, req.EventId);
            int dateDay = user.GetDateDay();
            Timestamp timeStamp = Timestamp.FromDateTime(DateTime.UtcNow);
            userEvent.LastDay = dateDay;
            userEvent.LastDate = timeStamp.ToDateTime().Ticks;

            foreach (int item in req.DailyEventId)
            {
                Logging.WriteLine($"[ObtainDailyEventReward] 活动事件任务ID：{item}", LogType.Info);

                var record = GameData.Instance.DailyEventTable.Values.FirstOrDefault(x => x.Id == item && x.EventId == req.EventId)
                             ?? throw new Exception($"[ObtainDailyEventReward]未找到记录: Id={item}, EventId={req.EventId}");

                if (record.RewardId != 0)
                {
                    Logging.WriteLine($"[ObtainDailyEventReward] 奖励ID：{record.RewardId}", LogType.Info);
                    // Actual reward
                    RewardRecord rewardRecord = GameData.Instance.GetRewardTableEntry(record.RewardId) ?? throw new Exception("unable to lookup reward");
                    RegisterRewards(user, rewardRecord,ref rewards);
                }
                
                user.AddTrigger(Trigger.DailyEventClear, 1, record.EventPhaseGroupId);
                user.AddTrigger(Trigger.EventPoint, 1, req.EventId);
                //user.EventMissionInfo[req.EventId].MissionIdList.Add(item);

                if (!userEvent.MissionIdList.Contains(item))
                {
                    Logging.WriteLine($"[ObtainDailyEventReward] 添加已完成事件任务ID：{item}", LogType.Info);
                    userEvent.MissionIdList.Add(item);
                }

                if (record.EventPhaseType == EventPhaseType.Final)
                {
                    userEvent.AllClear = true;
                }
            }

            //userEvent.LastDate = Timestamp.FromDateTime(DateTime.UtcNow).ToDateTime().Ticks;

            user.EventMissionInfo[req.EventId] = userEvent;

            response.Reward = rewards;
            
            

            JsonDb.Save();

            await WriteDataAsync(response);
        }

        public static NetRewardData RegisterRewards(User user, RewardRecord rewardData,ref NetRewardData ret)
        {
            
           
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

                    RewardUtils.AddSingleObject(user, ref ret, item.RewardId, item.RewardType, item.RewardValue);
                }
            }

            return ret;
        }


        /// <summary>
        /// Get user event mission data, if not exists, create a new one
        /// </summary>
        /// <param name="user">User</param>
        /// <param name="eventId">EventId</param>
        /// <returns>EventMissionData</returns>
        private static EventMissionData GetUserEventMissionData(User user, int eventId)
        {
            // Get user event mission data, if not exists, create a new one
            if (!user.EventMissionInfo.TryGetValue(eventId, out var userEvent))
            {
                userEvent = new EventMissionData();
                user.EventMissionInfo.Add(eventId, userEvent);
            }
            return userEvent;
        }
    }
}
