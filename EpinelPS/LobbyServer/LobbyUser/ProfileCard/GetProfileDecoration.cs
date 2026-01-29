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
            User user = GetUser();

            ResProfileCardDecorationLayout r = new();

            if (user.DecorationLayout.BackgroundId != 0)
            {
                r = new()
                {
                    Layout = user.DecorationLayout
                };
            }
            else
            {
                r = new()
                {
                    Layout = new ProfileCardDecorationLayout
                    {
                        BackgroundId = 101002,
                        ShowCharacterSpine = true
                    }
                };
            }

            await WriteDataAsync(r);
        }
    }
}
