using EpinelPS.Data;
using EpinelPS.Utils;
using System.Globalization;

namespace EpinelPS.LobbyServer.Shop.InApp
{
    [PacketPath("/inappshop/getbuyproduct")]
    public class GetBuyProduct : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetInAppShopBuyProduct  req = await ReadData<ReqGetInAppShopBuyProduct>();

            Logging.WriteLine($"GetBuyProduct: ProductId - {req.ProductId},Token -{req.Token}", LogType.Warning);

            ResGetInAppShopBuyProduct  response = new();
            NetRewardData ret = new();






            response.Reward =ret;
            
           
            await WriteDataAsync(response);
        }
    }
}
