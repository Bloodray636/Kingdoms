using System.Collections;
using KingdomsClone.Core.Events;
using KingdomsClone.Data;
using UnityEngine;

namespace KingdomsClone.Core
{
    [DefaultExecutionOrder(-1000)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.Boot;
        public GameState PreviousState { get; private set; } = GameState.Boot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            Debug.Log("[GameManager] Boot: инициализация");

            var data = GameDataSO.Instance;
            if (data == null)
            {
                Debug.LogError("[GameManager] GameDataSO не найден — игра не запустится");
                yield break;
            }

            Debug.Log($"[GameManager] Boot OK: {data.Resources.Count} ресурсов, " +
                      $"{data.Buildings.Count} зданий, {data.Units.Count} юнитов");

            yield return null; // один кадр в Boot — чтобы UI успел подписаться

            // TODO (когда появится меню): TransitionTo(GameState.Menu)
            TransitionTo(GameState.Playing);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Публичный API ----------

        public void StartGame()   => TransitionTo(GameState.Playing);
        public void PauseGame()   { if (CurrentState == GameState.Playing) TransitionTo(GameState.Paused); }
        public void ResumeGame()  { if (CurrentState == GameState.Paused)  TransitionTo(GameState.Playing); }
        public void QuitToMenu()  => TransitionTo(GameState.Menu);

        // ---------- State machine ----------

        public void TransitionTo(GameState next)
        {
            if (next == CurrentState) return;

            if (!IsValidTransition(CurrentState, next))
            {
                Debug.LogWarning($"[GameManager] Недопустимый переход {CurrentState} → {next}");
                return;
            }

            PreviousState = CurrentState;
            CurrentState = next;

            Debug.Log($"[GameManager] {PreviousState} → {next}");

            EventBus.Publish(new GameStateChangedEvent
            {
                PreviousState = PreviousState,
                NewState = next
            });
        }

        private static bool IsValidTransition(GameState from, GameState to) => from switch
        {
            GameState.Boot    => to == GameState.Menu || to == GameState.Playing,
            GameState.Menu    => to == GameState.Playing,
            GameState.Playing => to == GameState.Paused || to == GameState.Menu,
            GameState.Paused  => to == GameState.Playing || to == GameState.Menu,
            _ => false
        };
    }
}