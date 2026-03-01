using EpinelPS.Utils;
using EpinelPS.Data;
using EpinelPS.Database;

namespace EpinelPS.LobbyServer.LobbyUser.Profile
{
    [PacketPath("/User/GetProfileFrame")]
    public class GetProfileFrame : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetProfileFrame req = await ReadData<ReqGetProfileFrame>();
            ResGetProfileFrame response = new();
            User user = GetUser();
            if (user.FrameList.Count == 0)
            {
                user.FrameList.Add(1);
            }

            if (user.IconList.Count == 0)
            {
                user.IconList.Add(30100);
            }

            if (user.ProfileFrame == -1)
            {
                user.ProfileFrame = 1;
            }

            response.Frames.AddRange(user.FrameList);

            //直接获取全部边框
            foreach (var frameRecord in GameData.Instance.userFrameTable.Values)
            {
                user.FrameList.Add(frameRecord.Id);
                response.Frames.Add(frameRecord.Id);
            }
            JsonDb.Save();
            await WriteDataAsync(response);
        }
    }
}
