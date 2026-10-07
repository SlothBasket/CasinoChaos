using System;

namespace GWYF_CasinoChaos
{
    internal static class EyeTradeRules
    {
        internal static bool TryChoose(bool left, bool right, bool buying, bool chooseRight, out bool rightEye)
        {
            bool l = buying ? !left : left, r = buying ? !right : right;
            rightEye = l && r ? chooseRight : r;
            return l || r;
        }
        internal static bool TryTransact(bool left, bool right, bool buying, bool chooseRight, int price,
            Func<long, bool> tickets, Action<bool, bool> toggle, out bool rightEye)
        {
            if (!TryChoose(left, right, buying, chooseRight, out rightEye) || price < 0) return false;
            if (!tickets(buying ? -(long)price : price)) return false;
            toggle(rightEye, buying); return true;
        }
    }
}
