using EpinelPS.Utils;
using EpinelPS.Data;

namespace EpinelPS.LobbyServer.Outpost.Infracore
{
    [PacketPath("/infracore/reward")]
    public class ObtainInfracoreReward : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqObtainInfraCoreReward req = await ReadData<ReqObtainInfraCoreReward>();
            ResObtainInfraCoreReward response = new();

            User user = GetUser();

            int currentLevel = user.InfraCoreLvl;

            Logging.WriteLine($"当前等级：{currentLevel}",LogType.Info);

             Dictionary<int, InfraCoreGradeRecord> gradeTable = GameData.Instance.InfracoreTable;
            if (gradeTable.TryGetValue(currentLevel, out var gradeData))
            {
                if (gradeData.RewardId > 0)
                {
                    Logging.WriteLine($"奖励ID：{gradeData.RewardId}", LogType.Info);
                    bool isReceived = user.InfraCoreRewardReceived.ContainsKey(currentLevel) && user.InfraCoreRewardReceived[currentLevel];
                    
                    if (!isReceived)
                    {
                        user.InfraCoreRewardReceived[currentLevel] = true;
                        
                        var reward = RewardUtils.RegisterRewardsForUser(user, gradeData.RewardId);


                        response.Reward = reward;
                    }
                }
            }

            await WriteDataAsync(response);
        }
    }
}