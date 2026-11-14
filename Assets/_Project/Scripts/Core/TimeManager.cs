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

        // ФИКС: лимит тиков за кадр. Защита от лагов при огромной перемотке.
        private const int MaxTicksPerFrame = 256;

        [SerializeField, Range(MinSpeed, MaxSpeed)] private float startSpeed = 1f;

        private float _gameTimeSeconds;
        private float _tickAccumulator;
        private float _speed = 1f;
        private float _debugSpeedMultiplier = 1f;
        private int _lastDay = 1;
        private bool _isPaused = true;

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
        }

        private void OnEnable()  => EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        private void OnDisable() => EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            switch (e.NewState)
            {
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

            float dt = Time.deltaTime;
            _gameTimeSeconds += dt;
            ProcessTicks(dt);
        }

        private void LateUpdate()
        {
            // ФИКС: LateUpdate — финальная реконсиляция Time.timeScale.
            // Если кто-то извне (анимация, скрипт, катсцена) поменял — возвращаем наше значение.
            float expected = EffectiveSpeed;
            if (!Mathf.Approximately(Time.timeScale, expected))
            {
                Time.timeScale = expected;
            }
        }

        private void ProcessTicks(float deltaSeconds)
        {
            _tickAccumulator += deltaSeconds;
            int processed = 0;

            // ФИКС: ограничиваем количество тиков за кадр
            while (_tickAccumulator >= TickInterval && processed < MaxTicksPerFrame)
            {
                _tickAccumulator -= TickInterval;
                EventBus.Publish(new GameTickEvent { DeltaGameSeconds = TickInterval });
                processed++;
            }

            if (processed >= MaxTicksPerFrame)
            {
                Debug.LogWarning($"[TimeManager] Достигнут лимит {MaxTicksPerFrame} тиков за кадр. " +
                                 $"Отстаток {_tickAccumulator:F2}s будет отброшен (возможна потеря точности).");
                _tickAccumulator = 0f;
            }

            // Смена дня — тоже с защитой
            int currentDay = CurrentDay;
            int dayGuard = 0;
            while (currentDay != _lastDay && dayGuard < MaxTicksPerFrame)
            {
                _lastDay++;
                PublishDayChanged(_lastDay);
                dayGuard++;
            }

            if (dayGuard >= MaxTicksPerFrame)
            {
                Debug.LogWarning($"[TimeManager] Достигнут лимит смены дней за кадр. " +
                                 $"Возможно, _lastDay={_lastDay} != CurrentDay={currentDay}.");
                _lastDay = currentDay;
            }
        }

        private void PublishDayChanged(int day)
        {
            EventBus.Publish(new DayChangedEvent
            {
                NewDay = day,
                Year = ((day - 1) / (DaysPerSeason * 4)) + 1,
                Season = (Season)(((day - 1) / DaysPerSeason) % 4)
            });
        }

        // ============ ПУБЛИЧНЫЙ API ============

        public void SetSpeed(float newSpeed)
        {
            newSpeed = Mathf.Clamp(newSpeed, MinSpeed, MaxSpeed);
            if (Mathf.Approximately(_speed, newSpeed)) return;

            float old = _speed;
            _speed = newSpeed;
            EventBus.Publish(new GameSpeedChangedEvent { OldSpeed = old, NewSpeed = newSpeed });
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

        // ============ ПРОДВИЖЕНИЕ ВРЕМЕНИ ============

        /// <summary>
        /// Продвинуть игровое время на N секунд. НЕ РАБОТАЕТ НА ПАУЗЕ.
        /// Используется игровыми механиками: сон жителя, ускорение по кнопке, ритуалы.
        /// </summary>
        public void AdvanceGameTime(float seconds)
        {
            if (seconds <= 0f) return;
            if (_isPaused)
            {
                Debug.LogWarning($"[TimeManager] AdvanceGameTime({seconds}) проигнорирован: игра на паузе.");
                return;
            }

            _gameTimeSeconds += seconds;
            ProcessTicks(seconds);
        }

        /// <summary>
        /// ПРИНУДИТЕЛЬНО продвинуть время. Работает даже на паузе.
        /// ТОЛЬКО ДЛЯ ДЕБАГА/ЧИТОВ. Игровые механики должны использовать AdvanceGameTime.
        /// </summary>
        public void DebugAdvanceGameTime(float seconds)
        {
            if (seconds <= 0f) return;

            _gameTimeSeconds += seconds;
            ProcessTicks(seconds);
            Debug.Log($"[TimeManager] DEBUG перемотка {seconds}s → GameTime={_gameTimeSeconds:F1}s, Day={CurrentDay}");
        }

        // Обратная совместимость: старый код вызывает AddGameSeconds — оставим алиас
        [System.Obsolete("Используй AdvanceGameTime (уважает паузу) или DebugAdvanceGameTime (всегда)")]
        public void AddGameSeconds(float seconds) => DebugAdvanceGameTime(seconds);
    }
}