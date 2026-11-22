using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.Core;
using KingdomsClone.Grid;
using KingdomsClone.Grid.Events;
using UnityEngine.EventSystems;
using KingdomsClone.Core.DebugTools;

namespace KingdomsClone.CameraControl
{
    public class RTSCamera : MonoBehaviour
    {
        [Header("Panning")]
        [SerializeField] private float panSpeed = 30f;
        [SerializeField] private float fastPanMultiplier = 2.5f;
        [SerializeField] private float edgePanZonePixels = 15f;
        [SerializeField] private bool edgePanEnabled = true;
        [SerializeField] private float panSmoothTime = 0.15f;
        [Tooltip("0 = пивот зажат строго в [min,max], 1 = видимый край камеры совпадает с краем карты. " + "0.85 = можно чуть заглянуть за край (как в K&C).")]
        [SerializeField, Range(0f, 1.5f)] private float boundsPadding = 0.85f;

        [Header("Zoom")]
        [SerializeField, Range(0f, 1f)] private float initialZoom = 0.4f;
        [SerializeField] private float minDistance = 10f;
        [SerializeField] private float maxDistance = 150f;
        [SerializeField] private float minPitch = 45f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private float scrollSensitivity = 0.05f;
        [SerializeField] private float zoomSmoothTime = 0.15f;

        [Header("Orthographic Size (если камера ortho)")]
        [Tooltip("orthographicSize при zoom=0 (близко).")]
        [SerializeField] private float orthographicMinSize = 5f;
        [Tooltip("orthographicSize при zoom=1 (далеко).")]
        [SerializeField] private float orthographicMaxSize = 40f;

        [Header("Map Bounds (XZ) — автозаполняется из GridSystem")]
        [SerializeField] private Vector2 mapMin = new Vector2(-50f, -50f);
        [SerializeField] private Vector2 mapMax = new Vector2(50f, 50f);

        private Vector3 _currentPivot;
        private Vector3 _targetPivot;
        private Vector3 _pivotVelocity;
        private float _currentZoom;
        private float _targetZoom;
        private float _zoomVelocity;

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

            if (GridSystem.Instance != null && GridSystem.Instance.Grid != null) {
                var g = GridSystem.Instance;

                Vector3 origin = g.OriginPosition;

                SetBounds(
                    new Vector2(origin.x, origin.z),
                    new Vector2(origin.x + g.Width * g.CellSize, origin.z + g.Height * g.CellSize)
                );

                Vector3 startWorld = g.CellToWorld(g.StartLocation);
                _currentPivot = _targetPivot = startWorld;
                _pivotVelocity = Vector3.zero;

                ClampPivot();
                ApplyTransform();
            } else {
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

        private void HandlePanInput()
        {
            Vector2 input = Vector2.zero;
            bool fast = false;

            bool inputBlockedByUI = GUIUtility.keyboardControl != 0;

            var kb = Keyboard.current;

            if (kb != null && !inputBlockedByUI) {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) {
                    input.y += 1f;
                }

                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) {
                    input.y -= 1f;
                }

                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) {
                    input.x += 1f;
                }

                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) {
                    input.x -= 1f;
                }

