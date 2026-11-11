using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using KingdomsClone.Data;
using KingdomsClone.Core;
using KingdomsClone.Gameplay;

namespace KingdomsClone.UI
{
    public class BuildingUIManager : MonoBehaviour
    {
        private static BuildingUIManager _instance;

        [Header("References")]
        [SerializeField] private BuildingPlacer placer;

        [Header("Layout")]
        [SerializeField] private float panelHeight = 220f;
        [SerializeField] private float categoryBarHeight = 42f;
        [SerializeField] private float buildingButtonSize = 110f;
        [SerializeField] private float spacing = 8f;

        private Font _font;
        private Canvas _canvas;
        private GameObject _panel;
        private RectTransform _buildingGridRect;
        private GameObject _tooltipPanel;
        private RectTransform _tooltipRect;
        private Text _tooltipText;
        private GameObject _toggleBtnGo;

        private BuildingCategory _currentCategory = BuildingCategory.Housing;
        private readonly Dictionary<BuildingCategory, Image> _categoryImages = new();
        private readonly List<GameObject> _buildingButtons = new();
        private bool _panelOpen = true;

        private static readonly BuildingCategory[] _categories =
        {
            BuildingCategory.Housing,
            BuildingCategory.Production,
            BuildingCategory.Happiness,
            BuildingCategory.Defense,
            BuildingCategory.Storage,
        };

        private static readonly Dictionary<BuildingCategory, string> _categoryLabels = new()
        {
            { BuildingCategory.Housing,    "🏠 Жильё" },
            { BuildingCategory.Production, "⚒ Производство" },
            { BuildingCategory.Happiness,  "☺ Счастье" },
            { BuildingCategory.Defense,    "🛡 Оборона" },
            { BuildingCategory.Storage,    "📦 Склады" },
        };

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[BuildingUIManager] Дубликат обнаружен! Уничтожаю лишний.");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (placer == null) placer = FindObjectOfType<BuildingPlacer>();
            if (placer == null) Debug.LogError("[BuildingUIManager] BuildingPlacer НЕ НАЙДЕН!");
            else Debug.Log("[BuildingUIManager] placer найден: " + placer.name);

            _font = LoadFont();
            EnsureEventSystem();
            BuildUI();

