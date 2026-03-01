using EpinelPS.Database;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Jukebox
{
    [PacketPath("/jukebox/set/tableid")]
    public class SetTableId : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            //设置指挥中心背景音乐
            ReqSetJukeboxBgmTableId req = await ReadData<ReqSetJukeboxBgmTableId>();
            User user = GetUser();

            ResSetJukeboxBgmTableId response = new();

            Logging.WriteLine($"[Jukebox]获得歌曲 id {req.JukeboxTableId} ,位置{req.Location}",LogType.Info);

            if (req.Location == NetJukeboxLocation.CommanderRoom)
            {
                user.CommanderMusic.TableId = req.JukeboxTableId;
                user.CommanderMusic.Type = NetJukeboxBgmType.JukeboxTableId;
            }
            else if (req.Location == NetJukeboxLocation.Lobby)
            {
                user.LobbyMusic.TableId = req.JukeboxTableId;
                user.LobbyMusic.Type = NetJukeboxBgmType.JukeboxTableId;
            }
            JsonDb.Save();

            await WriteDataAsync(response);
        }
    }
}
