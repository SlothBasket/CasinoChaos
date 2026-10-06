using System;
using FMOD;
using FMODUnity;

namespace GWYF_CasinoChaos
{
    // Owns only these three DSPs. Never writes vanilla channel/bus volumes.
    internal sealed class FmodDspChain : IDisposable
    {
        private Channel _channel;
        private ChannelGroup _group;
        private readonly bool _isGroup;
        private readonly FMOD.System _core;
        private DSP _gain, _lowpass, _distortion;
        internal FmodDspChain(Channel channel) { _core = RuntimeManager.CoreSystem; _channel = channel; Create(); }
        internal FmodDspChain(ChannelGroup group) : this(group, RuntimeManager.CoreSystem) { }
        internal FmodDspChain(ChannelGroup group, FMOD.System core) { _core = core; _group = group; _isGroup = true; Create(); }
        private void Add(DSP dsp)
        {
            var result = _isGroup ? _group.addDSP(CHANNELCONTROL_DSP_INDEX.HEAD, dsp) : _channel.addDSP(CHANNELCONTROL_DSP_INDEX.HEAD, dsp);
            if (result != RESULT.OK) throw new InvalidOperationException("Audio DSP attach: " + result);
        }
        private void Create()
        {
            try
            {
                Make(DSP_TYPE.FADER, out _gain); Make(DSP_TYPE.LOWPASS_SIMPLE, out _lowpass); Make(DSP_TYPE.DISTORTION, out _distortion);
                Set(1, HearingTuning.NormalCutoffHz, 0);
            }
            catch { Dispose(); throw; }
        }
        private void Make(DSP_TYPE type, out DSP dsp)
        {
            var result = _core.createDSPByType(type, out dsp);
            if (result != RESULT.OK) throw new InvalidOperationException("Audio DSP create: " + result);
            dsp.setBypass(true); Add(dsp);
        }
        internal void Set(float gain, float cutoff, float distortion)
        {
            _gain.setParameterFloat((int)DSP_FADER.GAIN, (float)(20 * Math.Log10(Math.Max(0.001, gain))));
            _gain.setBypass(gain >= 0.9999f);
            _lowpass.setParameterFloat((int)DSP_LOWPASS_SIMPLE.CUTOFF, cutoff);
            _lowpass.setBypass(cutoff >= HearingTuning.NormalCutoffHz - 1);
            _distortion.setParameterFloat((int)DSP_DISTORTION.LEVEL, distortion);
            _distortion.setBypass(distortion <= 0);
        }
        private void Remove(ref DSP dsp)
        {
            if (!dsp.hasHandle()) return;
            dsp.setBypass(true);
            if (_isGroup) _group.removeDSP(dsp); else _channel.removeDSP(dsp);
            dsp.release(); dsp.clearHandle();
        }
        public void Dispose() { Remove(ref _distortion); Remove(ref _lowpass); Remove(ref _gain); }
    }
}
