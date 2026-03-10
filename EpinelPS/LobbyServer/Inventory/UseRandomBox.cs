using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Inventory
{
    [PacketPath("/inventory/userandombox")]
    public class UseRandomBox : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqUseRandomBox req = await ReadData<ReqUseRandomBox>();
            User user = GetUser();

            ResUseRandomBox response = new();

            DbItemData box = user.Items.Where(x => x.Isn == req.Isn).FirstOrDefault() ?? throw new InvalidDataException("cannot find box with isn " + req.Isn);
            
            ItemConsumeRecord? cItem = GameData.Instance.ConsumableItems.Where(x => x.Value.Id == box.ItemType).FirstOrDefault().Value ?? throw new Exception("cannot find item Id " + box.ItemType);
            int usefragcost = cItem.UseFragCost;
            Logging.WriteLine($"物品id {cItem.Id}，单次使用消耗 {usefragcost} 个");
            if (req.Count * usefragcost > box.Count) throw new Exception("count mismatch");
            box.Count -= req.Count*usefragcost;
            
            if (box.Count == 0) user.Items.Remove(box);

            response.Reward = NetUtils.UseLootBox(user, box.ItemType, req.Count);

            // update client sIde box count
            response.Reward.UserItems.Add(NetUtils.UserItemDataToNet(box));

            JsonDb.Save();

            await WriteDataAsync(response);
        }
    }
}
