using UnityEngine;

namespace GWYF_CasinoChaos
{
    // Unity fallback: processes samples without overwriting volume or filters.
    public sealed class UnityHearingModifier : MonoBehaviour
    {
        private volatile float _gain = 1, _alpha = 1;
        private readonly float[] _history = new float[32];
        internal float Strength;
        internal void Set(float gain, float cutoff)
        {
            _gain = gain;
            _alpha = cutoff >= HearingTuning.NormalCutoffHz - 1 ? 1 :
                1 - Mathf.Exp(-2 * Mathf.PI * cutoff / Mathf.Max(1, AudioSettings.outputSampleRate));
        }
        private void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = _gain, alpha = _alpha;
            if (channels <= 0 || channels > _history.Length) return;
            if (gain >= 0.9999f && alpha >= 1) { System.Array.Clear(_history, 0, _history.Length); return; }
            for (int i = 0; i < data.Length; i++)
            {
                int c = i % channels;
                float sample = _history[c] + alpha * (data[i] - _history[c]);
                _history[c] = sample; data[i] = sample * gain;
            }
        }
    }
}
