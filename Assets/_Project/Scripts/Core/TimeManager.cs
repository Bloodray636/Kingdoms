using UnityEngine;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Core
{
    /// <summary>
    /// Игровое время: тики, дни, сезоны, скорость.
    /// Управляет Time.timeScale (пауза = 0, скорости 1..3, но дебаг может выкрутить x10+).
    /// Сам подписан на GameStateChangedEvent — пауза игры = пауза времени.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        public const float SecondsPerDay = 60f;
        public const int DaysPerSeason = 30;
        public const int TicksPerSecond = 4;
        public const float MinSpeed = 1f;
        public const float MaxSpeed = 3f;
        private const float TickInterval = 1f / TicksPerSecond;  // 0.25

        [SerializeField, Range(MinSpeed, MaxSpeed)] private float startSpeed = 1f;

        private float _gameTimeSeconds;
        private float _tickAccumulator;
        private float _speed = 1f;
        private float _debugSpeedMultiplier = 1f;  // Для читов (x10) — не сбрасывается обычным SetSpeed
        private int _lastDay = 1;
        private bool _isPaused = true;   // стартуем в Boot — время стоит

        public float GameTimeSeconds => _gameTimeSeconds;
        public int CurrentDay => Mathf.FloorToInt(_gameTimeSeconds / SecondsPerDay) + 1;
        public float DayProgress => (_gameTimeSeconds % SecondsPerDay) / SecondsPerDay;
        public float Speed => _speed;
        public float EffectiveSpeed => _isPaused ? 0f : _speed * _debugSpeedMultiplier;
        public bool IsPaused => _isPaused;

        public int Year => ((CurrentDay - 1) / (DaysPerSeason * 4)) + 1;
        public Season CurrentSeason => (Season)(((CurrentDay - 1) / DaysPerSeason) % 4);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _speed = Mathf.Clamp(startSpeed, MinSpeed, MaxSpeed);
            ApplyTimeScale();
        }

        private void OnEnable()  => EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        private void OnDisable() => EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            if (e.NewState == GameState.Playing)      SetPaused(false);
            else if (e.NewState == GameState.Paused)  SetPaused(true);
            // Menu/Boot — не трогаем (решим, когда появится реальное меню)
        }

        private void Update()
        {
            if (EffectiveSpeed <= 0f) return;

            float dt = Time.deltaTime;  // уже масштабирован через Time.timeScale
            _gameTimeSeconds += dt;

            // Тики: 4 раза в секунду игрового времени.
            // ВАЖНО: на скорости x10 и выше dt может быть больше TickInterval (0.25),
            // поэтому в одном кадре может вылететь несколько тиков подряд. Цикл while это покрывает.
            _tickAccumulator += dt;
            while (_tickAccumulator >= TickInterval)
            {
                _tickAccumulator -= TickInterval;
                EventBus.Publish(new GameTickEvent { DeltaGameSeconds = TickInterval });
            }

            // Смена дня. На x10 день может смениться не по одному — цикл это покроет.
            int day = CurrentDay;
            while (day != _lastDay)
            {
                _lastDay++;
                EventBus.Publish(new DayChangedEvent
                {
                    NewDay = _lastDay,
                    Year = Year,
                    Season = CurrentSeason
                });
            }
        }

        // ---------- Публичный API ----------

        public void SetSpeed(float newSpeed)
        {
            newSpeed = Mathf.Clamp(newSpeed, MinSpeed, MaxSpeed);
            if (Mathf.Approximately(_speed, newSpeed)) return;

            float old = _speed;
            _speed = newSpeed;
            ApplyTimeScale();
            EventBus.Publish(new GameSpeedChangedEvent { OldSpeed = old, NewSpeed = newSpeed });
        }

        /// <summary>
        /// Дебаг-множитель скорости. Применяется поверх обычной скорости.
        /// SetDebugSpeed(10) при Speed=1 → время идёт x10.
        /// SetDebugSpeed(1)  → возвращает обычную скорость.
        /// </summary>
        public void SetDebugSpeed(float multiplier)
        {
            _debugSpeedMultiplier = Mathf.Max(1f, multiplier);
            ApplyTimeScale();
            Debug.Log($"[TimeManager] Debug speed multiplier set to {_debugSpeedMultiplier}x (effective: {EffectiveSpeed}x)");
        }

        private void SetPaused(bool paused)
        {
            if (_isPaused == paused) return;
            _isPaused = paused;
            ApplyTimeScale();
        }

        private void ApplyTimeScale()
        {
            Time.timeScale = EffectiveSpeed;
        }

        /// <summary>Дебаг: промотать игровое время вперёд. Для тестов и читов.</summary>
        public void AddGameSeconds(float seconds)
        {
            if (seconds <= 0f) return;
            _gameTimeSeconds += seconds;
            
            int day = CurrentDay;
            while (day != _lastDay)
            {
                _lastDay++;
                EventBus.Publish(new DayChangedEvent
                {
                    NewDay = _lastDay,
                    Year = Year,
                    Season = CurrentSeason
                });
            }
        }
    }
}