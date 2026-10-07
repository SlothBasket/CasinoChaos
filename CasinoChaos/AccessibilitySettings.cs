using BepInEx.Configuration;

namespace GWYF_CasinoChaos
{
    internal static class AccessibilitySettings
    {
        internal static float SingleEyeAlpha = 0.98f, BlindAlpha = 0.97f;
        private static ConfigFile _config;
        private static ConfigEntry<VisionMode> _mode;
        private static ConfigEntry<float> _black, _blur, _reach, _feather, _bothBlack, _bothBlur, _center;
        private static ConfigEntry<float> _sideGain, _sideCutoff, _bothGain, _bothCutoff, _exponent,
            _mouthGain, _mouthCutoff, _distortion, _singleAlpha, _blindAlpha;
        internal static void Bind(ConfigFile config)
        {
            if (_config != null) return;
            _config = config;
            _sideGain = Float("Hearing", "MissingSideGain", .65f, 0, 1, "Gain directly on the missing-ear side.");
            _sideCutoff = Float("Hearing", "MissingSideCutoffHz", 750, 100, 22000, "Low-pass cutoff directly on the missing-ear side.");
            _bothGain = Float("Hearing", "BothMissingGain", .55f, 0, 1, "Gain with neither ear present.");
            _bothCutoff = Float("Hearing", "BothMissingCutoffHz", 550, 100, 22000, "Low-pass cutoff with neither ear present.");
            _exponent = Float("Hearing", "DirectionExponent", 1.25f, 1, 4, "Smooth side-dot curve for directional muffling.");
            _mouthGain = Float("Voice", "MouthlessGain", .60f, 0, 1, "Receiver gain replacing vanilla's narrow-band mouth EQ.");
            _mouthCutoff = Float("Voice", "MouthlessCutoffHz", 650, 100, 22000, "Mouthless speech low-pass cutoff.");
            _distortion = Float("Voice", "MouthlessDistortion", .15f, 0, 1, "Mild distortion for sealed-mouth speech.");
            _singleAlpha = Float("Vision", "SingleEyeAlpha", .98f, 0, 1, "Static missing-half opacity; 1 is solid black.");
            _blindAlpha = Float("Vision", "BothEyesAlpha", .97f, 0, 1, "Static full-screen opacity; 1 is solid black.");
            _mode = config.Bind("Vision", "Mode", VisionMode.VanillaExtended, "World-only feathered GPU effect, or legacy hard UI overlay fallback.");
            _black = Float("Vision", "SingleEyeBlackStrength", 1, 0, 1, "Static missing-side darkening.");
            _blur = Float("Vision", "SingleEyeBlurStrength", .95f, 0, 1, "Missing-side GPU blur blend.");
            _reach = Float("Vision", "SingleEyeInnerReach", .60f, .4f, .8f, "How far the feather extends from the missing-side edge.");
            _feather = Float("Vision", "SingleEyeFeatherWidth", .40f, .1f, .6f, "Static feather width; black plateau is reach minus feather.");
            _bothBlack = Float("Vision", "BothEyesBlackStrength", 1, 0, 1, "Full blindness darkness strength.");
            _bothBlur = Float("Vision", "BothEyesBlurStrength", 1, 0, 1, "Full blindness GPU blur blend.");
            _center = Float("Vision", "BothEyesCenterVisibility", .025f, 0, .10f, "Residual central world visibility when both eyes are missing.");
            Refresh();
        }
        private static ConfigEntry<float> Float(string section, string name, float value, float min, float max, string description)
            => _config.Bind(section, name, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));
        internal static void Refresh()
        {
            if (_config == null) return;
            HearingTuning.BadSideGain = _sideGain.Value == .20f || _sideGain.Value == .75f ? .65f : _sideGain.Value; HearingTuning.BadSideCutoffHz = _sideCutoff.Value == 800 || _sideCutoff.Value == 1100 ? 750 : _sideCutoff.Value;
            HearingTuning.BothMissingGain = _bothGain.Value == .15f || _bothGain.Value == .65f ? .55f : _bothGain.Value; HearingTuning.BothMissingCutoffHz = _bothCutoff.Value == 650 || _bothCutoff.Value == 800 ? 550 : _bothCutoff.Value;
            HearingTuning.DirectionExponent = _exponent.Value == 2 || _exponent.Value == 1.5f ? 1.25f : _exponent.Value; HearingTuning.MouthGain = _mouthGain.Value;
            HearingTuning.MouthCutoffHz = _mouthCutoff.Value; HearingTuning.MouthDistortion = _distortion.Value;
            SingleEyeAlpha = _singleAlpha.Value; BlindAlpha = _blindAlpha.Value;
            VisionTuning.Mode = _mode.Value; VisionTuning.SingleEyeBlackStrength = _black.Value; VisionTuning.SingleEyeBlurStrength = _blur.Value;
            VisionTuning.SingleEyeInnerReach = _reach.Value; VisionTuning.SingleEyeFeatherWidth = _feather.Value;
            VisionTuning.BothEyesBlackStrength = _bothBlack.Value; VisionTuning.BothEyesBlurStrength = _bothBlur.Value; VisionTuning.BothEyesCenterVisibility = _center.Value;
        }
    }
}
