using EpinelPS.Database;
using EpinelPS.Models;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Intercept
{
    [PacketPath("/intercept/fastclear")]
    public class FastClearInterceptData : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqFastClearIntercept req = await ReadData<ReqFastClearIntercept>();
            User user = GetUser();

            InterceptionClearResult sRes = InterceptionHelper.Clear(user, req.Intercept, req.InterceptId);
            user.ResetableData.InterceptionTickets--;

            long userDamage;

            if (user.InterceptRecord.TryGetValue(req.InterceptId, out long value))
            {
                userDamage = value;
            }
            else
            {
                userDamage = 0;
            }
            

            ResFastClearIntercept response = new()
            {
                TicketCount = User.ResetableData.InterceptionTickets,
                MaxTicketCount = JsonDb.Instance.MaxInterceptionCount,
                Damage = userDamage,
                NormalReward = sRes.NormalReward,
                BonusReward = sRes.BonusReward
            };

            user.AddTrigger(Data.Trigger.InterceptClear, 1);

            JsonDb.Save();

            await WriteDataAsync(response);
        }
    }
}
