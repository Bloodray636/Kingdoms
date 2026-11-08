using UnityEngine;
using KingdomsClone.CameraControl;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Core.DebugTools
{
    public class DebugMenu : MonoBehaviour
    {
        private bool _isOpen = false;
        private RTSCamera _camera;

        private void Update()
        {
            // F10 — открыть/закрыть меню
            if (Input.GetKeyDown(KeyCode.F10))
                _isOpen = !_isOpen;
        }

        private void OnGUI()
        {
            // --- Верхний правый угол: всегда видимая инфа ---
            var tm = TimeManager.Instance;
            if (tm != null)
            {
                string info = $"Day {tm.CurrentDay} ({tm.CurrentSeason}) | Speed: {tm.EffectiveSpeed:F1}x";
                GUI.Box(new Rect(Screen.width - 260, 10, 250, 25), info);
            }

            if (!_isOpen) return;

            // --- Основное окно (слева сверху) ---
            GUI.Box(new Rect(10, 10, 320, 360), "🛠️ DEBUG MENU (F10)");
            GUILayout.BeginArea(new Rect(20, 40, 300, 320));

            // 1. Ресурсы
            GUILayout.Label("--- Resources ---");
            if (GUILayout.Button("+1000 All Resources"))
            {
                if (ResourceManager.Instance != null)
                    ResourceManager.Instance.AddAllResources(1000);
                else
                    Debug.LogWarning("[Debug] ResourceManager not found in scene!");
            }

            GUILayout.Space(10);

            // 2. Враги
            GUILayout.Label("--- Enemies ---");
            if (GUILayout.Button("Spawn Raid (Wave 1)"))
            {
                EventBus.Publish(new SpawnRaidRequestedEvent { WaveNumber = 1 });
                Debug.Log("[Debug] SpawnRaidRequestedEvent published!");
            }

            GUILayout.Space(10);

            // 3. Камера
            GUILayout.Label("--- Camera ---");
            if (GUILayout.Button("Teleport to Center (0,0)"))
            {
                if (_camera == null) _camera = FindObjectOfType<RTSCamera>();
                if (_camera != null) _camera.TeleportTo(Vector3.zero);
            }
            if (GUILayout.Button("Teleport to Corner (30,30)"))
            {
                if (_camera == null) _camera = FindObjectOfType<RTSCamera>();
                if (_camera != null) _camera.TeleportTo(new Vector3(30f, 0f, 30f));
            }

            GUILayout.Space(10);

            // 4. Время
            GUILayout.Label("--- Time Speed ---");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1x"))  if (tm != null) tm.SetDebugSpeed(1f);
            if (GUILayout.Button("5x"))  if (tm != null) tm.SetDebugSpeed(5f);
            if (GUILayout.Button("10x")) if (tm != null) tm.SetDebugSpeed(10f);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 5. Состояние игры
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