namespace KingdomsClone.Core.Pooling
{
    /// <summary>
    /// Компоненты на пулимых объектах реализуют этот интерфейс,
    /// чтобы сбрасывать/чистить состояние при взятии/возврате в пул.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Вызывается при взятии из пула. Сбрось состояние.</summary>
        void OnGetFromPool();

        /// <summary>Вызывается при возврате в пул. Почисти ресурсы.</summary>
        void OnReturnToPool();
    }
}