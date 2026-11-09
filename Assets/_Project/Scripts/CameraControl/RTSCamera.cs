using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.Core;
using KingdomsClone.Grid;
using KingdomsClone.Grid.Events;

namespace KingdomsClone.CameraControl
{
    /// <summary>
    /// RTS-камера: панорамирование (WASD + край экрана), зум колесом,
    /// ограничение по границам карты, плавность через SmoothDamp.
    /// Камера смотрит на "пивот" на земле (Y=0); пивот двигается, камера следует.
    /// Автоматически подхватывает границы при генерации карты через GridGeneratedEvent.
    /// </summary>
    public class RTSCamera : MonoBehaviour
    {
        [Header("Panning")]
        [SerializeField] private float panSpeed = 30f;
        [SerializeField] private float fastPanMultiplier = 2.5f;
        [SerializeField] private float edgePanZonePixels = 15f;
        [SerializeField] private bool edgePanEnabled = true;
        [SerializeField] private float panSmoothTime = 0.15f;

        [Header("Zoom")]
        [SerializeField, Range(0f, 1f)] private float initialZoom = 0.4f;
        [SerializeField] private float minDistance = 10f;
        [SerializeField] private float maxDistance = 40f;
        [SerializeField] private float minPitch = 45f;          // близко: наклон к горизонту
        [SerializeField] private float maxPitch = 70f;          // далеко: top-down
        [SerializeField] private float scrollSensitivity = 0.0015f;
        [SerializeField] private float zoomSmoothTime = 0.15f;

        [Header("Map Bounds (XZ) — автозаполняется из GridSystem")]
        [Tooltip("Если GridSystem есть в сцене — эти значения перезапишутся из события GridGeneratedEvent.")]
        [SerializeField] private Vector2 mapMin = new Vector2(-50f, -50f);
        [SerializeField] private Vector2 mapMax = new Vector2(50f, 50f);

        private Vector3 _currentPivot;
        private Vector3 _targetPivot;
        private Vector3 _pivotVelocity;
        private float _currentZoom;
        private float _targetZoom;
        private float _zoomVelocity;

        // ---------- Lifecycle ----------

        private void OnEnable()
        {
            EventBus.Subscribe<GridGeneratedEvent>(OnGridGenerated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GridGeneratedEvent>(OnGridGenerated);
        }

        private void Start()
        {
            _currentZoom = _targetZoom = Mathf.Clamp01(initialZoom);

            // Если сетка УЖЕ сгенерирована до того, как мы стартовали — забираем границы сразу.
            // (защита от race condition: GridSystem.Start мог отработать раньше нашего OnEnable)
            if (GridSystem.Instance != null && GridSystem.Instance.Grid != null)
            {
                var g = GridSystem.Instance;
                SetBounds(
                    new Vector2(0f, 0f),
                    new Vector2(g.Width * g.CellSize, g.Height * g.CellSize)
                );
            }
            else
            {
                // Иначе остаёмся на дефолтных границах из инспектора
                SetBounds(mapMin, mapMax);
            }
        }

        private void Update()
        {
            HandlePanInput();
            HandleZoomInput();
            ClampPivot();
            SmoothStep();
            ApplyTransform();
        }

        // ---------- Input ----------

        private void HandlePanInput()
        {
            Vector2 input = Vector2.zero;
            bool fast = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  input.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  input.x -= 1f;
                fast = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            }

            if (edgePanEnabled)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 mp = mouse.position.ReadValue();
                    // В редакторе мышь может быть вне Game view — отсекаем такие случаи
                    if (mp.x >= 0 && mp.x <= Screen.width && mp.y >= 0 && mp.y <= Screen.height)
                    {
                        if (mp.x < edgePanZonePixels) input.x -= 1f;
                        if (mp.x > Screen.width - edgePanZonePixels) input.x += 1f;
                        if (mp.y < edgePanZonePixels) input.y -= 1f;
                        if (mp.y > Screen.height - edgePanZonePixels) input.y += 1f;
                    }
                }
            }

            if (input.sqrMagnitude > 0f)
            {
                input = Vector2.ClampMagnitude(input, 1f); // диагональ не быстрее
                float zoomSpeedMul = Mathf.Lerp(0.6f, 1.6f, _currentZoom); // дальше = быстрее
                float speed = panSpeed * zoomSpeedMul * (fast ? fastPanMultiplier : 1f);
                _targetPivot += new Vector3(input.x, 0f, input.y) * speed * Time.deltaTime;
            }
        }

        private void HandleZoomInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;
            // 0 = близко, 1 = далеко. Скролл "вверх" (положительный) = приближаемся.
            _targetZoom = Mathf.Clamp01(_targetZoom - scroll * scrollSensitivity);
        }

        // ---------- Bounds / Transform ----------

        private void ClampPivot()
        {
            _targetPivot.x = Mathf.Clamp(_targetPivot.x, mapMin.x, mapMax.x);
            _targetPivot.z = Mathf.Clamp(_targetPivot.z, mapMin.y, mapMax.y);
        }

        private void SmoothStep()
        {
            _currentPivot = Vector3.SmoothDamp(_currentPivot, _targetPivot, ref _pivotVelocity, panSmoothTime);
            _currentZoom = Mathf.SmoothDamp(_currentZoom, _targetZoom, ref _zoomVelocity, zoomSmoothTime);
        }

        private void ApplyTransform()
        {
            float distance = Mathf.Lerp(minDistance, maxDistance, _currentZoom);
            float pitch = Mathf.Lerp(minPitch, maxPitch, _currentZoom);

            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.position = _currentPivot + rotation * Vector3.back * distance;
            transform.rotation = rotation;
        }

        // ---------- Public API ----------

        /// <summary>Мгновенно переместить камеру в мировую точку (для дебаг-меню).</summary>
        public void TeleportTo(Vector3 worldPos)
        {
            _targetPivot = new Vector3(worldPos.x, 0f, worldPos.z);
            _currentPivot = _targetPivot;
            _pivotVelocity = Vector3.zero;
            ClampPivot();
            ApplyTransform();
            Debug.Log($"[RTSCamera] Teleported to {_targetPivot}");
        }

        /// <summary>Задать границы карты в мировых XZ-координатах.</summary>
        private void SetBounds(Vector2 min, Vector2 max)
        {
            mapMin = min;
            mapMax = max;

            Vector2 center = (mapMin + mapMax) * 0.5f;
            _currentPivot = _targetPivot = new Vector3(center.x, 0f, center.y);
            _pivotVelocity = Vector3.zero;
        }

        private void OnGridGenerated(GridGeneratedEvent e)
        {
            SetBounds(
                new Vector2(e.OriginWorldPosition.x, e.OriginWorldPosition.z),
                new Vector2(
                    e.OriginWorldPosition.x + e.Width * e.CellSize,
                    e.OriginWorldPosition.z + e.Height * e.CellSize
                )
            );
            ApplyTransform();
            Debug.Log($"[RTSCamera] Grid bounds applied: {mapMin} .. {mapMax}");
        }

        // ---------- Gizmos ----------

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 p1 = new Vector3(mapMin.x, 0f, mapMin.y);
            Vector3 p2 = new Vector3(mapMax.x, 0f, mapMin.y);
            Vector3 p3 = new Vector3(mapMax.x, 0f, mapMax.y);
            Vector3 p4 = new Vector3(mapMin.x, 0f, mapMax.y);
            Gizmos.DrawLine(p1, p2); Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p4); Gizmos.DrawLine(p4, p1);

            if (Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_currentPivot + Vector3.up * 0.1f, 0.5f);
            }
        }
    }
}