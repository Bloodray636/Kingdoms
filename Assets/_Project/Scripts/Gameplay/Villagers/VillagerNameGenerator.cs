using UnityEngine;

namespace KingdomsClone.Gameplay
{
    public static class VillagerNameGenerator
    {
        private static readonly string[] MaleNames =
        {
            "Славик", "Слава",
        };

        private static readonly string[] FemaleNames =
        {
            "Анна", "Мария", "Ольга", "Елена", "Наталья", "Татьяна", "Ирина", "Екатерина",
            "Анастасия", "Дарья", "Софья", "Полина", "Вера", "Надежда", "Любовь", "Ксения",
            "Алина", "Кристина", "Юлия", "Светлана", "Маргарита", "Валентина", "Алёна", "Милана",
        };

        private static readonly string[] LastNames =
        {
            "Иванов", "Петров", "Сидоров", "Смирнов", "Кузнецов", "Попов", "Васильев", "Соколов",
            "Михайлов", "Новиков", "Фёдоров", "Морозов", "Волков", "Алексеев", "Лебедев", "Семёнов",
            "Егоров", "Павлов", "Козлов", "Степанов", "Николаев", "Орлов", "Андреев", "Макаров",
        };

        public static string Generate(out bool isMale)
        {
            isMale = Random.value < 0.5f;
            string first = isMale ? MaleNames[Random.Range(0, MaleNames.Length)] : FemaleNames[Random.Range(0, FemaleNames.Length)];
            string last = LastNames[Random.Range(0, LastNames.Length)];

            if (!isMale) {
                last += "а";
            }
            
            return $"{first} {last}";
        }

        public static string Generate() => Generate(out _);
    }
}