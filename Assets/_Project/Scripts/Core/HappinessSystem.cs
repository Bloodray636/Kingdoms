using UnityEngine;

namespace KingdomsClone.Core
{
    /// <summary>
    /// ЗАГЛУШКА под Epic 6. Пока возвращает фиксированное значение.
    /// В Epic 6 будет считаться из модификаторов (налоги, здания, еда и т.д.).
    /// </summary>
    public class HappinessSystem : MonoBehaviour
    {
        public static HappinessSystem Instance { get; private set; }

        [SerializeField] private float defaultHappiness = 5f;

        public float CurrentHappiness => defaultHappiness;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // Для теста из DebugMenu
        public void SetHappiness(float value) => defaultHappiness = Mathf.Clamp(value, 0f, 10f);
    }
}