namespace SSW_x_Vonage_Clean_Architecture.Domain.Teams;

public static class MissionErrors
{
    public static readonly Error AlreadyCompleted = Error.Conflict(
        "Mission.AlreadyCompleted",
        "Mission is already completed");
}