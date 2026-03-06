using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop
{
    [PacketPath("/productoffer/setseen")]
    public class SeenProductOffer : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqSetSetSeenProductOffer req = await ReadData<ReqSetSetSeenProductOffer>();

            
            // TODO: Figure out a way to disable ads

            ResSetSetSeenProductOffer response = new();

            response.Result.Add(new NetUserProductOfferSeenHistory
            {
                ProductOfferId = req.ProductOfferId
            });


            await WriteDataAsync(response);
        }
    }
}
