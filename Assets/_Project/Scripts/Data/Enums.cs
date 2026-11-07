namespace KingdomsClone.Data
{
    public enum BuildingCategory
    {
        Housing,     // жильё — дома
        Production,  // производство — лесоруб, ферма, кузница
        Happiness,   // счастье — таверна, церковь, баня
        Defense,     // оборона — стены, башни
        Storage,     // склады
        Special      // цитадель, ратуша — уникальные
    }

    public enum ResourceCategory
    {
        Food,         // еда
        Material,     // дерево, камень
        Precious,     // золото
        Intermediate  // доски, инструменты, железо
    }
}