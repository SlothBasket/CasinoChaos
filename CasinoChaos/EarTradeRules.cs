using System;
using System.Collections.Generic;

namespace GWYF_CasinoChaos
{
    internal sealed class EarPressLedger
    {
        private readonly Dictionary<object, uint> _last = new Dictionary<object, uint>();
        internal bool TryAccept(object sender, uint sequence)
        {
            if (sender == null || sequence == 0 || (_last.TryGetValue(sender, out uint previous) && sequence <= previous)) return false;
            _last[sender] = sequence; return true;
        }
        internal void Clear() => _last.Clear();
    }
}
