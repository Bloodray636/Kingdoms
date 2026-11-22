namespace KingdomsClone.Core.Pooling
{
    public interface IPoolable
    {
        void OnGetFromPool();
        void OnReturnToPool();
    }
}