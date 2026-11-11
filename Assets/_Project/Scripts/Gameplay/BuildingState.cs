namespace KingdomsClone.Gameplay
{
    public enum BuildingState
    {
        Placing,   // Призрак (еще не построено)
        Building,  // В процессе строительства (если добавим время постройки)
        Active,    // Здание работает
        Destroyed  // Здание разрушено
    }
}