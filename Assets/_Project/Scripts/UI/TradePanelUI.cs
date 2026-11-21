using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using KingdomsClone.Core;
using KingdomsClone.Data;

namespace KingdomsClone.UI
{
    /// <summary>
    /// ЗАГЛУШКА под Epic 11 (торговля).
    /// Показывает панель с инвентарём игрока и кнопками "Скоро".
    /// 
    /// В Epic 11: заменить "Скоро" на:
    /// - список торговых предложений (продать/купить)
    /// - караваны (прибытие/отправка)
    /// - отношения с AI-королевствами
    /// - цены, зависящие от репутации
    /// </summary>
    public class TradePanelUI : MonoBehaviour
    {
        [Header("Hotkey")]
        [SerializeField] private Key toggleKey = Key.Y;

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(560f, 420f);

        [Header("Colors")]
        [SerializeField] private Color panelBg = new Color(0.07f, 0.08f, 0.11f, 0.97f);
        [SerializeField] private Color headerBg = new Color(0.12f, 0.16f, 0.22f, 1f);
        [SerializeField] private Color rowBg = new Color(0.13f, 0.15f, 0.19f, 1f);
        [SerializeField] private Color disabledBtn = new Color(0.25f, 0.25f, 0.28f, 1f);
        [SerializeField] private Color accent = new Color(0.25f, 0.55f, 0.75f, 1f);

        private Font _font;
        private Canvas _canvas;
        private GameObject _dim;
        private GameObject _panel;
        private RectTransform _resourceListRect;
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            _font = LoadFont();
            BuildUI();
            _panel.SetActive(false);
            if (_dim != null) _dim.SetActive(false);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            bool togglePressed = false;
            var toggleCtrl = kb[toggleKey];
            if (toggleCtrl != null)
                togglePressed = toggleCtrl.wasPressedThisFrame;

            if (togglePressed)
            {
                if (!_isOpen && KingdomsClone.Core.DebugTools.DebugMenu.IsOpen) return;
                Toggle();
            }

            if (_isOpen && kb.escapeKey.wasPressedThisFrame)
                Close();
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            _isOpen = true;
            _panel.SetActive(true);
            if (_dim != null) _dim.SetActive(true);
            RefreshResourceList();
        }

        public void Close()
        {
            _isOpen = false;
            _panel.SetActive(false);
            if (_dim != null) _dim.SetActive(false);
        }

        // ═══════════════════ ПОСТРОЕНИЕ ═══════════════════

        private void BuildUI()
        {
            var canvasGo = new GameObject("TradePanelCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 62;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // ═══════ ЗАТЕМНЕНИЕ ФОНА ═══════
            _dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            _dim.transform.SetParent(_canvas.transform, false);
            var dimRect = _dim.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            var dimImg = _dim.GetComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.55f);
            dimImg.raycastTarget = true;
            _dim.GetComponent<Button>().onClick.AddListener(Close);

            // ═══════ ОСНОВНАЯ ПАНЕЛЬ ═══════
            _panel = new GameObject("TradePanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvas.transform, false);
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = panelSize;
            panelRect.anchoredPosition = Vector2.zero;
            _panel.GetComponent<Image>().color = panelBg;

            BuildHeader(panelRect);
            BuildResourceList(panelRect);
            BuildFooter(panelRect);
        }

        private void BuildHeader(RectTransform panelRect)
        {
            var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(_panel.transform, false);
            var hRect = header.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0, 1);
            hRect.anchorMax = new Vector2(1, 1);
            hRect.pivot = new Vector2(0.5f, 1);
            hRect.sizeDelta = new Vector2(0, 50);
            hRect.anchoredPosition = Vector2.zero;
            header.GetComponent<Image>().color = headerBg;

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(header.transform, false);
            var tRect = titleGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(16, 0);
            tRect.offsetMax = new Vector2(-56, 0);

            var title = titleGo.GetComponent<Text>();
            title.text = "💰 Торговля (недоступна)";
            title.font = _font;
            title.fontSize = 22;
            title.fontStyle = FontStyle.Bold;
            title.color = Color.white;
            title.alignment = TextAnchor.MiddleLeft;
            title.raycastTarget = false;

            // Close button
            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(header.transform, false);
            var cRect = closeGo.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(1, 0.5f);
            cRect.anchorMax = new Vector2(1, 0.5f);
            cRect.pivot = new Vector2(1, 0.5f);
            cRect.sizeDelta = new Vector2(36, 36);
            cRect.anchoredPosition = new Vector2(-8, 0);
            closeGo.GetComponent<Image>().color = new Color(0.5f, 0.15f, 0.15f, 1f);

            var cTextGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            cTextGo.transform.SetParent(closeGo.transform, false);
            var ctRect = cTextGo.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.offsetMin = Vector2.zero;
            ctRect.offsetMax = Vector2.zero;
            var cText = cTextGo.GetComponent<Text>();
            cText.text = "✖";
            cText.font = _font;
            cText.fontSize = 20;
            cText.color = Color.white;
            cText.alignment = TextAnchor.MiddleCenter;
            cText.raycastTarget = false;

            closeGo.GetComponent<Button>().onClick.AddListener(Close);
        }

