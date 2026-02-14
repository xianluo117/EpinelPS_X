using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Utils;
using Google.Protobuf.Collections;
using Org.BouncyCastle.Ocsp;

namespace EpinelPS.LobbyServer.Inventory
{
    [PacketPath("/ProfileCard/ProfileRandomBox/Open")]
    public class UseProfileRandomBox : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqOpenProfileRandomBox req = await ReadData<ReqOpenProfileRandomBox>();
            User user = GetUser();

            ResOpenProfileRandomBox response = new();

            ProfileRandomBoxSingleOpeningResult res = new();

            ItemData box = user.Items.Where(x => x.Isn == req.Isn).FirstOrDefault() ?? throw new InvalidDataException("cannot find box with isn " + req.Isn);



            ItemConsumeRecord? cItem = GameData.Instance.ConsumableItems.Where(x => x.Value.Id == box.ItemType).FirstOrDefault().Value ?? throw new Exception("cannot find item Id " + box.ItemType);

            int usefragcost = cItem.UseFragCost;

            if (req.NumOpens * usefragcost > box.Count) throw new Exception("count mismatch");

            box.Count -= req.NumOpens * usefragcost;
            if (box.Count == 0) user.Items.Remove(box);
            
            var  ss = response.OpeningResult;
            

            for (int i = 0; i < req.NumOpens; i++)
            {
                ProfileCardObjectRecord tempCardObjectRecord =new();

                tempCardObjectRecord = NetUtils.UseProfileBox(user, box.ItemType);

                res.ObjectTid = tempCardObjectRecord.Id;

                if (tempCardObjectRecord.ObjectType == ObjectType.BackGround)
                {
                    user.BackgroundList.Add(tempCardObjectRecord.Id);

                }
                else if (tempCardObjectRecord.ObjectType == ObjectType.Sticker)
                {
                    user.StickerList.Add(tempCardObjectRecord.Id);
                }

                Console.WriteLine($"[UseProfileRandomBox] 使用装饰抽奖获得: ID： {tempCardObjectRecord.Id}，类型 {tempCardObjectRecord.ObjectType}");

                response.OpeningResult.Add(res);
                
            }

            response.ProfileCardTicketMaterialSync.Add(NetUtils.UserItemDataToNet(box));

            Console.WriteLine($"[UseProfileRandomBox] 奖励: {ss}");

            JsonDb.Save();

            await WriteDataAsync(response);
        }
    }
}
