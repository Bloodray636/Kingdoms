namespace KingdomsClone.Core.Events
{
    public struct GameTickEvent
    {
        public float DeltaGameSeconds;
    }

    public struct DayChangedEvent
    {
        public int NewDay;
        public int Year;
        public Season Season;
    }

    public struct GameSpeedChangedEvent
    {
        public float OldSpeed;
        public float NewSpeed;
    }

    public struct SeasonChangedEvent
    {
        public Season OldSeason;
        public Season NewSeason;
        public int Year;
    }
}