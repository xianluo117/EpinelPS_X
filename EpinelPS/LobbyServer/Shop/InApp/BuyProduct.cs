using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Models;
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

            int packageListTableId = x.ExtraData?.PackageListTableId ?? 0;
            string referenceId = Guid.NewGuid().ToString("N");

            JsonDb.Instance.SimulatedPurchaseOrders[referenceId] = new SimulatedPurchaseOrder
            {
                ReferenceId = referenceId,
                ProductId = x.ProductId ?? string.Empty,
                PackageListTableId = packageListTableId,
                UserId = UserId,
                IsConsumed = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            response.ReferenceId = referenceId;
            string baseUrl = GameConfig.Root.PaymentRedirectBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                string host = ctx?.Request.Host.Host ?? "kr-lobby.nikke-kr.com";
                baseUrl = $"http://{host}:8080";
            }

            baseUrl = baseUrl.TrimEnd('/');
            response.RedirectUrl = $"{baseUrl}/payment/jupiter/success?referenceId={referenceId}";

            Logging.WriteLine($"[JupiterSandbox] Created order ref={referenceId} product={x.ProductId} packageList={packageListTableId} user={UserId}", LogType.Error);
            Logging.WriteLine($"[JupiterSandbox] Response ref={response.ReferenceId} redirect={response.RedirectUrl}", LogType.Error);

            await WriteDataAsync(response);
        }
    }
}
