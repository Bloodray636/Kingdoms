namespace KingdomsClone.Core.Events
{
    public struct RoadPlacedEvent
    {
        public int X;
        public int Y;
    }

    public struct RoadRemovedEvent
    {
        public int X;
        public int Y;
    }
}