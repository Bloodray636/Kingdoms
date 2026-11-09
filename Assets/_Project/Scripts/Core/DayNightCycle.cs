using UnityEngine;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Core
{
    [ExecuteAlways]
    public class DayNightCycle : MonoBehaviour
    {
        [Header("Sun / Moon")]
        [SerializeField] private Light sun;
        [SerializeField] private Light moon; // опционально
        [SerializeField] private float sunTilt = 30f; 

        [Header("Gradients (0 = полночь, 0.5 = полдень, 1 = полночь)")]
        [SerializeField] private Gradient sunColor;
        
        // ИСПРАВЛЕНИЕ 3.1: Правильная кривая с 5 точками. 
        // 00:00 - 0.0 | 06:00 - 0.0 | 12:00 - 1.2 | 18:00 - 0.0 | 24:00 - 0.0
        [SerializeField] private AnimationCurve sunIntensity = new AnimationCurve(
            new Keyframe(0f, 0f), 
            new Keyframe(0.25f, 0f), 
            new Keyframe(0.5f, 1.2f), 
            new Keyframe(0.75f, 0f), 
            new Keyframe(1f, 0f)
        );
        
        [SerializeField] private Gradient ambientColor;
        [SerializeField] private Gradient fogColor;

        [Header("Moon")]
        [SerializeField] private AnimationCurve moonIntensity;
        [SerializeField] private Color moonColor = new Color(0.6f, 0.7f, 1f);

        private const float SunriseHour = 6f;
        private const float SunsetHour = 18f;

        private void OnEnable()
        {
            EventBus.Subscribe<GameTickEvent>(OnTick);
            RefreshFromTimeManager();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameTickEvent>(OnTick);
        }

        private void OnTick(GameTickEvent _)
        {
            RefreshFromTimeManager();
        }

        private void RefreshFromTimeManager()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return;

            float t = tm.DayProgress;
            ApplyLighting(t);
        }

        private void ApplyLighting(float t)
        {
            if (sun == null) return;

            // Солнце: 6:00 = горизонт (0°), 12:00 = зенит (90°), 18:00 = горизонт (180°)
            float sunAngle = t * 360f - 90f;
            sun.transform.rotation = Quaternion.Euler(sunAngle, sunTilt, 0f);

            sun.color = sunColor.Evaluate(t);
            sun.intensity = sunIntensity.Evaluate(t);
            sun.enabled = sun.intensity > 0.01f; // ночью отключаем — экономия на тенях

            // Луна: противофаза солнцу
            if (moon != null && moonIntensity != null)
            {
                moon.transform.rotation = Quaternion.Euler(sunAngle + 180f, sunTilt, 0f);
                moon.color = moonColor;
                moon.intensity = moonIntensity.Evaluate(t);
                moon.enabled = moon.intensity > 0.01f;
            }

            // Солнце для скайбокса — то, что светит сильнее
            RenderSettings.sun = (sun.enabled) ? sun : moon;

            // Ambient и туман
            RenderSettings.ambientLight = ambientColor.Evaluate(t);
            RenderSettings.fogColor = fogColor.Evaluate(t);
        }

        // ---------- Preview в редакторе (без Play) ----------
        [ContextMenu("Preview 00:00 (Midnight)")]
        private void PreviewMidnight() => ApplyLighting(0f);

        [ContextMenu("Preview 06:00 (Sunrise)")]
        private void PreviewSunrise() => ApplyLighting(0.25f);

        [ContextMenu("Preview 12:00 (Noon)")]
        private void PreviewNoon() => ApplyLighting(0.5f);

        [ContextMenu("Preview 18:00 (Sunset)")]
        private void PreviewSunset() => ApplyLighting(0.75f);
    }
}