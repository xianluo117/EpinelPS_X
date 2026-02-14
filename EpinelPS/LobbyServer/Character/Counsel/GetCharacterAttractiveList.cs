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

            if (user.ResetableData.DailyCounselCount.Count == 0)
            {
                Logging.WriteLine($"[咨询]检测到次数信息为空！！",LogType.Error);
                user.ResetableData.DailyCounselCount[1] = 3;
            }

            if (User.ShouldResetUser())
            {
                user.ResetableData.DailyCounselCount[1] = 3;
            }

            user.ResetableData.DailyCounselCount.TryGetValue(1, out int CounselCount);

            Logging.WriteLine($"[咨询]次数：{CounselCount}",LogType.Info);
            // Check if it's a new day, reset daily missions
            

            if (CounselCount<0)
            {
                response.CounselAvailableCount = 3;
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