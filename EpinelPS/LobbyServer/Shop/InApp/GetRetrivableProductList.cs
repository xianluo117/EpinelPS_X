using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Utils;
using System.Linq;

namespace EpinelPS.LobbyServer.Shop.InApp
{
    [PacketPath("/inappshop/getreceivableproductlist")]
    public class GetRetrivableProductList : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetInAppShopReceivableProductList x = await ReadData<ReqGetInAppShopReceivableProductList>();

            ResGetInAppShopReceivableProductList response = new();

            Logging.WriteLine($"GetRetrivableProductList: {x}", LogType.Warning);

            DateTime now = DateTime.UtcNow;

            var pendingOrders = JsonDb.Instance.SimulatedPurchaseOrders.Values
                .Where(order => order.UserId == UserId && !order.IsConsumed)
                .ToList();

            foreach (var order in pendingOrders)
            {
                int subTid = 0;
                if (GameData.Instance.PackageListTable.TryGetValue(order.PackageListTableId, out PackageListRecord? packageList))
                {
                    int packageShopId = packageList.PackageShopId;
                    var shop = GameData.Instance.InAppShopManagerRecords.Values
                        .FirstOrDefault(csp => csp.PackageShopId == packageShopId && csp.StartDate <= now && now <= csp.EndDate);
                    if (shop != null)
                    {
                        subTid = shop.SubCategoryId;
                    }
                }

                response.DataList.Add(new NetInAppShopReceivableProductData()
                {
                    ProductId = order.ProductId ?? string.Empty,
                    Token = order.ReferenceId,
                    SubTid = subTid
                });
            }

            Logging.WriteLine($"GetRetrivableProductList: user={UserId} pending={pendingOrders.Count} returned={response.DataList.Count}", LogType.Warning);

            await WriteDataAsync(response);
        }
    }
}
