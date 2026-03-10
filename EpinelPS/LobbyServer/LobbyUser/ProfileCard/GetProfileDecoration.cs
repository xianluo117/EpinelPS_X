using EpinelPS.Database;
using EpinelPS.Utils;
using log4net.Layout;

namespace EpinelPS.LobbyServer.LobbyUser.ProfileCard
{
    [PacketPath("/ProfileCard/DecorationLayout/Get")]
    public class GetProfileDecoration : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqProfileCardDecorationLayout req = await ReadData<ReqProfileCardDecorationLayout>();
            User? user = GetUser((ulong)req.TargetUsn);
            Logging.WriteLine($"[GetProfileDecoration] {req.TargetUsn}");
            ResProfileCardDecorationLayout res = new();

            Logging.WriteLine($"[GetProfileDecoration] BackgroundId: {user.DecorationLayout.BackgroundId},ShowSpine: {user.DecorationLayout.ShowCharacterSpine}");

            if (user.DecorationLayout.BackgroundId != 0)
            {
                res = new()
                {
                    Layout = user.DecorationLayout
                };
            }
            else
            {
                res = new()
                {
                    Layout = new ProfileCardDecorationLayout
                    {
                        BackgroundId = 101001,
                        ShowCharacterSpine = true
                    }
                };
                user.DecorationLayout.BackgroundId = 101001;
                user.DecorationLayout.ShowCharacterSpine = true;
            }

            JsonDb.Save();
            await WriteDataAsync(res);
        }
    }
}
