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

            ResGetInAppShopBuyProduct  response = new();

            
            
           
            await WriteDataAsync(response);
        }
    }
}
