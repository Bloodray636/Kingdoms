namespace KingdomsClone.Core.Events
{
    /// <summary>Тик 4 раза в секунду игрового времени. DeltaGameSeconds — всегда 0.25.</summary>
    public struct GameTickEvent
    {
        public float DeltaGameSeconds;
    }

    /// <summary>Смена игрового дня. Публикуется раз в 60 игровых секунд.</summary>
    public struct DayChangedEvent
    {
        public int NewDay;
        public int Year;
        public Season Season;
    }

    /// <summary>Игрок сменил скорость 1x/2x/3x. Пауза сюда НЕ входит.</summary>
    public struct GameSpeedChangedEvent
    {
        public float OldSpeed;
        public float NewSpeed;
    }
}