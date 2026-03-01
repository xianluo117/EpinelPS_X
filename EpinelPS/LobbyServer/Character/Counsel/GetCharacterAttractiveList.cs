using EpinelPS.Data;
using EpinelPS.Utils;
using Microsoft.Extensions.Logging;

namespace EpinelPS.LobbyServer.Character.Counsel
{
    [PacketPath("/character/attractive/get")]
    public class GetCharacterAttractiveList : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetAttractiveList req = await ReadData<ReqGetAttractiveList>();
            User user = GetUser();

            
            ResGetAttractiveList response = new();
            // {
            //     CounselAvailableCount = 3 // TODO
            // };


            int infralev = user.InfraCoreLvl;


            //基础核心增加次数
            // 0 - 解锁派遣全部领取
            // 1 - 派遣目录上限提高{ }
            // 2 - 每日咨询增加{ }
            // 3 - 解锁派遣自动配置
            // 4 - 耐力上限提高{ }
            // 5 - 解锁派遣全部派遣
            // 6 - 普通商店免费重置次数增加{ }
            // 7 - 新人竞技场次数增加{ }
            // 
            var infracore = GameData.Instance.InfracoreTable.Values.Where(x => x.Grade == infralev).FirstOrDefault();

            if (user.ResetableData.DailyCounselCount.Count == 0)
            {
                Logging.WriteLine($"[咨询]检测到次数信息为空！！",LogType.Error);
                user.ResetableData.DailyCounselCount[1] = 3 + infracore.FunctionList[2].Function;
            }

            if (User.ShouldResetUser())
            {
                user.ResetableData.DailyCounselCount[1] = 3 + infracore.FunctionList[2].Function;
            }

            user.ResetableData.DailyCounselCount.TryGetValue(1, out int CounselCount);

            Logging.WriteLine($"[咨询]次数：{CounselCount}",LogType.Info);
            // Check if it's a new day, reset daily missions
            

            if (CounselCount<0)
            {
                response.CounselAvailableCount = 3 + infracore.FunctionList[2].Function;
            }
            else
            {
                response.CounselAvailableCount = CounselCount;
            }

            foreach (NetUserAttractiveData item in user.BondInfo)
            {
                response.Attractives.Add(item);
                item.CanCounselToday = true;
                
            }

            // TODO: Validate response from real server and pull info from user info
            await WriteDataAsync(response);
        }
    }
}