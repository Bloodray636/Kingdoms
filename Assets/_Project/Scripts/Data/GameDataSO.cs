using System.Collections.Generic;
using UnityEngine;
using UResources = UnityEngine.Resources;

namespace KingdomsClone.Data
{
    [CreateAssetMenu(
        menuName = "KingdomsClone/Game Data",
        fileName = "GameData",
        order = 0)]
    public class GameDataSO : ScriptableObject
    {
        // Путь внутри папки Resources (см. ниже куда класть ассет)
        private const string ResourcePath = "GameData";

        private static GameDataSO _instance;
        public static GameDataSO Instance
        {
            get
            {
                if (_instance == null)
                    _instance = UResources.Load<GameDataSO>(ResourcePath);
                return _instance;
            }
        }

        [SerializeField] private List<ResourceDataSO> resources = new();
        [SerializeField] private List<BuildingDataSO> buildings = new();
        [SerializeField] private List<UnitDataSO> units = new();

        public IReadOnlyList<ResourceDataSO> Resources => resources;
        public IReadOnlyList<BuildingDataSO> Buildings => buildings;
        public IReadOnlyList<UnitDataSO> Units => units;

        // Индексы по ID — строятся лениво
        private Dictionary<string, ResourceDataSO> _resourcesById;
        private Dictionary<string, BuildingDataSO> _buildingsById;
        private Dictionary<string, UnitDataSO> _unitsById;

        public ResourceDataSO GetResource(string id) => Lookup(_resourcesById ??= BuildIndex(resources), id);
        public BuildingDataSO GetBuilding(string id) => Lookup(_buildingsById ??= BuildIndex(buildings), id);
        public UnitDataSO GetUnit(string id) => Lookup(_unitsById ??= BuildIndex(units), id);

        private static T Lookup<T>(Dictionary<string, T> dict, string id) where T : DataSO
            => dict != null && dict.TryGetValue(id, out var v) ? v : null;

        private static Dictionary<string, T> BuildIndex<T>(IReadOnlyList<T> items) where T : DataSO
        {
            var dict = new Dictionary<string, T>();
            if (items == null) return dict;
            foreach (var item in items)
            {
                if (item == null)
                {
                    Debug.LogWarning($"[GameDataSO] Пустая ссылка в списке {typeof(T).Name}");
                    continue;
                }
                if (dict.ContainsKey(item.Id))
                {
                    Debug.LogWarning($"[GameDataSO] Дубликат ID '{item.Id}' в {typeof(T).Name}");
                    continue;
                }
                dict[item.Id] = item;
            }
            return dict;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Сбрасываем кэш при редактировании в инспекторе
            _resourcesById = null;
            _buildingsById = null;
            _unitsById = null;
        }
#endif
    }
}