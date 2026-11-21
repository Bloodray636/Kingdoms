using KingdomsClone.Gameplay;

namespace KingdomsClone.Core.Events
{
    public struct TaxChangedEvent
    {
        public TaxLevel OldLevel;
        public TaxLevel NewLevel;
    }

    /// <summary>
    /// Публикуется, когда счастье пересчиталось (например, из-за налогов).
    /// UI и жители могут обновить свои данные.
    /// </summary>
    public struct HappinessChangedEvent
    {
        public float OldValue;
        public float NewValue;
    }
}