            Debug.Log($"[BuildingUIManager] UI построен. Panel={_panel.name}, Toggle={_toggleBtnGo.name}");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame)
            {
                TogglePanel();
            }
        }

        private static Font LoadFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return f;
        }

        private void EnsureEventSystem()
        {
            var existing = FindObjectOfType<EventSystem>();
            if (existing != null)
            {
                Debug.Log($"[BuildingUIManager] EventSystem уже есть: {existing.name}");
                return;
            }

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Debug.Log("[BuildingUIManager] Создан EventSystem с InputSystemUIInputModule");
        }

        // =====================  ПОСТРОЕНИЕ UI  =====================

        private void BuildUI()
        {
            var canvasGo = new GameObject("BuildingUICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // ---------- Основная панель (снизу) ----------
            _panel = new GameObject("BottomPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvas.transform, false);
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(1, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.sizeDelta = new Vector2(0, panelHeight);
            panelRect.anchoredPosition = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.11f, 0.96f);

            // ---------- Вкладки категорий ----------
            var catBar = new GameObject("CategoryBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            catBar.transform.SetParent(_panel.transform, false);
            var catRect = catBar.GetComponent<RectTransform>();
            catRect.anchorMin = new Vector2(0, 1);
            catRect.anchorMax = new Vector2(1, 1);
            catRect.pivot = new Vector2(0.5f, 1);
            catRect.sizeDelta = new Vector2(-20, categoryBarHeight);
            catRect.anchoredPosition = new Vector2(0, -6);

            var catHlg = catBar.GetComponent<HorizontalLayoutGroup>();
            catHlg.childAlignment = TextAnchor.MiddleLeft;
            catHlg.spacing = 6;
            catHlg.padding = new RectOffset(10, 10, 0, 0);
            catHlg.childForceExpandWidth = false;
            catHlg.childForceExpandHeight = true;
            catHlg.childControlWidth = true;
            catHlg.childControlHeight = true;

            foreach (var cat in _categories)
                CreateCategoryButton(catBar.transform, cat);

            // ---------- Сетка кнопок зданий ----------
            var gridGo = new GameObject("BuildingGrid", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            gridGo.transform.SetParent(_panel.transform, false);
            _buildingGridRect = gridGo.GetComponent<RectTransform>();
            _buildingGridRect.anchorMin = new Vector2(0, 0);
            _buildingGridRect.anchorMax = new Vector2(1, 0);
            _buildingGridRect.pivot = new Vector2(0.5f, 0);
            _buildingGridRect.sizeDelta = new Vector2(-20, panelHeight - categoryBarHeight - 16);
            _buildingGridRect.anchoredPosition = new Vector2(0, 6);

            var hlg = gridGo.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = spacing;
            hlg.padding = new RectOffset(10, 10, 5, 5);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            BuildTooltip();
            BuildToggleButton();
            BuildCloseButton();          
            RefreshCategory(_currentCategory);
        }

        private void CreateCategoryButton(Transform parent, BuildingCategory cat)
        {
            var btnGo = new GameObject($"Cat_{cat}", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);

            var img = btnGo.GetComponent<Image>();
            img.color = cat == _currentCategory
                ? new Color(0.25f, 0.45f, 0.7f, 1f)
                : new Color(0.15f, 0.17f, 0.22f, 1f);
            _categoryImages[cat] = img;

            var le = btnGo.AddComponent<LayoutElement>();
            le.preferredWidth = 170;
            le.preferredHeight = categoryBarHeight - 8;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(btnGo.transform, false);
            var tRect = textGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.text = _categoryLabels[cat];
            text.font = _font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            btnGo.GetComponent<Button>().onClick.AddListener(() => SelectCategory(cat));
        }

        private void BuildTooltip()
        {
            _tooltipPanel = new GameObject("Tooltip", typeof(RectTransform), typeof(Image));
            _tooltipPanel.transform.SetParent(_canvas.transform, false);
            _tooltipRect = _tooltipPanel.GetComponent<RectTransform>();
            _tooltipRect.sizeDelta = new Vector2(340, 240);
            _tooltipRect.pivot = new Vector2(0.5f, 0);
            _tooltipPanel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.98f);

            var textGo = new GameObject("TooltipText", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(_tooltipPanel.transform, false);
            var tRect = textGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(12, 12);
            tRect.offsetMax = new Vector2(-12, -12);

            _tooltipText = textGo.GetComponent<Text>();
            _tooltipText.font = _font;
            _tooltipText.fontSize = 15;
            _tooltipText.color = new Color(0.9f, 0.92f, 0.95f);
            _tooltipText.raycastTarget = false;
            _tooltipText.supportRichText = true;
            _tooltipText.alignment = TextAnchor.UpperLeft;

            _tooltipPanel.SetActive(false);
        }

        private void BuildToggleButton()
        {
            _toggleBtnGo = new GameObject("TogglePanelBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            _toggleBtnGo.transform.SetParent(_canvas.transform, false);
            var rect = _toggleBtnGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.sizeDelta = new Vector2(180, 56);
            rect.anchoredPosition = new Vector2(20, 20);

            _toggleBtnGo.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.7f, 1f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(_toggleBtnGo.transform, false);
            var tRect = textGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var t = textGo.GetComponent<Text>();
            t.text = "🔨 Открыть меню (B)";
            t.font = _font;
            t.fontSize = 18;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;

            _toggleBtnGo.GetComponent<Button>().onClick.AddListener(TogglePanel);
            _toggleBtnGo.SetActive(false);
        }

        private void BuildCloseButton()
        {
            var btnGo = new GameObject("ClosePanelBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(_panel.transform, false);

            var rect = btnGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(40, 40);
            rect.anchoredPosition = new Vector2(-8, -8);

            btnGo.GetComponent<Image>().color = new Color(0.5f, 0.15f, 0.15f, 1f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(btnGo.transform, false);
            var tRect = textGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var t = textGo.GetComponent<Text>();
            t.text = "✖";
            t.font = _font;
            t.fontSize = 22;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;

            btnGo.GetComponent<Button>().onClick.AddListener(() => SetPanelOpen(false));
        }

        // =====================  ЛОГИКА  =====================

        private void SelectCategory(BuildingCategory cat)
        {
            _currentCategory = cat;
            foreach (var kvp in _categoryImages)
                kvp.Value.color = kvp.Key == cat
                    ? new Color(0.25f, 0.45f, 0.7f, 1f)
                    : new Color(0.15f, 0.17f, 0.22f, 1f);
            RefreshCategory(cat);
        }

        private void RefreshCategory(BuildingCategory cat)
        {
            foreach (var b in _buildingButtons) if (b != null) Destroy(b);
            _buildingButtons.Clear();

            var data = GameDataSO.Instance;
            if (data == null) return;

            int count = 0;
            foreach (var building in data.Buildings)
            {
                if (building == null || building.Category != cat) continue;
                CreateBuildingButton(building);
                count++;
            }

            if (count == 0)
            {
                var emptyGo = new GameObject("Empty", typeof(RectTransform), typeof(Text));
                emptyGo.transform.SetParent(_buildingGridRect, false);
                var t = emptyGo.GetComponent<Text>();
                t.text = "Нет зданий в этой категории";
                t.font = _font;
                t.fontSize = 18;
                t.color = new Color(0.5f, 0.5f, 0.55f);
                t.alignment = TextAnchor.MiddleLeft;
                _buildingButtons.Add(emptyGo);
            }
        }

        private void CreateBuildingButton(BuildingDataSO building)
        {
            var btnGo = new GameObject($"Build_{building.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(_buildingGridRect, false);

            var le = btnGo.AddComponent<LayoutElement>();
            le.preferredWidth = buildingButtonSize;
            le.preferredHeight = buildingButtonSize;

            var img = btnGo.GetComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.3f, 1f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(btnGo.transform, false);
            var tRect = textGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(4, 4);
            tRect.offsetMax = new Vector2(-4, -4);

            var t = textGo.GetComponent<Text>();
            t.text = building.DisplayName;
            t.font = _font;
            t.fontSize = 14;
            t.color = Color.white;
            t.alignment = TextAnchor.LowerCenter;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;

            btnGo.GetComponent<Button>().onClick.AddListener(() => OnBuildingClicked(building));

            var hover = btnGo.AddComponent<BuildingHoverHandler>();
            hover.OnEnter = rect => ShowTooltip(building, rect);
            hover.OnExit = HideTooltip;

            _buildingButtons.Add(btnGo);
        }

        private void OnBuildingClicked(BuildingDataSO building)
        {
            Debug.Log($"[BuildingUIManager] КЛИК по кнопке: {building?.DisplayName}");

            if (building == null) return;

            SetPanelOpen(false);
            HideTooltip();

            if (placer == null)
            {
                Debug.LogError("[BuildingUIManager] placer == null! Призрак НЕ появится.");
                return;
            }

            placer.StartPlacement(building);
            Debug.Log("[BuildingUIManager] StartPlacement вызван.");
        }

        private void TogglePanel()
        {
            Debug.Log($"[BuildingUIManager] TogglePanel из {_panelOpen}");
            SetPanelOpen(!_panelOpen);
        }

        private void SetPanelOpen(bool open)
        {
            Debug.Log($"[BuildingUIManager] SetPanelOpen({open})");
            _panelOpen = open;

            if (_panel != null)
            {
                _panel.SetActive(open);
                Debug.Log($"[BuildingUIManager] _panel.activeSelf = {_panel.activeSelf}");
            }
            else Debug.LogError("[BuildingUIManager] _panel == null!");

            if (_toggleBtnGo != null)
            {
                _toggleBtnGo.SetActive(!open);
                Debug.Log($"[BuildingUIManager] _toggleBtnGo.activeSelf = {_toggleBtnGo.activeSelf}");
            }
            else Debug.LogError("[BuildingUIManager] _toggleBtnGo == null!");

            HideTooltip();
        }

        // =====================  ТУЛТИП  =====================

        private void ShowTooltip(BuildingDataSO building, RectTransform anchor)
        {
            if (building == null || _tooltipPanel == null) return;

            var rm = ResourceManager.Instance;
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"<b>{building.DisplayName}</b>");
            if (!string.IsNullOrEmpty(building.Description))
            {
                sb.AppendLine($"<i>{building.Description}</i>");
                sb.AppendLine();
            }

            sb.AppendLine("<b>Стоимость:</b>");
            if (building.BuildCost != null && building.BuildCost.Count > 0)
            {
                foreach (var ra in building.BuildCost)
                {
                    if (ra == null || ra.Resource == null) continue;
                    int have = rm != null ? rm.GetAmount(ra.Resource) : 0;
                    bool enough = have >= ra.Amount;
                    string color = enough ? "#8FFF8F" : "#FF8888";
                    sb.AppendLine($"  <color={color}>{ra.Resource.DisplayName}: {ra.Amount} (есть {have})</color>");
                }
            }
            else sb.AppendLine("  (бесплатно)");

            sb.AppendLine();
            sb.AppendLine($"<b>HP:</b> {building.MaxHealth}");
            sb.AppendLine($"<b>Размер:</b> {building.Size.x}×{building.Size.y}");
            if (building.WorkerSlots > 0) sb.AppendLine($"<b>Рабочих мест:</b> {building.WorkerSlots}");

            _tooltipText.text = sb.ToString();

            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, corners[1]);

            float tooltipW = _tooltipRect.sizeDelta.x;
            float tooltipH = _tooltipRect.sizeDelta.y;
            
            float x = screenPos.x + (anchor.rect.width / 2f);
            float y = screenPos.y + 10;

            x = Mathf.Clamp(x, tooltipW / 2f + 10, Screen.width - tooltipW / 2f - 10);
            y = Mathf.Min(y, Screen.height - tooltipH - 10);

            _tooltipRect.position = new Vector3(x, y, 0);
            _tooltipPanel.SetActive(true);
            _tooltipPanel.transform.SetAsLastSibling();
        }

        private void HideTooltip()
        {
            if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
        }
    }
}