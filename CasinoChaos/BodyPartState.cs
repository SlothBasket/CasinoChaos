using System;
using System.Collections.Generic;

namespace GWYF_CasinoChaos
{
    internal enum CustomBodyPart : byte { LeftEar, RightEar, LeftLeg, RightLeg, Dong, Butt }

    internal readonly struct BodyState
    {
        internal const byte CompleteMask = 63;
        internal readonly byte PresentMask;
        internal readonly uint Revision;
        internal BodyState(byte mask, uint revision) { PresentMask = mask; Revision = revision; }
        internal bool Has(CustomBodyPart part) => (PresentMask & (1 << (int)part)) != 0;
        internal static BodyState Complete => new BodyState(CompleteMask, 0);
    }

    // Transport-independent client view. Audio observes this view; it never
    // grants/removes a part. Only the server transport publishes accepted states.
    internal static class BodyPartState
    {
        private static readonly Dictionary<ulong, BodyState> States = new Dictionary<ulong, BodyState>();
        internal static event Action<ulong, CustomBodyPart, bool> Changed;
        internal static BodyState Get(ulong steamId) => States.TryGetValue(steamId, out var state) ? state : BodyState.Complete;
        internal static bool Receive(ulong steamId, byte mask, uint revision)
        {
            if (steamId == 0 || (mask & ~BodyState.CompleteMask) != 0 || revision == 0) return false;
            bool exists = States.TryGetValue(steamId, out var before);
            if (exists && revision <= before.Revision) return false;
            if (!exists) before = BodyState.Complete;
            States[steamId] = new BodyState(mask, revision);
            foreach (CustomBodyPart part in Enum.GetValues(typeof(CustomBodyPart)))
                if (!exists || before.Has(part) != Get(steamId).Has(part))
                    Changed?.Invoke(steamId, part, Get(steamId).Has(part));
            return true;
        }
        internal static void Clear()
        {
            var old = new List<KeyValuePair<ulong, BodyState>>(States);
            States.Clear();
            foreach (var entry in old)
                foreach (CustomBodyPart part in Enum.GetValues(typeof(CustomBodyPart)))
                    if (!entry.Value.Has(part)) Changed?.Invoke(entry.Key, part, true);
        }
    }
}
