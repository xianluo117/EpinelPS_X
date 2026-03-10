using EpinelPS.Data;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop.PackageShop
{
    [PacketPath("/packageshop/getpopuppackagestate")]
    public class GetPackagePopupState : LobbyMsgHandler
    {
        //指挥官等级礼包
        protected override async Task HandleAsync()
        {
            ReqGetPopupPackageState req = await ReadData<ReqGetPopupPackageState>();
            
            ResGetPopupPackageState response = new();

            // disable ads
            foreach (KeyValuePair<int, PopupPackageListRecord> item in GameData.Instance.PopupPackages)
                response.AppearedList.Add(item.Key);

            await WriteDataAsync(response);
        }
    }
}
