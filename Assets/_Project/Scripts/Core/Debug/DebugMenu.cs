using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.CameraControl;
using KingdomsClone.Core.Events;
using KingdomsClone.Grid;

namespace KingdomsClone.Core.DebugTools
{
    public class DebugMenu : MonoBehaviour
    {
        private bool _isOpen = false;
        private RTSCamera _camera;
        private string _seedInput = "";

        private void Start()
        {
            Debug.Log("[DebugMenu] Скрипт загружен. Нажми клавишу ` (тильда/Ё) для открытия меню.");
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.backquoteKey.wasPressedThisFrame)
            {
                _isOpen = !_isOpen;
            }
        }

        private void OnGUI()
        {
            var tm = TimeManager.Instance;
            var grid = GridSystem.Instance;

            // --- Верхний правый угол: всегда видимая инфа ---
            if (tm != null)
            {
                string info = $"Day {tm.CurrentDay} ({tm.CurrentSeason}) | Speed: {tm.EffectiveSpeed:F1}x";
                GUI.Box(new Rect(Screen.width - 260, 10, 250, 25), info);
            }

            if (!_isOpen) return;

            // --- Основное окно ---
            GUI.Box(new Rect(10, 10, 340, 480), "🛠️ DEBUG MENU (`)");
            GUILayout.BeginArea(new Rect(20, 40, 320, 440));

            // 1. Ресурсы
            GUILayout.Label("--- Resources ---");
            if (GUILayout.Button("+1000 All Resources"))
            {
                if (ResourceManager.Instance != null)
                    ResourceManager.Instance.AddAllResources(1000);
            }

            GUILayout.Space(10);

            // 2. Генерация карты (НОВОЕ!)
            GUILayout.Label("--- Map Generation ---");
            if (grid != null)
            {
                GUILayout.Label($"Current Seed: {grid.CurrentSeed}");

                if (GUILayout.Button("Random Map (Random Seed)"))
                {
                    int newSeed = UnityEngine.Random.Range(1, 999999);
                    grid.RegenerateMap(newSeed);
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label("Seed:", GUILayout.Width(40));
                _seedInput = GUILayout.TextField(_seedInput, GUILayout.Width(120));
                if (GUILayout.Button("Apply Seed"))
                {
                    if (int.TryParse(_seedInput, out int parsedSeed))
                    {
                        grid.RegenerateMap(parsedSeed);
                    }
                    else
                    {
                        Debug.LogWarning($"[Debug] Некорректный сид: '{_seedInput}'");
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("❌ GridSystem not found!");
            }

            GUILayout.Space(10);

            // 3. Враги
            GUILayout.Label("--- Enemies ---");
            if (GUILayout.Button("Spawn Raid (Wave 1)"))
            {
                EventBus.Publish(new SpawnRaidRequestedEvent { WaveNumber = 1 });
                Debug.Log("[Debug] SpawnRaidRequestedEvent published!");
            }

            GUILayout.Space(10);

            // 4. Камера
            GUILayout.Label("--- Camera ---");
            if (GUILayout.Button("Teleport to Center (50,50)"))
            {
                if (_camera == null) _camera = FindObjectOfType<RTSCamera>();
                if (_camera != null) _camera.TeleportTo(new Vector3(50f, 0f, 50f));
            }

            GUILayout.Space(10);

            // 5. Время
            GUILayout.Label("--- Time Speed ---");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1x"))  if (tm != null) tm.SetDebugSpeed(1f);
            if (GUILayout.Button("5x"))  if (tm != null) tm.SetDebugSpeed(5f);
            if (GUILayout.Button("10x")) if (tm != null) tm.SetDebugSpeed(10f);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 6. Состояние игры
            GUILayout.Label("--- Game State ---");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Play"))  GameManager.Instance.StartGame();
            if (GUILayout.Button("Pause")) GameManager.Instance.PauseGame();
            if (GUILayout.Button("Menu"))  GameManager.Instance.QuitToMenu();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}