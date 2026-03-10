using EpinelPS.Data;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop.InApp
{
    [PacketPath("/inappshop/jupiter/buyproduct")]
    public class BuyJupiterProduct : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqBuyJupiterProduct x = await ReadData<ReqBuyJupiterProduct>();

            Logging.WriteLine($"[info]{x},id- {x.ProductId},{x.Currency},{x.ExtraData},{x.Language},{x.Price}",LogType.Error);

            ResBuyJupiterProduct response = new();
          
            await WriteDataAsync(response);
        }
    }
}
