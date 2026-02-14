using EpinelPS.Data;
using EpinelPS.Utils;

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

            DateTime now = DateTime.Now;

            GameData.Instance.InAppShopManagerRecords.Values
                //.Where(csp => csp.StartDate <= now && now <= csp.EndDate )
                .Where(csp => csp.IsHideIfNotValid==true)
                .OrderBy(csp => csp.OrderGroupId)
                .ThenBy(csp => csp.Id)
                .ToList()
                .ForEach(csp =>
                {
                    response.DataList.Add(new NetInAppShopReceivableProductData()
                    {
                        ProductId = csp.Id.ToString(),
                        SubTid = csp.SubCategoryId
                        
                    });

                });
           

            // TODO

            await WriteDataAsync(response);
        }
    }
}