                fast = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            }

            if (edgePanEnabled) {
                var mouse = Mouse.current;

                if (mouse != null) {
                    bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) || DebugMenu.IsOpen;

                    if (!overUI) {
                        Vector2 mp = mouse.position.ReadValue();

                        if (mp.x >= 0 && mp.x <= Screen.width && mp.y >= 0 && mp.y <= Screen.height) {
                            if (mp.x < edgePanZonePixels) {
                                input.x -= 1f;
                            }

                            if (mp.x > Screen.width - edgePanZonePixels) {
                                input.x += 1f;
                            }

                            if (mp.y < edgePanZonePixels) {
                                input.y -= 1f;
                            }

                            if (mp.y > Screen.height - edgePanZonePixels) {
                                input.y += 1f;
                            }
                        }
                    }
                }
            }

            if (input.sqrMagnitude > 0f) {
                input = Vector2.ClampMagnitude(input, 1f);

                float yaw = transform.eulerAngles.y;
                Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 forward = yawRotation * Vector3.forward;
                Vector3 right = yawRotation * Vector3.right;
                Vector3 worldDir = forward * input.y + right * input.x;

                float zoomSpeedMul = Mathf.Lerp(0.6f, 1.6f, _currentZoom);
                float speed = panSpeed * zoomSpeedMul * (fast ? fastPanMultiplier : 1f);

                _targetPivot += worldDir * speed * Time.unscaledDeltaTime;
            }
        }

        private void HandleZoomInput()
        {
            var mouse = Mouse.current;

            if (mouse == null) return;
            if (DebugMenu.IsOpen) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (GUIUtility.keyboardControl != 0) return;

            float scroll = mouse.scroll.ReadValue().y;

            if (Mathf.Abs(scroll) < 0.01f) return;

            _targetZoom = Mathf.Clamp01(_targetZoom - scroll * scrollSensitivity);
        }

        private void ClampPivot()
        {
            var cam = Camera.main;

            if (cam == null) {
                _targetPivot.x = Mathf.Clamp(_targetPivot.x, mapMin.x, mapMax.x);
                _targetPivot.z = Mathf.Clamp(_targetPivot.z, mapMin.y, mapMax.y);
                return;
            }

            float pitchDeg = Mathf.Lerp(minPitch, maxPitch, _targetZoom);
            float pitchRad = pitchDeg * Mathf.Deg2Rad;

            float halfVertical;
            float groundHalfX;
            float groundHalfZ;

            if (cam.orthographic) {
                halfVertical = Mathf.Lerp(orthographicMinSize, orthographicMaxSize, _targetZoom);
                groundHalfZ = halfVertical / Mathf.Max(0.1f, Mathf.Sin(pitchRad));
                groundHalfX = halfVertical * cam.aspect;
            } else {
                // Перспектива
                float distance = Mathf.Lerp(minDistance, maxDistance, _targetZoom);
                float halfFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;

                halfVertical = distance * Mathf.Tan(halfFovRad);
                groundHalfZ = halfVertical / Mathf.Max(0.1f, Mathf.Sin(pitchRad));
                groundHalfX = groundHalfZ * cam.aspect;
            }

            groundHalfX *= boundsPadding;
            groundHalfZ *= boundsPadding;

            float minX = mapMin.x + groundHalfX;
            float maxX = mapMax.x - groundHalfX;
            float minZ = mapMin.y + groundHalfZ;
            float maxZ = mapMax.y - groundHalfZ;

            // Если карта меньше видимой области
            if (minX > maxX) minX = maxX = (mapMin.x + mapMax.x) * 0.5f;
            if (minZ > maxZ) minZ = maxZ = (mapMin.y + mapMax.y) * 0.5f;

            _targetPivot.x = Mathf.Clamp(_targetPivot.x, minX, maxX);
            _targetPivot.z = Mathf.Clamp(_targetPivot.z, minZ, maxZ);
        }

        private void SmoothStep()
        {
            _currentPivot = Vector3.SmoothDamp(_currentPivot, _targetPivot, ref _pivotVelocity, panSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            _currentZoom = Mathf.SmoothDamp(_currentZoom, _targetZoom, ref _zoomVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        private void ApplyTransform()
        {
            float pitch = Mathf.Lerp(minPitch, maxPitch, _currentZoom);
            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);

            var cam = GetComponent<Camera>();

            if (cam != null && cam.orthographic) {
                cam.orthographicSize = Mathf.Lerp(orthographicMinSize, orthographicMaxSize, _currentZoom);
                transform.position = _currentPivot + rotation * Vector3.back * 50f;
            } else {
                float distance = Mathf.Lerp(minDistance, maxDistance, _currentZoom);
                transform.position = _currentPivot + rotation * Vector3.back * distance;
            }

            transform.rotation = rotation;
        }

        public void TeleportTo(Vector3 worldPos)
        {
            _targetPivot = new Vector3(worldPos.x, 0f, worldPos.z);
            _currentPivot = _targetPivot;
            _pivotVelocity = Vector3.zero;
            ClampPivot();
            ApplyTransform();
            Debug.Log($"[RTSCamera] Teleported to {_targetPivot}");
        }

        private void SetBounds(Vector2 min, Vector2 max)
        {
            mapMin = min;
            mapMax = max;
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

            Vector3 startWorld = new Vector3(
                e.StartLocation.x * e.CellSize + (e.CellSize * 0.5f) + e.OriginWorldPosition.x,
                0f,
                e.StartLocation.y * e.CellSize + (e.CellSize * 0.5f) + e.OriginWorldPosition.z
            );

            _currentPivot = _targetPivot = startWorld;
            _pivotVelocity = Vector3.zero;

            ClampPivot();
            ApplyTransform();
            Debug.Log($"[RTSCamera] Grid bounds applied. Centered on StartLocation: {e.StartLocation}");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 p1 = new Vector3(mapMin.x, 0f, mapMin.y);
            Vector3 p2 = new Vector3(mapMax.x, 0f, mapMin.y);
            Vector3 p3 = new Vector3(mapMax.x, 0f, mapMax.y);
            Vector3 p4 = new Vector3(mapMin.x, 0f, mapMax.y);
            Gizmos.DrawLine(p1, p2); Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p4); Gizmos.DrawLine(p4, p1);

            if (Application.isPlaying) {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_currentPivot + Vector3.up * 0.1f, 0.5f);
            }
        }
    }
}