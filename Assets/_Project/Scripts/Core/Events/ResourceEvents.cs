using KingdomsClone.Data;

namespace KingdomsClone.Core.Events
{
    public struct ResourceChangedEvent
    {
        public ResourceDataSO Resource;
        public int OldAmount;
        public int NewAmount;
        public int Delta => NewAmount - OldAmount;
    }
}