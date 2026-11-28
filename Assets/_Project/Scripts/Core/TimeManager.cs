using UnityEngine;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Core
{
    [DefaultExecutionOrder(-900)]
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        public const float SecondsPerDay = 60f;
        public const int DaysPerSeason = 30;
        public const int TicksPerSecond = 4;
        public const float MinSpeed = 1f;
        public const float MaxSpeed = 3f;
        private const float TickInterval = 1f / TicksPerSecond;
        private const int MaxTicksPerFrame = 256;

        [SerializeField, Range(MinSpeed, MaxSpeed)] private float startSpeed = 1f;

        private float _gameTimeSeconds;
        private float _tickAccumulator;
        private float _speed = 1f;
        private float _debugSpeedMultiplier = 1f;
        private int _lastDay = 1;
        private Season _lastSeason = Season.Spring; 
        private bool _isPaused = true;
        private float _lastBacklogWarningTime = -10f;

        public float GameTimeSeconds => _gameTimeSeconds;
        public int CurrentDay => Mathf.FloorToInt(_gameTimeSeconds / SecondsPerDay) + 1;
        public float DayProgress => (_gameTimeSeconds % SecondsPerDay) / SecondsPerDay;
        public float Speed => _speed;
        public float EffectiveSpeed => _isPaused ? 0f : _speed * _debugSpeedMultiplier;
        public bool IsPaused => _isPaused;

        public int Year => ((CurrentDay - 1) / (DaysPerSeason * 4)) + 1;
        public Season CurrentSeason => (Season)(((CurrentDay - 1) / DaysPerSeason) % 4);

        public int DayInSeason => ((CurrentDay - 1) % DaysPerSeason) + 1;

        public float SeasonProgress => DaysPerSeason > 1 ? (float)(DayInSeason - 1) / (DaysPerSeason - 1) : 0f;

        public static string GetSeasonNameRu(Season s) => s switch
        {
            Season.Spring => "Весна",
            Season.Summer => "Лето",
            Season.Autumn => "Осень",
            Season.Winter => "Зима",
            _ => s.ToString()
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { 
                Destroy(gameObject); 
                return; 
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _speed = Mathf.Clamp(startSpeed, MinSpeed, MaxSpeed);
        }

        private void OnEnable() => EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        private void OnDisable() => EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);

        private void OnDestroy() { 
            if (Instance == this) {
                Instance = null;
            } 
        }

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            switch (e.NewState) {
                case GameState.Playing:
                    SetPaused(false);
                    break;
                case GameState.Paused:
                case GameState.Menu:
                case GameState.Boot:
                    SetPaused(true);
                    break;
            }
        }

        private void Update()
        {
            if (EffectiveSpeed <= 0f) return;
            ProcessTicks(Time.deltaTime, allowUncapped: false);
        }

        private void LateUpdate()
        {
            float expected = EffectiveSpeed;

            if (!Mathf.Approximately(Time.timeScale, expected)) {
                Time.timeScale = expected;
            }
        }

        private void ProcessTicks(float deltaSeconds, bool allowUncapped)
        {
            if (deltaSeconds <= 0f) return;
            _tickAccumulator += deltaSeconds;

            int maxTicks = allowUncapped ? int.MaxValue : MaxTicksPerFrame;
            int processed = 0;

            while (_tickAccumulator >= TickInterval && processed < maxTicks) {
                _tickAccumulator -= TickInterval;
                _gameTimeSeconds += TickInterval;

                EventBus.Publish(new GameTickEvent { DeltaGameSeconds = TickInterval });
                processed++;

                int currentDay = Mathf.FloorToInt(_gameTimeSeconds / SecondsPerDay) + 1;

                while (_lastDay < currentDay) {
                    _lastDay++;
                    PublishDayChanged(_lastDay);
                }
            }

            if (!allowUncapped && processed >= MaxTicksPerFrame && _tickAccumulator >= TickInterval) {
                if (Time.time - _lastBacklogWarningTime > 1f) {
                    _lastBacklogWarningTime = Time.time;
                    Debug.LogWarning($"[TimeManager] Backlog {_tickAccumulator:F2}s — догоняем на след. кадрах");
                }
            }
        }

        private void PublishDayChanged(int day)
        {
            int year = ((day - 1) / (DaysPerSeason * 4)) + 1;
            Season season = (Season)(((day - 1) / DaysPerSeason) % 4);

            EventBus.Publish(new DayChangedEvent
            {
                NewDay = day,
                Year = year,
                Season = season
            });

            if (season != _lastSeason)
            {
                var oldSeason = _lastSeason;
                _lastSeason = season;

                EventBus.Publish(new SeasonChangedEvent
                {
                    OldSeason = oldSeason,
                    NewSeason = season,
                    Year = year
                });

                Debug.Log($"[TimeManager] Сезон: {oldSeason} → {season} (Год {year}, День {day})");
            }
        }

        public void SetSpeed(float newSpeed)
        {
            newSpeed = Mathf.Clamp(newSpeed, MinSpeed, MaxSpeed);

            if (Mathf.Approximately(_speed, newSpeed)) return;

            float old = _speed;
            _speed = newSpeed;

            EventBus.Publish(new GameSpeedChangedEvent { 
                OldSpeed = old, 
                NewSpeed = newSpeed 
            });
        }

        public void SetDebugSpeed(float multiplier)
        {
            _debugSpeedMultiplier = Mathf.Max(1f, multiplier);
            Debug.Log($"[TimeManager] Debug speed ×{_debugSpeedMultiplier} (effective {EffectiveSpeed}x)");
        }

        private void SetPaused(bool paused)
        {
            if (_isPaused == paused) return;
            _isPaused = paused;
        }

        public void AdvanceGameTime(float seconds)
        {
            if (seconds <= 0f) return;

            if (_isPaused) {
                Debug.LogWarning($"[TimeManager] AdvanceGameTime({seconds}) проигнорирован: игра на паузе.");
                return;
            }

            ProcessTicks(seconds, allowUncapped: true);
        }

        public void DebugAdvanceGameTime(float seconds)
        {
            if (seconds <= 0f) return;

            int oldDay = CurrentDay;
            int estimatedTicks = Mathf.FloorToInt(seconds * TicksPerSecond);
            ProcessTicks(seconds, allowUncapped: true);

            Debug.Log($"[TimeManager] DEBUG перемотка {seconds}s (~{estimatedTicks} тиков) → " + $"GameTime={_gameTimeSeconds:F1}s, Day={oldDay}→{CurrentDay}");
        }

        [System.Obsolete("Используй AdvanceGameTime (уважает паузу) или DebugAdvanceGameTime (всегда)")]
        public void AddGameSeconds(float seconds) => DebugAdvanceGameTime(seconds);
    }
}