        private void BuildResourceList(RectTransform panelRect)
        {
            var lblGo = new GameObject("ListLabel", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(_panel.transform, false);
            var lRect = lblGo.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0, 1);
            lRect.anchorMax = new Vector2(1, 1);
            lRect.pivot = new Vector2(0, 1);
            lRect.sizeDelta = new Vector2(-32, 28);
            lRect.anchoredPosition = new Vector2(16, -60);

            var lbl = lblGo.GetComponent<Text>();
            lbl.text = "Ваши ресурсы (пока только для справки):";
            lbl.font = _font;
            lbl.fontSize = 15;
            lbl.color = new Color(0.7f, 0.75f, 0.85f);
            lbl.alignment = TextAnchor.MiddleLeft;
            lbl.raycastTarget = false;

            var listGo = new GameObject("ResourceList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(_panel.transform, false);
            _resourceListRect = listGo.GetComponent<RectTransform>();
            _resourceListRect.anchorMin = new Vector2(0, 0);
            _resourceListRect.anchorMax = new Vector2(1, 1);
            _resourceListRect.offsetMin = new Vector2(16, 130);
            _resourceListRect.offsetMax = new Vector2(-16, -95);

            var vlg = listGo.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.spacing = 4;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
        }

        private void BuildFooter(RectTransform panelRect)
        {
            var sep = new GameObject("Separator", typeof(RectTransform), typeof(Image));
            sep.transform.SetParent(_panel.transform, false);
            var sRect = sep.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0, 0);
            sRect.anchorMax = new Vector2(1, 0);
            sRect.pivot = new Vector2(0.5f, 0);
            sRect.sizeDelta = new Vector2(-32, 1);
            sRect.anchoredPosition = new Vector2(0, 115);
            sep.GetComponent<Image>().color = new Color(1, 1, 1, 0.1f);

            var infoGo = new GameObject("Info", typeof(RectTransform), typeof(Text));
            infoGo.transform.SetParent(_panel.transform, false);
            var iRect = infoGo.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0, 0);
            iRect.anchorMax = new Vector2(1, 0);
            iRect.pivot = new Vector2(0, 0);
            iRect.sizeDelta = new Vector2(-32, 60);
            iRect.anchoredPosition = new Vector2(16, 50);

            var info = infoGo.GetComponent<Text>();
            info.text = "<b>Торговля будет доступна в ближайших версиях.</b>\n" +
                        "Караваны, AI-королевства, репутация — всё в разработке.";
            info.font = _font;
            info.fontSize = 14;
            info.color = new Color(0.65f, 0.7f, 0.8f);
            info.alignment = TextAnchor.UpperLeft;
            info.raycastTarget = false;
            info.supportRichText = true;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;

            var btnRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            btnRow.transform.SetParent(_panel.transform, false);
            var bRect = btnRow.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0, 0);
            bRect.anchorMax = new Vector2(1, 0);
            bRect.pivot = new Vector2(0.5f, 0);
            bRect.sizeDelta = new Vector2(-32, 40);
            bRect.anchoredPosition = new Vector2(0, 8);

            var hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            CreateDisabledButton(btnRow.transform, "Продать (скоро)");
            CreateDisabledButton(btnRow.transform, "Купить (скоро)");
            CreateDisabledButton(btnRow.transform, "Караван (скоро)");
        }

        private void CreateDisabledButton(Transform parent, string label)
        {
            var btnGo = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image));
            btnGo.transform.SetParent(parent, false);

            var le = btnGo.AddComponent<LayoutElement>();
            le.preferredWidth = 150;
            le.preferredHeight = 40;

            btnGo.GetComponent<Image>().color = disabledBtn;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(btnGo.transform, false);
            var tRect = textGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.font = _font;
            text.fontSize = 14;
            text.color = new Color(0.6f, 0.6f, 0.65f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }

        // ═══════════════════ ОБНОВЛЕНИЕ ═══════════════════

        private void RefreshResourceList()
        {
            foreach (Transform child in _resourceListRect)
                Destroy(child.gameObject);

            var rm = ResourceManager.Instance;
            var gd = GameDataSO.Instance;
            if (rm == null || gd == null) return;

            foreach (var res in gd.Resources)
            {
                if (res == null) continue;
                int amount = rm.GetAmount(res);
                CreateResourceRow(res, amount);
            }
        }

        private void CreateResourceRow(ResourceDataSO res, int amount)
        {
            var rowGo = new GameObject($"Row_{res.Id}", typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(_resourceListRect, false);

            var le = rowGo.AddComponent<LayoutElement>();
            le.preferredHeight = 28;
            le.minHeight = 28;

            rowGo.GetComponent<Image>().color = rowBg;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rowGo.transform, false);
            var iRect = iconGo.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0, 0.5f);
            iRect.anchorMax = new Vector2(0, 0.5f);
            iRect.pivot = new Vector2(0, 0.5f);
            iRect.sizeDelta = new Vector2(20, 20);
            iRect.anchoredPosition = new Vector2(6, 0);
            iconGo.GetComponent<Image>().color = res.UiColor;
            iconGo.GetComponent<Image>().raycastTarget = false;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(rowGo.transform, false);
            var tRect = textGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(32, 0);
            tRect.offsetMax = new Vector2(-8, 0);

            var text = textGo.GetComponent<Text>();
            text.text = $"{res.DisplayName}: <b>{amount}</b>";
            text.font = _font;
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.supportRichText = true;
        }

        private Font LoadFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return f;
        }
    }
}