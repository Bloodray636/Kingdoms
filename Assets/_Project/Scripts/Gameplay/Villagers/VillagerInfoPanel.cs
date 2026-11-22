using UnityEngine;
using UnityEngine.UI;

namespace KingdomsClone.Gameplay
{
    public class VillagerInfoPanel : MonoBehaviour
    {
        private Villager _target;
        private Canvas _canvas;
        private GameObject _panel;
        private Text _nameText;
        private Text _stateText;
        private Text _jobText;
        private Text _homeText;
        private Text _hungerText;
        private Text _happinessText;
        private Font _font;
        private float _updateTimer;
        private const float UpdateInterval = 0.2f;

        private void Awake()
        {
            _font = LoadFont();
            BuildUI();
            _panel.SetActive(false);
        }

        public void Show(Villager v)
        {
            _target = v;

            if (_target == null) { 
                Hide(); 
                return; 
            }

            _panel.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            _target = null;

            if (_panel != null) {
                _panel.SetActive(false);
            } 
        }

        private void Update()
        {
            if (_target == null || _panel == null || !_panel.activeSelf) return;

            if (_target.gameObject == null || !_target.gameObject.activeInHierarchy) { 
                Hide();
                return; 
            }

            _updateTimer += Time.deltaTime;

            if (_updateTimer >= UpdateInterval) { 
                _updateTimer = 0f; 
                Refresh(); 
            }
        }

        private void Refresh()
        {
            if (_target == null) return;

            _nameText.text = _target.VillagerName ?? "Безымянный";
            _stateText.text = $"Состояние: {StateRu(_target.State)}";

            if (_target.CurrentJob != null) {
                _jobText.text = $"Работа: {JobRu(_target.CurrentJob.Type)} ({_target.CurrentJob.WorkplaceCell})";
            } else {
                _jobText.text = "Работа: безработный";
            }

            if (_target.HomeBuilding != null && _target.HomeBuilding.Data != null) {
                _homeText.text = $"Дом: {_target.HomeBuilding.Data.DisplayName} ({_target.HomeBuilding.GridPosition})";
            } else {
                _homeText.text = "Дом: нет";
            }

            int hunger = _target.HungerLevel;
            _hungerText.text = $"Голод: {hunger}/3 {(hunger >= 2 ? "⚠ Голодает" : "")}";
            _hungerText.color = hunger >= 2 ? Color.red : (hunger >= 1 ? Color.yellow : Color.white);

            float h = _target.Happiness;
            _happinessText.text = $"Счастье: {h:F1}/10";
            _happinessText.color = h < 3f ? Color.red : (h < 6f ? Color.yellow : Color.green);
        }

        private static string StateRu(VillagerState s) => s switch
        {
            VillagerState.Idle => "Отдыхает",
            VillagerState.Move => "Идёт",
            VillagerState.Work => "Работает",
            VillagerState.Eat => "Ест",
            VillagerState.Sleep => "Спит",
            VillagerState.Flee => "Бежит",
            _ => s.ToString()
        };

        private static string JobRu(JobType j) => j switch
        {
            JobType.Lumberjack => "Лесоруб",
            JobType.Farmer => "Фермер",
            JobType.StoneMiner => "Каменотёс",
            JobType.Fisherman => "Рыбак",
            JobType.Treasurer => "Казначей",
            JobType.Miner => "Рудокоп",       
            JobType.Smelter => "Плавильщик",    
            JobType.Blacksmith => "Кузнец",        
            JobType.Builder => "Строитель",
            JobType.Guard => "Стражник",
            _ => "—"
        };

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

        private void BuildUI()
        {
            var canvasGo = new GameObject("VillagerInfoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 55;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvas.transform, false);

            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(360, 320);
            rect.anchoredPosition = new Vector2(-20, 260);
            _panel.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.11f, 0.96f);

            // Close
            var closeGo = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(_panel.transform, false);

            var cRect = closeGo.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(1, 1);
            cRect.anchorMax = new Vector2(1, 1);
            cRect.pivot = new Vector2(1, 1);
            cRect.sizeDelta = new Vector2(36, 36);
            cRect.anchoredPosition = new Vector2(-6, -6);
            closeGo.GetComponent<Image>().color = new Color(0.5f, 0.15f, 0.15f, 1f);

            var cTextGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            cTextGo.transform.SetParent(closeGo.transform, false);

            var ctRect = cTextGo.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero; ctRect.anchorMax = Vector2.one;
            ctRect.offsetMin = Vector2.zero; ctRect.offsetMax = Vector2.zero;

            var cText = cTextGo.GetComponent<Text>();
            cText.text = "✖"; cText.font = _font; cText.fontSize = 20;
            cText.alignment = TextAnchor.MiddleCenter; cText.color = Color.white;
            cText.raycastTarget = false;
            closeGo.GetComponent<Button>().onClick.AddListener(Hide);

            _nameText = CreateLabel(_panel.transform, new Vector2(12, -12), 300, 32, 22, FontStyle.Bold);
            _stateText = CreateLabel(_panel.transform, new Vector2(12, -50), 320, 24, 16, FontStyle.Normal);
            _jobText = CreateLabel(_panel.transform, new Vector2(12, -78), 320, 24, 16, FontStyle.Normal);
            _homeText = CreateLabel(_panel.transform, new Vector2(12, -106), 320, 24, 16, FontStyle.Normal);
            _hungerText = CreateLabel(_panel.transform, new Vector2(12, -140), 320, 24, 16, FontStyle.Normal);
            _happinessText = CreateLabel(_panel.transform, new Vector2(12, -168), 320, 24, 16, FontStyle.Normal);
        }

        private Text CreateLabel(Transform parent, Vector2 pos, float w, float h, int size, FontStyle style)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = pos;

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }
    }
}