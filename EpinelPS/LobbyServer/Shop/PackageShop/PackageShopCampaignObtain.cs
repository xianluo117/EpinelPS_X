using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop.PackageShop
{
    [PacketPath("/packageshop/campaign/obtain")]
    public class PackageShopCampaignObtain : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqObtainCampaignPackage req = await ReadData<ReqObtainCampaignPackage>();
            
            ResObtainCampaignPackage response = new();

            // TODO
            await WriteDataAsync(response);
        }
    }
}