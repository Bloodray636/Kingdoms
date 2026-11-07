using UnityEngine;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Core.DebugTools
{
    public class TimeDebugOverlay : MonoBehaviour
    {
        private void OnEnable()  => EventBus.Subscribe<DayChangedEvent>(OnDayChanged);
        private void OnDisable() => EventBus.Unsubscribe<DayChangedEvent>(OnDayChanged);

        private void OnDayChanged(DayChangedEvent e)
        {
            Debug.Log($"[Time] День {e.NewDay}, {SeasonRu(e.Season)}, год {e.Year}");
        }

        private void Update()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return;

            if (Input.GetKeyDown(KeyCode.F5)) tm.SetSpeed(1f);
            if (Input.GetKeyDown(KeyCode.F6)) tm.SetSpeed(2f);
            if (Input.GetKeyDown(KeyCode.F7)) tm.SetSpeed(3f);
        }

        private void OnGUI()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return;

            GUILayout.BeginArea(new Rect(Screen.width - 280, 10, 270, 110), GUI.skin.box);
            GUILayout.Label($"Day {tm.CurrentDay}  ({tm.DayProgress:P0})  {SeasonRu(tm.CurrentSeason)}  Year {tm.Year}");
            GUILayout.Label($"Speed: {tm.Speed}x   {(tm.IsPaused ? "⏸ PAUSED" : "▶ running")}");
            GUILayout.Label("F5=1x  F6=2x  F7=3x   (F2/F3 = GameState pause)");
            GUILayout.EndArea();
        }

        private static string SeasonRu(Season s) => s switch
        {
            Season.Spring => "Весна",
            Season.Summer => "Лето",
            Season.Autumn => "Осень",
            Season.Winter => "Зима",
            _ => s.ToString()
        };
    }
}