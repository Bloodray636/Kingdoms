using UnityEngine;

namespace KingdomsClone.Data
{
    public abstract class DataSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Уникальный строковый ID. Если пусто — берётся имя ассета.")]
        [SerializeField] private string id;

        [Tooltip("Отображаемое имя для UI. Если пусто — берётся имя ассета.")]
        [SerializeField] private string displayName;

        [Tooltip("Описание для тултипов.")]
        [SerializeField, TextArea(2, 5)] private string description;

        [Tooltip("Иконка для UI.")]
        [SerializeField] private Sprite icon;

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // Автозаполнение ID из имени ассета — защита от опечаток.
            if (string.IsNullOrEmpty(id))
                id = name;
        }
#endif
    }
}