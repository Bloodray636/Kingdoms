namespace KingdomsClone.Gameplay
{
    /// <summary>
    /// Приоритет вакансии. Чем выше — тем раньше JobManager назначит жителя.
    /// </summary>
    public enum JobPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
}