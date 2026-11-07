using UnityEngine;
using KingdomsClone.Data;

namespace KingdomsClone.Utils
{
    public class DataSanityCheck : MonoBehaviour
    {
        private void Start()
        {
            var data = GameDataSO.Instance;
            if (data == null)
            {
                Debug.LogError("[DataSanityCheck] GameDataSO не найден в Resources!");
                return;
            }

            Debug.Log($"✅ GameData: {data.Resources.Count} ресурсов, " +
                      $"{data.Buildings.Count} зданий, {data.Units.Count} юнитов");

            foreach (var r in data.Resources)
                Debug.Log($"   [R] {r.Id} — {r.DisplayName} ({r.Category})");

            foreach (var b in data.Buildings)
                Debug.Log($"   [B] {b.Id} — {b.DisplayName}, {b.Size.x}×{b.Size.y}, " +
                          $"HP {b.MaxHealth}, IsProducer={b.IsProducer}");

            foreach (var u in data.Units)
                Debug.Log($"   [U] {u.Id} — {u.DisplayName}, " +
                          $"HP {u.MaxHealth}, Worker={u.IsWorker}, Combat={u.IsCombatant}");

            // Проверка лукапов по ID
            var wood = data.GetResource("Resource_Wood");
            Debug.Log(wood != null
                ? $"✅ Lookup по ID работает: {wood.DisplayName}"
                : "❌ Lookup по ID не работает");
        }
    }
}