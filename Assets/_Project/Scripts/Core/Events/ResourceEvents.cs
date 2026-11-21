using KingdomsClone.Data;

namespace KingdomsClone.Core.Events
{
    /// <summary>
    /// Публикуется через EventBus при изменении ресурса.
    /// Старое значение нужно для анимаций ("+5 дерева") и для разницы.
    /// </summary>
    public struct ResourceChangedEvent
    {
        public ResourceDataSO Resource;
        public int OldAmount;
        public int NewAmount;

        public int Delta => NewAmount - OldAmount;
    }
}