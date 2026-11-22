using KingdomsClone.Gameplay;

namespace KingdomsClone.Core.Events
{
    public struct TaxChangedEvent
    {
        public TaxLevel OldLevel;
        public TaxLevel NewLevel;
    }

    public struct HappinessChangedEvent
    {
        public float OldValue;
        public float NewValue;
    }
}