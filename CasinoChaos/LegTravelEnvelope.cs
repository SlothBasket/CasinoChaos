using System;
namespace GWYF_CasinoChaos
{
    internal static class LegTravelEnvelope
    {
        internal static bool Plan(float available,int legs,float speed,float receiptDelta,float peakDelta,float distance,bool groundIdle,out float remaining)
        {
            remaining=available;
            if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<0)return false;
            float rate=groundIdle?.05f:speed*1.25f;
            float budget=Math.Min(speed*.25f+.25f,available+rate*Math.Max(0,Math.Min(.25f,receiptDelta)));
            if(distance>budget||distance>speed*Math.Max(.02f,Math.Min(.25f,peakDelta))*1.35f+.12f||(legs==1&&groundIdle&&distance>.12f))return false;
            remaining=budget-distance;return true;
        }
    }
}
