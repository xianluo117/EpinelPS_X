using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Intercept
{
    [PacketPath("/intercept/getclearinfo")]
    public class GetInterceptClearinfo : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetInterceptClearInfo req = await ReadData<ReqGetInterceptClearInfo>();

            Logging.WriteLine($"通关信息{req}:Intercept- {req.Intercept},FilterType-{req.FilterType},InterceptId-{req.InterceptId},OrderType-{req.OrderType},TabType-{req.TabType}",LogType.Error);

            User user = GetUser();

            ResGetInterceptClearInfo response = new();


            var info = GetClearInfo(user, req);
           

            //response.Histories.Add(info);

            await WriteDataAsync(response);
        }

        private NetInterceptClearInfo GetClearInfo(User user, ReqGetInterceptClearInfo req)
        {
            NetInterceptClearInfo info = new();

            var specialTable = GameData.Instance.InterceptSpecial;
            var specialBosses = specialTable.Values.Where(x => x.Group == 1).OrderBy(x => x.Order).ToList();

            var dayOfYear = DateTime.UtcNow.DayOfYear;
            var specialIndex = dayOfYear % specialBosses.Count;

            var specialId = specialBosses[specialIndex].Id;
            return null;

        }

    }
}
