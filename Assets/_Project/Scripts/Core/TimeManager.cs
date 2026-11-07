using UnityEngine;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Core
{
    /// <summary>
    /// Игровое время: тики, дни, сезоны, скорость.
    /// Управляет Time.timeScale (пауза = 0, скорости 1..3).
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
        private int _lastDay = 1;
        private bool _isPaused = true;   // стартуем в Boot — время стоит

        public float GameTimeSeconds => _gameTimeSeconds;
        public int CurrentDay => Mathf.FloorToInt(_gameTimeSeconds / SecondsPerDay) + 1;
        public float DayProgress => (_gameTimeSeconds % SecondsPerDay) / SecondsPerDay;
        public float Speed => _speed;
        public float EffectiveSpeed => _isPaused ? 0f : _speed;
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

            // Тики: 4 раза в секунду игрового времени
            _tickAccumulator += dt;
            while (_tickAccumulator >= TickInterval)
            {
                _tickAccumulator -= TickInterval;
                EventBus.Publish(new GameTickEvent { DeltaGameSeconds = TickInterval });
            }

            // Смена дня
            int day = CurrentDay;
            if (day != _lastDay)
            {
                _lastDay = day;
                EventBus.Publish(new DayChangedEvent
                {
                    NewDay = day,
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
            if (day != _lastDay)
            {
                _lastDay = day;
                EventBus.Publish(new DayChangedEvent
                {
                    NewDay = day,
                    Year = Year,
                    Season = CurrentSeason
                });
            }
        }
    }
}