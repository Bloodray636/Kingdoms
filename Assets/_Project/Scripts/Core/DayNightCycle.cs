using UnityEngine;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Core
{
    [ExecuteAlways]
    public class DayNightCycle : MonoBehaviour
    {
        [Header("Sun / Moon")]
        [SerializeField] private Light sun;
        [SerializeField] private Light moon;
        [SerializeField] private float sunTilt = 30f;

        [Header("Gradients (0 = полночь, 0.5 = полдень, 1 = полночь)")]
        [SerializeField] private Gradient sunColor;

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

        private static DayNightCycle _activeCycle;

        private void OnEnable()
        {
            _activeCycle = this;
            EventBus.Subscribe<GameTickEvent>(OnTick);

            if (Application.isPlaying) {
                RefreshFromTimeManager();
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameTickEvent>(OnTick);

            if (_activeCycle == this) {
                _activeCycle = null;
            }
        }

        private void OnTick(GameTickEvent _)
        {
            if (_activeCycle != this) return;
            RefreshFromTimeManager();
        }

        private void RefreshFromTimeManager()
        {
            if (!Application.isPlaying) return;

            var tm = TimeManager.Instance;

            if (tm == null) return;

            ApplyLighting(tm.DayProgress);
        }

        private void ApplyLighting(float t)
        {
            if (sun == null) return;

            float sunAngle = t * 360f - 90f;
            sun.transform.rotation = Quaternion.Euler(sunAngle, sunTilt, 0f);

            sun.color = sunColor.Evaluate(t);
            sun.intensity = sunIntensity.Evaluate(t);
            sun.enabled = sun.intensity > 0.01f;

            if (moon != null && moonIntensity != null) {
                moon.transform.rotation = Quaternion.Euler(sunAngle + 180f, sunTilt, 0f);
                moon.color = moonColor;
                moon.intensity = moonIntensity.Evaluate(t);
                moon.enabled = moon.intensity > 0.01f;
            }

            RenderSettings.sun = (sun.enabled) ? sun : moon;
            RenderSettings.ambientLight = ambientColor.Evaluate(t);
            RenderSettings.fogColor = fogColor.Evaluate(t);
        }

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