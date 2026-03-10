using EpinelPS.Models;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Tower
{
    [PacketPath("/tower/fastcleartower")]
    public class FastClearTower : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqFastClearTower req = await ReadData<ReqFastClearTower>();

            ResFastClearTower response = new();
            
            User user = GetUser();

            response.Reward = ClearTower.CompleteTower(user, req.TowerId).Reward;

            await WriteDataAsync(response);
        }
    }
}
