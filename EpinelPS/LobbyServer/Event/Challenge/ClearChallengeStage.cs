using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Utils;
using log4net;


namespace EpinelPS.LobbyServer.Event.Challenge
{
    [PacketPath("/event/challengestage/clear")]
    public class ClearChallengeStage : LobbyMsgHandler
    {
        
        protected override async Task HandleAsync()
        {
            ReqClearChallengeEventStage req = await ReadData<ReqClearChallengeEventStage>();
            User user = GetUser();

            ResClearChallengeEventStage response = new();
            
            int difficultId = 0;
            NetRewardData reward = new();
            EventData? eventData = new EventData();
            Console.WriteLine($"[ClearChallengeStage] 请求参数 - StageId: {req.StageId}, EventId: {req.EventId},BattleResult: {req.BattleResult}");

            if (user.EventInfo.TryGetValue(req.EventId, out eventData) && req.BattleResult == 1)
            {
                Console.WriteLine($"[ClearChallengeStage] 战役完成 - eventData: {eventData}, BattleResult: {req.BattleResult}");
                if (eventData.ClearedStages.Contains(req.StageId))
                {
                    ClearStage(user, req.StageId, ref reward, req.BattleResult, 1); // always clearCount = 1 for normal clear
                    response.Reward = reward;
                }
                else
                {
                    Console.WriteLine($"[ClearChallengeStage] 初次完成");
                    ClearStage(user, req.StageId, ref reward, req.BattleResult, 0); // always clearCount = 1 for normal clear
                    eventData.ClearedStages.Add(req.StageId);
                    response.FirstClearReward = reward;
                }

                eventData.LastStage = req.StageId;
                eventData.Diff = difficultId;
                user.AddTrigger(Trigger.EventStageClear, 1, req.StageId);
                user.AddTrigger(Trigger.EventDungeonStageClear, 1, req.EventId);

            }
            else
            {
               // user.EventInfo.Add(req.EventId, new EventData() { LastStage = req.StageId, ClearedStages = [req.StageId] });
            }
           

            //response.RemainTicket = 4;

            JsonDb.Save();
            await WriteDataAsync(response);
        }


        /// <summary>
        /// Clear event stage and get rewards  
        /// </summary>
        /// <param name="user">The user clearing the stage</param>
        /// <param name="stageId">The ID of the stage being cleared</param>
        /// <param name="reward">The reward data for the stage</param>
        /// <param name="bonusReward">The bonus reward data for the stage</param>
        /// <param name="battleResult">The result of the battle</param>
        /// <param name="clearCount">The number of times the stage has been cleared</param>
        public static void ClearStage(User user, int stageId, ref NetRewardData reward, int battleResult = 0, int clearCount = 0)
        {
            if (battleResult != 1) return;
            GetReward(user, stageId, ref reward, clearCount);
        }

        /// <summary>
        /// Get normal reward for clearing event stage
        /// </summary>
        /// <param name="user">The user clearing the stage</param>
        /// <param name="stageId">The ID of the stage being cleared</param>
        /// <param name="reward">The reward data for the stage</param>
        /// <param name="battleResult">The result of the battle</param>
        /// <param name="clearCount">The number of times the stage has been cleared</param>
        public static void GetReward(User user, int stageId, ref NetRewardData reward, int clearCount)
        {
            int rewardId = 0;
            if (clearCount<1)
            {
                rewardId = GetFirstRewardId(stageId);
                Console.WriteLine($"[ClearChallengeStage] FirstRewardId {rewardId}");
                clearCount = 1;
            }
            else
            {
                rewardId = GetRewardId(stageId);
                Console.WriteLine($"[ClearChallengeStage] RewardId {rewardId}");
            }
              
            if (rewardId == 0) return;
            RecievedReward(user, ref reward, rewardId, clearCount);
        }

        private static void RecievedReward(User user, ref NetRewardData reward, int rewardId, int clearCount)
        {
            RewardRecord? rewardData = GameData.Instance.GetRewardTableEntry(rewardId);
            if (rewardData == null)
            {
                Logging.WriteLine($"unknown reward Id {rewardId}", LogType.Error);
                return;
            }
            foreach (var item in rewardData.Rewards)
            {
                if (item == null) continue;
                if (item.RewardType == RewardType.None) continue;
                Console.WriteLine($"[ClearChallengeStage] 奖励信息 id {item.RewardId}，类型 {item.RewardType} ，数量{item.RewardValue * clearCount}");
                RewardUtils.AddSingleObject(user, ref reward, item.RewardId, item.RewardType, item.RewardValue * clearCount);
            }
        }

        /// <summary>
        /// Get reward Id from EventDungeonSpotBattleTable
        /// </summary>
        /// <param name="stageId">The ID of the stage being cleared</param>
        /// <returns>The reward ID for the stage</returns>
        private static int GetRewardId(int stageId)
        {
            if (GameData.Instance.EventDungeonSpotBattleTable.TryGetValue(stageId, out EventDungeonSpotBattleRecord? stageRecord))
            {
                return stageRecord.ClearRewardId;
            }
            return 0;
        }

        /// <summary>
        /// Get reward Id from EventDungeonSpotBattleTable
        /// </summary>
        /// <param name="stageId">The ID of the stage being cleared</param>
        /// <returns>The reward ID for the stage</returns>
        private static int GetFirstRewardId(int stageId)
        {
            if (GameData.Instance.EventDungeonSpotBattleTable.TryGetValue(stageId, out EventDungeonSpotBattleRecord? stageRecord))
            {
                return stageRecord.FirstClearRewardId;
            }
            return 0;
        }


        /*
        /// <summary>
        /// Get bonus reward Id from EventDungeonTable via EventDungeonStageTable and EventDungeonDifficultTable
        /// </summary>    
        /// <param name="stageId">The ID of the stage being cleared</param>
        /// <returns>The bonus reward ID for the stage</returns>
        private static int GetBonusRewardId(int stageId)
        {
            if (!GameData.Instance.EventDungeonStageTable.TryGetValue(stageId, out EventDungeonStageRecord? eventStage))
            {
                log.Error($"EventDungeonStageTable not found for StageId: {stageId}");
                return 0;
            }
            EventDungeonDifficultRecord? difficult = GameData.Instance.EventDungeonDifficultTable.Values.FirstOrDefault(x => x.StageGroup == eventStage.Group);
            if (difficult == null)
            {
                log.Error($"EventDungeonDifficultTable not found for Group: {eventStage.Group}");
                return 0;
            }
            EventDungeonRecord? dungeon = GameData.Instance.EventDungeonTable.Values.FirstOrDefault(x => x.DifficultGroup == difficult.Group);
            if (dungeon == null)
            {
                log.Error($"EventDungeonTable not found for DifficultGroup: {difficult.Group}");
                return 0;
            }
            return dungeon.BonusRewardId;
        }*/
    }
}