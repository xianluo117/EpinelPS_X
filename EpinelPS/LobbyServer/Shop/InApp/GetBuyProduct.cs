using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Models;
using EpinelPS.Utils;
using System.Globalization;

namespace EpinelPS.LobbyServer.Shop.InApp
{
    [PacketPath("/inappshop/getbuyproduct")]
    public class GetBuyProduct : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqGetInAppShopBuyProduct req = await ReadData<ReqGetInAppShopBuyProduct>();

            Logging.WriteLine($"GetBuyProduct: ProductId - {req.ProductId},Token -{req.Token}", LogType.Warning);

            ResGetInAppShopBuyProduct response = new();
            NetRewardData ret = new() { PassPoint = new() };

            if (!JsonDb.Instance.SimulatedPurchaseOrders.TryGetValue(req.Token ?? string.Empty, out SimulatedPurchaseOrder? order))
            {
                Logging.WriteLine($"[JupiterSandbox] Unknown order token={req.Token}", LogType.Warning);
                response.Reward = ret;
                await WriteDataAsync(response);
                return;
            }

            if (order.IsConsumed)
            {
                Logging.WriteLine($"[JupiterSandbox] Order already consumed token={req.Token}", LogType.Warning);
                response.Reward = ret;
                await WriteDataAsync(response);
                return;
            }

            if (!string.Equals(order.ProductId, req.ProductId ?? string.Empty, StringComparison.Ordinal))
            {
                Logging.WriteLine($"[JupiterSandbox] ProductId mismatch token={req.Token} req={req.ProductId} order={order.ProductId}", LogType.Warning);
                response.Reward = ret;
                await WriteDataAsync(response);
                return;
            }

            if (!GameData.Instance.PackageListTable.TryGetValue(order.PackageListTableId, out PackageListRecord? packageList))
            {
                Logging.WriteLine($"[JupiterSandbox] PackageList missing id={order.PackageListTableId}", LogType.Error);
                response.Reward = ret;
                await WriteDataAsync(response);
                return;
            }

            if (!GameData.Instance.PackageShopTable.TryGetValue(packageList.PackageShopId, out PackageShopRecord? packageShop))
            {
                Logging.WriteLine($"[JupiterSandbox] PackageShop missing id={packageList.PackageShopId}", LogType.Error);
                response.Reward = ret;
                await WriteDataAsync(response);
                return;
            }

            int packageGroupId = packageShop.PackageGroupId;
            var rewards = GameData.Instance.PackageGroupTable.Values
                .Where(x => x.PackageGroupId == packageGroupId)
                .OrderBy(x => x.Id)
                .ToList();

            if (rewards.Count == 0)
            {
                Logging.WriteLine($"[JupiterSandbox] PackageGroup empty groupId={packageGroupId}", LogType.Error);
                response.Reward = ret;
                await WriteDataAsync(response);
                return;
            }

            User user = GetUser();
            foreach (var reward in rewards)
            {
                RewardUtils.AddSingleObject(user, ref ret, reward.ProductId, reward.ProductType, reward.ProductValue);
            }

            order.IsConsumed = true;
            JsonDb.Save();

            response.Reward = ret;
            Logging.WriteLine($"[JupiterSandbox] Granted rewards token={req.Token} groupId={packageGroupId} items={rewards.Count}", LogType.Info);

            await WriteDataAsync(response);
        }
    }
}
