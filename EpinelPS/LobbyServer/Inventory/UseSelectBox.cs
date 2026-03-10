using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Inventory
{
    [PacketPath("/inventory/useselectbox")]
    public class UseSelectBox : LobbyMsgHandler
    {
        //调用的是ItemSelectOptionTable.json 这个文件
        protected override async Task HandleAsync()
        {
            ReqUseSelectBox req = await ReadData<ReqUseSelectBox>();
            User user = GetUser();

            ResUseSelectBox response = new();

            DbItemData box = user.Items.Where(x => x.Isn == req.Isn).FirstOrDefault() ?? throw new InvalidDataException("cannot find box with isn " + req.Isn);

            var boxinfo = GameData.Instance.ConsumableItems.Where(x => x.Value.Id == box.ItemType).FirstOrDefault().Value;
            Logging.WriteLine($"单次使用消耗 {boxinfo.UseFragCost} 个");
            int totalCount = req.Select.Sum(opt => opt.Count)*boxinfo.UseFragCost;

            if (totalCount > box.Count) throw new Exception("count mismatch");

            
            //Console.WriteLine($"[UseSelectBox] 请求参数 - ISN: {req.Isn}, 盒子id {box.ItemType}, 数量: {totalCount}");

            box.Count -= totalCount;
            if (box.Count == 0) user.Items.Remove(box);



            response.Reward = NetUtils.UseSelectBox(user, box.ItemType, req);

            // update client sIde box count
            response.Reward.UserItems.Add(NetUtils.UserItemDataToNet(box));

            JsonDb.Save();

            await WriteDataAsync(response);
        }
    }
}