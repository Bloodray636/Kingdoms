namespace KingdomsClone.Core.Events
{
    /// <summary>
    /// Публикуется при любом изменении карты (постройка, снос, дорога, перегенерация).
    /// PathfindingService очищает кэш путей — все пути строятся заново.
    /// </summary>
    public struct PathCacheClearEvent { }
}