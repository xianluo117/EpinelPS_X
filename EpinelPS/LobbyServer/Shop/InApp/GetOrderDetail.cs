using EpinelPS.Database;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Shop.InApp
{
    [PacketPath("/inappshop/jupiter/getorderdetail")]
    public class GetJupiterOrderDetail : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetJupiterOrderDetail req = await ReadData<ReqGetJupiterOrderDetail>();

            Logging.WriteLine($"GetJupiterOrderDetail: ReferenceId={req.ReferenceId}", LogType.Warning);

            ResGetJupiterOrderDetail response = new();

            if (JsonDb.Instance.SimulatedPurchaseOrders.ContainsKey(req.ReferenceId ?? string.Empty))
            {
                response.Status = "success";
            }
            else
            {
                response.Status = "unknown";
            }

            Logging.WriteLine($"[JupiterSandbox] OrderDetail status={response.Status} ref={req.ReferenceId}", LogType.Warning);

            await WriteDataAsync(response);
        }
    }
}
