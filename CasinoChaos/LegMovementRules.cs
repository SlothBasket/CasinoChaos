namespace GWYF_CasinoChaos
{
    internal static class LegTuning
    {
        internal const float HopRunFraction=.70f, SlideWalkFraction=.25f, LandingDelay=.15f, AirSteering=.10f;
        internal static int Count(BodyState state)=>(state.Has(CustomBodyPart.LeftLeg)?1:0)+(state.Has(CustomBodyPart.RightLeg)?1:0);
    }
    internal sealed class HopGate
    {
        internal bool InFlight {get;private set;}
        private bool _leftGround;
        private float _takeoff, _landed=-1;
        internal void Observe(float time,bool grounded,float vertical)
        {
            if(!InFlight)return;
            if(!grounded)_leftGround=true;
            if(grounded&&vertical<=.2f&&(_leftGround||time-_takeoff>.35f))
            {InFlight=false;_landed=time;}
        }
        internal bool TryStart(float time,bool grounded,bool wants)
        {
            if(!wants||!grounded||InFlight||time-_landed<LegTuning.LandingDelay)return false;
            InFlight=true;_leftGround=false;_takeoff=time;return true;
        }
    }
}
