using EpinelPS.Utils;
using EpinelPS.Data;

namespace EpinelPS.LobbyServer.LobbyUser.Profile
{
    [PacketPath("/lobby/usertitle/get")]
    public class GetUserTitle : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetUserTitleList req = await ReadData<ReqGetUserTitleList>();
            ResGetUserTitleList res = new();
            User user = GetUser();
            // Access GameData and get all UserTitle IDs
            //Dictionary<int, UserTitleRecord> userTitleRecords = GameData.Instance.userTitleRecords;

            if (user.TitleList.Count==0)
            {
                user.TitleList.Add(1);
            }



            foreach (var titleId in user.TitleList)
            {
                res.UserTitleList.Add(new ResGetUserTitleList.Types.NetUserTitle() { UserTitleId = titleId });
            }

            res.CurrentUserTitle = user.TitleId;
            
            // foreach (int titleId in userTitleRecords.Keys)
            // {
            //     r.UserTitleList.Add(new ResGetUserTitleList.Types.NetUserTitle() { UserTitleId = titleId });
            // }

            await WriteDataAsync(res);
        }
    }
}
