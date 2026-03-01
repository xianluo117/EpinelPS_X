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

            response.CostumeIds.AddRange(user.CostumeList);
            // return all
            // response.CostumeIds.AddRange(GameData.Instance.GetAllCostumes());

            await WriteDataAsync(response);
        }
    }
}
