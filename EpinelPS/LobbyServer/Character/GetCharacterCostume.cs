using EpinelPS.Data;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Character
{
    [PacketPath("/character/costume/get")]
    public class GetCharacterCostume : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetCharacterCostumeData req = await ReadData<ReqGetCharacterCostumeData>();
            User user = GetUser();
            ResGetCharacterCostumeData response = new();

            // 全量解锁：直接返回全表，避免数据库/缓存/清单不同步导致失效
            response.CostumeIds.AddRange(GameData.Instance.GetAllCostumes());

            await WriteDataAsync(response);
        }
    }
}
