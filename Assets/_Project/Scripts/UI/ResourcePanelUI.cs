using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KingdomsClone.Core;
using KingdomsClone.Data;

namespace KingdomsClone.UI
{
    public class ResourcePanelUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float panelHeight = 46f;
        [SerializeField] private float slotWidth = 110f;
        [SerializeField] private float slotSpacing = 6f;
        [SerializeField] private Vector2 panelPadding = new Vector2(12f, 4f);

        [Header("Colors")]
        [SerializeField] private Color panelBg = new Color(0.07f, 0.08f, 0.11f, 0.92f);
        [SerializeField] private Color slotBg = new Color(0.12f, 0.14f, 0.18f, 1f);
        [SerializeField] private Color gainFlash = new Color(0.3f, 0.9f, 0.3f, 1f);
        [SerializeField] private Color lossFlash = new Color(0.9f, 0.3f, 0.3f, 1f);

        [Header("Flash")]
        [SerializeField] private float flashDuration = 0.4f;

        private Font _font;
        private Canvas _canvas;
        private GameObject _panel;
        private RectTransform _panelRect; 
        private RectTransform _rowRect;

        private class Slot
        {
            public ResourceDataSO resource;
            public string shortName;
            public Text amountText;
            public Image bgImage;
            public float flashTimer;
            public Color baseColor;
            public Color flashColor;
        }

        private readonly List<Slot> _slots = new();
        private readonly Dictionary<string, Slot> _slotsById = new();

        private void Awake()
        {
            _font = LoadFont();
            BuildUI();
        }

        private void Start()
        {
            PopulateFromGameData();
            Subscribe();
        }

        private void OnDestroy() => Unsubscribe();

        private void Update()
        {
            for (int i = 0; i < _slots.Count; i++) {
                var s = _slots[i];

                if (s.flashTimer <= 0) continue;

                s.flashTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(s.flashTimer / flashDuration);

                if (s.bgImage != null) {
                    s.bgImage.color = Color.Lerp(s.baseColor, Color.white, t * 0.5f);
                }

                if (s.amountText != null) {
                    s.amountText.color = Color.Lerp(s.flashColor, Color.white, 1f - t);
                }

                if (s.flashTimer <= 0f) {
                    if (s.bgImage != null) {
                        s.bgImage.color = s.baseColor;
                    }

                    if (s.amountText != null) {
                        s.amountText.color = Color.white;
                    }
                }
            }
        }

        // Подписки

        private void Subscribe()
        {
            if (ResourceManager.Instance == null) {
                Debug.LogError("[ResourcePanelUI] ResourceManager.Instance == null — панель не будет обновляться!");
                return;
            }

            ResourceManager.Instance.OnResourceChanged += OnResourceChanged;
        }

        private void Unsubscribe()
        {
            if (ResourceManager.Instance == null) return;

            ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;
        }

        private void OnResourceChanged(ResourceDataSO res, int oldAmt, int newAmt)
        {
            if (res == null) return;

            if (!_slotsById.TryGetValue(res.Id, out var slot)) {
                CreateSlot(res);
                ResizePanel();
                ForceLayoutNow();
                return;
            }

            if (slot.amountText != null) {
                slot.amountText.text = $"{slot.shortName} {newAmt}";
            }

            slot.flashTimer = flashDuration;
            slot.flashColor = newAmt > oldAmt ? gainFlash : lossFlash;
        }

        // Построение

        private void BuildUI()
        {
            var canvasGo = new GameObject("ResourcePanelCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 60;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _panel = new GameObject("TopPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvas.transform, false);
            _panelRect = _panel.GetComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(0, 1);
            _panelRect.anchorMax = new Vector2(0, 1);
            _panelRect.pivot = new Vector2(0, 1);
            _panelRect.sizeDelta = new Vector2(100, panelHeight);
            _panelRect.anchoredPosition = new Vector2(10, -10);

            _panel.GetComponent<Image>().color = panelBg;
            _panel.GetComponent<Image>().raycastTarget = true;

            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(_panel.transform, false);
            _rowRect = rowGo.GetComponent<RectTransform>();
            _rowRect.anchorMin = Vector2.zero;
            _rowRect.anchorMax = Vector2.one;
            _rowRect.offsetMin = panelPadding;
            _rowRect.offsetMax = -panelPadding;

            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = slotSpacing;
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(0, 0, 0, 0);
        }

        private void PopulateFromGameData()
        {
            var gd = GameDataSO.Instance;

            if (gd == null || gd.Resources == null) return;

            foreach (var res in gd.Resources) {
                if (res == null) continue;
                CreateSlot(res);
            }

            ResizePanel();
            ForceLayoutNow();
        }

        private void CreateSlot(ResourceDataSO res)
        {
            if (res == null || _slotsById.ContainsKey(res.Id)) return;

            var slotGo = new GameObject($"Slot_{res.Id}", typeof(RectTransform), typeof(Image));
            slotGo.transform.SetParent(_rowRect, false);

            var slotRT = slotGo.GetComponent<RectTransform>();
            float slotHeight = panelHeight - panelPadding.y * 2;
            slotRT.sizeDelta = new Vector2(slotWidth, slotHeight);
            slotRT.anchorMin = new Vector2(0, 0.5f);
            slotRT.anchorMax = new Vector2(0, 0.5f);
            slotRT.pivot = new Vector2(0, 0.5f);
            slotRT.anchoredPosition = Vector2.zero;

            var le = slotGo.AddComponent<LayoutElement>();
            le.preferredWidth = slotWidth;
            le.preferredHeight = slotHeight;
            le.minWidth = slotWidth;
            le.flexibleWidth = 0;

            var bg = slotGo.GetComponent<Image>();
            bg.color = slotBg;
            bg.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(slotGo.transform, false);

            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.sizeDelta = new Vector2(24, 24);
            iconRect.anchoredPosition = new Vector2(6, 0);

            var iconImg = iconGo.GetComponent<Image>();
            iconImg.color = res.UiColor;
            iconImg.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(slotGo.transform, false);

            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(34, 0);
            labelRect.offsetMax = new Vector2(-6, 0);

            string shortName = ShortName(res.DisplayName);

            var label = labelGo.GetComponent<Text>();
            label.text = $"{shortName} {ResourceManager.Instance?.GetAmount(res) ?? 0}";
            label.font = _font;
            label.fontSize = 15;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            var slot = new Slot {
                resource = res,
                shortName = shortName,
                amountText = label,
                bgImage = bg,
                baseColor = slotBg,
                flashColor = Color.white,
                flashTimer = 0f
            };

            _slots.Add(slot);
            _slotsById[res.Id] = slot;
        }

        private void ResizePanel()
        {
            int count = _slots.Count;
            float width = panelPadding.x * 2 + count * slotWidth + Mathf.Max(0, count - 1) * slotSpacing;

            width = Mathf.Max(100, width);
            _panelRect.sizeDelta = new Vector2(width, panelHeight);
        }

        private void ForceLayoutNow()
        {
            if (_rowRect == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rowRect);
        }

        private static string ShortName(string full)
        {
            if (string.IsNullOrEmpty(full)) return "?";
            if (full.Length <= 8) return full;
            return full.Substring(0, 7) + "…";
        }

        private Font LoadFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (f == null) {
                f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            
            if (f == null) {
                f = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }

            return f;
        }
    }
}