using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Inventory
{
    [PacketPath("/inventory/usebundlebox")]
    public class UseBundleBox : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqUseBundleBox req = await ReadData<ReqUseBundleBox>();
            User user = GetUser();

            ResUseRandomBox response = new();
            ItemData box = user.Items.Where(x => x.Isn == req.Isn).FirstOrDefault() ?? throw new InvalidDataException("cannot find box with isn " + req.Isn);
            if (req.Count > box.Count) throw new Exception("count mismatch");

            //Console.WriteLine($"[UseBundleBox] 请求参数 - ISN: {req.Isn}, 数量: {req.Count}");
            //Console.WriteLine($"[UseBundleBox] 获取盒子物品ID : {box.ItemType}");

            box.Count -= req.Count;
            if (box.Count == 0) user.Items.Remove(box);

            //Console.WriteLine($"[UseBundleBox] 开始处理BundleBox使用请求");

            response.Reward = NetUtils.UseBundleBox(user, box.ItemType, req.Count);


            // 更新客户端物品数量

            response.Reward.UserItems.Add(NetUtils.UserItemDataToNet(box));


            JsonDb.Save();

            await WriteDataAsync(response);

        }


    }
}