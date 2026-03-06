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
            List<int> newframeInts = new List<int> { 1, 7, 8, 9, 10, 11, 12, 13, 14, 15,25, 30801 };
            List<int> newiconInts = new List<int>{ 30100,39900,30200,30500 };

            if (user.FrameList.Count < 10)
            {
                user.FrameList.AddRange(newframeInts);
                user.FrameList = user.FrameList.Distinct().ToList();
            }

            if (user.IconList.Count < 4)
            {
                user.IconList.AddRange(newiconInts);
                user.IconList = user.IconList.Distinct().ToList();
            }

            if (user.ProfileFrame == -1)
            {
                user.ProfileFrame = 1;
            }

            response.Frames.AddRange(user.FrameList);

            //直接获取全部边框
            // foreach (var frameRecord in GameData.Instance.userFrameTable.Values)
            // {
            //     user.FrameList.Add(frameRecord.Id);
            //     response.Frames.Add(frameRecord.Id);
            // }
            JsonDb.Save();
            await WriteDataAsync(response);
        }
    }
}
