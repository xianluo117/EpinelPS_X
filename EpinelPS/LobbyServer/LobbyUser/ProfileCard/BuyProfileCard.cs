using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.LobbyUser.ProfileCard
{
    [PacketPath("/ProfileCard/Buy")]
    public class BuyProfileCard : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqBuyProfileCardObject req = await ReadData<ReqBuyProfileCardObject>();
            ResBuyProfileCardObject response = new();
            User user = GetUser();

            Console.WriteLine($"[BuyProfileCard] 请求参数 - ObjectTid: {req.ObjectTid}");

            ProfileCardObjectRecord? CardObject = GameData.Instance.ProfileCardObjectTable.Where(x => x.Value.Id == req.ObjectTid)
                .FirstOrDefault().Value ?? throw new Exception("cannot find Card Object Id " + req.ObjectTid);

            Console.WriteLine($"[BuyProfileCard] 获取装饰 - 类型: {CardObject.ObjectType} , 需要物品id {CardObject.RequireItemId} -数量 {CardObject.RequireItemValue}" );

            DbItemData box = user.Items.Where(x => x.ItemType == CardObject.RequireItemId).FirstOrDefault() ?? throw new InvalidDataException("cannot find item " + CardObject.RequireItemId);
            if (CardObject.RequireItemValue > box.Count) throw new Exception("count mismatch");

            //Console.WriteLine($"[UseBundleBox] 请求参数 - ISN: {req.Isn}, 数量: {req.Count}");
            //Console.WriteLine($"[UseBundleBox] 获取盒子物品ID : {box.ItemType}");

            box.Count -= CardObject.RequireItemValue;
            if (box.Count == 0) user.Items.Remove(box);

            if (CardObject.ObjectType == ObjectType.BackGround)
            {
                user.BackgroundList.Add(CardObject.Id);

            }
            else if (CardObject.ObjectType == ObjectType.Sticker)
            {
                user.StickerList.Add(CardObject.Id);
            }

            response.ProfileCardTicketMaterialSync = NetUtils.UserItemDataToNet(box);

            JsonDb.Save();

            await WriteDataAsync(response);
        }
    }
}
