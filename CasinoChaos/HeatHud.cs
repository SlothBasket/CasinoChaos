using Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GWYF_CasinoChaos
{
    internal static class HeatHud
    {
        private static RectTransform _root, _timer;
        private static RectTransform[] _moneyLabels;
        private static TextMeshProUGUI _text;
        private static GameUI _ui;
        private static int _lastLevel;
        private static float _pulseUntil;
        private static readonly Vector3[] Corners = new Vector3[4];
        internal static void ResetFeedback() { _lastLevel = 0; _pulseUntil = 0; if (_text) _text.text = "HEAT  0 / 5"; }
        internal static void Tick()
        {
            var ui = NetworkSingleton<GameUI>.Instance;
            if (_ui != ui) Shutdown();
            if (!_root && ui) Create(ui);
            if (!_root) return;
            var game = NetworkSingleton<GameManager>.Instance;
            bool visible = game && (game.state == GameState.Game || game.state == GameState.Lobby) && Mirror.NetworkClient.localPlayer;
            _root.gameObject.SetActive(visible);
            if (!visible) return;
            int level = HeatNetwork.DisplayLevel;
            if (level != _lastLevel)
            {
                if (level > _lastLevel) _pulseUntil = Time.unscaledTime + .35f;
                else _pulseUntil = 0;
                _lastLevel = level;
                _text.text = $"HEAT  {level} / 5";
            }
            float remaining = _pulseUntil - Time.unscaledTime;
            float scale = remaining > 0 ? 1 + .04f * Mathf.Sin(Mathf.PI * remaining / .35f) : 1;
            _root.localScale = Vector3.one * scale;
            // Place immediately left of the actual right-hand day/money/quota
            // labels. This remains above the contributions list, away from center.
            if (_moneyLabels != null)
            {
                var canvas = (RectTransform)_root.parent;
                float left=canvas.rect.xMax, top=canvas.rect.yMin;
                foreach(var label in _moneyLabels)
                {
                    if(!label)continue;
                    label.GetWorldCorners(Corners);
                    left=Mathf.Min(left,canvas.InverseTransformPoint(Corners[0]).x);
                    top=Mathf.Max(top,canvas.InverseTransformPoint(Corners[1]).y);
                }
                _root.anchoredPosition=new Vector2(left-canvas.rect.xMax-16,top-canvas.rect.yMax);
            }
        }
        private static void Create(GameUI ui)
        {
            var template = ui.transform.Find("TimerUI/Day&Floor/Day")?.GetComponent<TextMeshProUGUI>();
            _timer = ui.transform.Find("TimerUI") as RectTransform;
            var panel = _timer ? _timer.GetComponent<Image>() : null;
            if (!template || !_timer || !panel) return;
            var money=ui.transform.Find("Quota&ContributionsUI/QuotaBalance&Day");
            if(!money)return;
            _moneyLabels=new[]{money.Find("Day") as RectTransform,money.Find("Balance") as RectTransform,money.Find("Quota") as RectTransform};
            _ui = ui;
            var go = new GameObject("CasinoChaosHeat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _root = go.GetComponent<RectTransform>(); _root.SetParent(ui.transform, false);
            _root.SetSiblingIndex(_timer.GetSiblingIndex() + 1);
            _root.anchorMin = _root.anchorMax = new Vector2(1, 1);
            _root.pivot = new Vector2(1, 1); _root.sizeDelta = new Vector2(144, 36);
            var background = go.GetComponent<Image>();
            background.sprite = panel.sprite; background.material = panel.material;
            background.type = panel.type; background.color = panel.color; background.raycastTarget = false;
            // Clone only the confirmed Day text object, retain TMP/font/material,
            // remove its layout driver so its new fixed rectangle stays stable.
            var label = Object.Instantiate(template.gameObject, _root, false);
            label.name = "HeatText";
            foreach (var component in label.GetComponents<MonoBehaviour>())
                if (!(component is TextMeshProUGUI)) Object.DestroyImmediate(component);
            _text = label.GetComponent<TextMeshProUGUI>();
            _text.raycastTarget = false; _text.alignment = TextAlignmentOptions.Center;
            _text.enableAutoSizing = true; _text.fontSizeMin = 18; _text.fontSizeMax = 24;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            var rect = _text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8, 3); rect.offsetMax = new Vector2(-8, -3);
            _lastLevel = HeatNetwork.DisplayLevel; _text.text = $"HEAT  {_lastLevel} / 5";
            _pulseUntil = 0;
            Canvas.ForceUpdateCanvases();
        }
        internal static void Shutdown()
        {
            if (_root) { _root.gameObject.SetActive(false); Object.Destroy(_root.gameObject); }
            _root = _timer = null; _moneyLabels=null; _text = null; _ui = null; ResetFeedback();
        }
    }
}
