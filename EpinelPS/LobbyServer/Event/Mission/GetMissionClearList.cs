using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Event.Mission
{
    [PacketPath("/event/mission/getclearlist")]
    public class GetMissionClearList : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            // { "eventIdList": [ 60090, 60092, 20001, 20002 ] }
            //获取事件活动已完成任务情况
            ReqGetEventMissionClearList req = await ReadData<ReqGetEventMissionClearList>();
            User user = GetUser();
            ResGetEventMissionClearList response = new();
            Logging.WriteLine($"【debug】事件id {req.EventIdList}", LogType.Info);
            //var clearedList = EventMissionHelper.GetClearedList(user, req.EventIdList);

            EventMissionHelper.GetClearedList_N(user, req.EventIdList,ref response);

            //try
            //{
            //    response.ResGetEventMissionClearMap.AddRange(clearedList);
            //}
            //catch (Exception ex)
            //{
            //    Logging.Warn($"GetMissionClearList failed: {ex.Message}");
            //}

            Logging.WriteLine($"已完成任务数量 {response.ResGetEventMissionClearMap.Count}，{response.ResGetEventMissionClearMap}",LogType.Info);

            await WriteDataAsync(response);
        }
    }
}
