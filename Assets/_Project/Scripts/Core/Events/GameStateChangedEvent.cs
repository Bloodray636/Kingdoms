namespace KingdomsClone.Core.Events
{
    public struct GameStateChangedEvent
    {
        public GameState PreviousState;
        public GameState NewState;
    }
}