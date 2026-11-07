namespace KingdomsClone.Core
{
    public enum GameState
    {
        Boot,     // инициализация, загрузка данных
        Menu,     // главное меню / пауза с меню
        Playing,  // игра идёт
        Paused    // игра на паузе
    }
}