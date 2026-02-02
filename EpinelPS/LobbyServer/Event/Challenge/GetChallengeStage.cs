using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.LobbyServer.Event.StoryEvent;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Event.Challenge
{
    [PacketPath("/event/challengestage/get")]
    public class GetChallengeStage : LobbyMsgHandler
    {
        protected override async Task HandleAsync()
        {
            ReqChallengeEventStageData req = await ReadData<ReqChallengeEventStageData>();
            User user = GetUser();

            ResChallengeEventStageData response = new()
            {
                RemainTicket = EventStoryHelper.GetTicket(user, req.EventId),
                TeamData = new NetUserTeamData
                {
                    Type = (int)TeamType.ChallengeModeEvent
                },
            };
            // check if user has a team for this type
            if (user.UserTeams.TryGetValue((int)TeamType.ChallengeModeEvent, out NetUserTeamData? teamData))
            {
                response.TeamData = teamData;
            }

            if (!user.EventInfo.TryGetValue(req.EventId, out EventData? eventData))
            {
                eventData = new() { LastStage = 0 };
                user.EventInfo.Add(req.EventId, eventData);
            }
            
            // placeholder response data for last cleared stage
            response.LastClearedEventStageList.Add(new NetLastClearedEventStageData()
            {
                DifficultyId = eventData.Diff,
                StageId = eventData.LastStage
            });

            JsonDb.Save();
            await WriteDataAsync(response);
        }
    }
}
