using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace GWYF_CasinoChaos
{
    internal static class StaticEyeOverlay
    {
        private static readonly AccessTools.FieldRef<PlayerOrgans, bool> LeftEye = AccessTools.FieldRefAccess<PlayerOrgans, bool>("_localLeftEye");
        private static readonly AccessTools.FieldRef<PlayerOrgans, bool> RightEye = AccessTools.FieldRefAccess<PlayerOrgans, bool>("_localRightEye");
        private static readonly System.Reflection.FieldInfo VignetteField = AccessTools.Field(typeof(PlayerEyesUI), "_vignette");
        private static readonly System.Reflection.MethodInfo UpdateVanilla = AccessTools.Method(typeof(PlayerEyesUI), "UpdateVignette");
        private static readonly Dictionary<PlayerEyesUI, (object Vignette, bool Active)> Suppressed = new Dictionary<PlayerEyesUI, (object, bool)>();
        private static GameObject _root;
        private static Image _left, _right, _full;
        private static float _nextScan;
        internal static bool Installed { get; private set; }
        internal static void Install() { Installed = true; _nextScan = 0; WorldEyeEffect.Install(); }
        internal static void Suppress(PlayerEyesUI eyes)
        {
            if (!Installed || !eyes) return;
            var vignette = VignetteField.GetValue(eyes);
            if (vignette == null) return;
            var active = AccessTools.Field(vignette.GetType(), "active");
            if (!Suppressed.ContainsKey(eyes)) Suppressed.Add(eyes, (vignette, (bool)active.GetValue(vignette)));
            active.SetValue(vignette, false);
        }
        internal static void Tick()
        {
            if (!Installed) return;
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + .5f;
                foreach (var eyes in Object.FindObjectsByType<PlayerEyesUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Suppress(eyes);
                var dead = new List<PlayerEyesUI>();
                foreach (var entry in Suppressed) if (!entry.Key) dead.Add(entry.Key);
                foreach (var key in dead) Suppressed.Remove(key);
            }
            var organs = NetworkClient.active && NetworkClient.localPlayer ? NetworkClient.localPlayer.GetComponent<PlayerOrgans>() : null;
            bool missingLeft = organs && !LeftEye(organs), missingRight = organs && !RightEye(organs);
            WorldEyeEffect.SetState(missingLeft, missingRight);
            if (VisionTuning.Mode != VisionMode.HardOverlay) { if (_root) _root.SetActive(false); return; }
            if (!missingLeft && !missingRight) { if (_root) _root.SetActive(false); return; }
            if (!_root) Create();
            _root.SetActive(true);
            bool both = missingLeft && missingRight;
            _left.gameObject.SetActive(missingLeft && !both); _right.gameObject.SetActive(missingRight && !both);
            _full.gameObject.SetActive(both);
            _left.color = _right.color = new Color(0, 0, 0, AccessibilitySettings.SingleEyeAlpha);
            _full.color = new Color(0, 0, 0, AccessibilitySettings.BlindAlpha);
        }
        private static void Create()
        {
            _root = new GameObject("CasinoChaos Static Eye Overlay", typeof(RectTransform), typeof(Canvas));
            Object.DontDestroyOnLoad(_root);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -100;
            // Normalized anchors cover precisely the requested halves at any resolution.
            _left = Panel("Missing LeftEye", 0, .5f);
            _right = Panel("Missing RightEye", .5f, 1);
            _full = Panel("Both Eyes Missing", 0, 1);
        }
        private static Image Panel(string name, float min, float max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_root.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(min, 0); rect.anchorMax = new Vector2(max, 1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.raycastTarget = false; return image;
        }
        internal static void Shutdown()
        {
            Installed = false; WorldEyeEffect.Shutdown();
            if (_root) { _root.SetActive(false); Object.Destroy(_root); }
            _root = null; _left = _right = _full = null;
            // Plugin unpatches first. ToggleEye kept vanilla's internal eye flags current.
            foreach (var entry in Suppressed)
                if (entry.Key && entry.Value.Vignette != null)
                { AccessTools.Field(entry.Value.Vignette.GetType(), "active").SetValue(entry.Value.Vignette, entry.Value.Active); UpdateVanilla.Invoke(entry.Key, null); }
            Suppressed.Clear();
        }
    }
    [HarmonyPatch(typeof(PlayerEyesUI), "Awake")]
    internal static class SuppressEyeVignetteOnAwake
    { private static void Postfix(PlayerEyesUI __instance) => StaticEyeOverlay.Suppress(__instance); }
    [HarmonyPatch(typeof(PlayerEyesUI), "UpdateVignette")]
    internal static class ReplaceEyeVignette
    {
        private static bool Prefix(PlayerEyesUI __instance)
        { if (!StaticEyeOverlay.Installed) return true; StaticEyeOverlay.Suppress(__instance); return false; }
    }
}
