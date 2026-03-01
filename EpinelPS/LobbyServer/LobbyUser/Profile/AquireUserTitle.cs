using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Utils;
using Org.BouncyCastle.Ocsp;

namespace EpinelPS.LobbyServer.LobbyUser.Profile
{
    [PacketPath("/lobby/usertitle/acquire")]
    public class AquireUserTitle : LobbyMsgHandler
    {
       

        protected override async Task HandleAsync()
        {
            ReqAcquireUserTitle req = await ReadData<ReqAcquireUserTitle>();
            User user = GetUser();
            ResAcquireUserTitle response = new();

            var ret = new NetRewardData();

            foreach (var acquireIds in req.UserTitleAcquireIds)
            {
                UserTitleAcquireConditionRecord? acquireCondition = GameData.Instance.UserTitleAcquireConditionTable.Values.FirstOrDefault(x => x.Id == acquireIds);

                RewardUtils.AddSingleObject(user, ref ret, acquireCondition.UserTitleId, RewardType.UserTitle, 1);

            }

            response.Reward = ret;

            JsonDb.Save();
            // TODO

            await WriteDataAsync(response);
        }
    }
}
