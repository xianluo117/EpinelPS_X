using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Outpost
{
    [PacketPath("/outpost/buildingisdone")]
    public class SetBuildingDone : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqBuildingIsDone req = await ReadData<ReqBuildingIsDone>();
            User user = GetUser();

            ResBuildingIsDone response = new();
            
            var build = user.OutpostBuildings.Where(x => x.SlotId == req.PositionId).FirstOrDefault() ?? throw new InvalidDataException("未发现建筑 " + req.PositionId);
            build.IsDone = true;
            response.ConditionTriggerTidList.Add(build.BuildingId);
            response.BuildingId = build.BuildingId;
           
            await WriteDataAsync(response);
        }
    }
}
