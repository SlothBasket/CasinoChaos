namespace GWYF_CasinoChaos
{
    internal readonly struct DongAppearance
    {
        internal readonly float Width, Length, Depth;
        internal readonly bool Sideways, FullRotation, BroadTip;
        // Tiny roots sit closer to the body; larger roots retain clearance.
        internal float AttachmentDepth => .338f + Depth * .12f;
        internal float AngularDamping => FullRotation ? .12f : Length < .11f ? .25f : 2f;
        internal float MaximumAngularSpeed => FullRotation ? 24f : Length < .11f ? 14f : 6f;
        internal DongAppearance(float width, float length, float depth, bool sideways, bool fullRotation, bool broadTip)
        { Width=width; Length=length; Depth=depth; Sideways=sideways; FullRotation=sideways&&fullRotation; BroadTip=broadTip; }
        private static ulong Mix(ulong n)
        {
            unchecked { n += 0x9e3779b97f4a7c15UL; n=(n^(n>>30))*0xbf58476d1ce4e5b9UL; n=(n^(n>>27))*0x94d049bb133111ebUL; return n^(n>>31); }
        }
        private static float Range(ulong n,float min,float max) => min+(max-min)*((n&0xffffff)/16777215f);
        internal static DongAppearance For(ulong id,uint shuffle)
        {
            // Identity provides a stable random initial appearance on all clients.
            // Only the host's shuffle counter is synchronized, never hinge angles.
            ulong n=Mix(id ^ ((ulong)shuffle*0x9e3779b97f4a7c15UL));
            // Mostly normal sizes, with deliberately comic small/large tails.
            int tier=(int)(Mix(n+2)%10);
            float widthMin=tier<2?.025f:tier>=8?.18f:.09f;
            float widthMax=tier<2?.05f:tier>=8?.24f:.15f;
            float lengthMin=tier<2?.055f:tier>=8?.36f:.20f;
            float lengthMax=tier<2?.10f:tier>=8?.42f:.32f;
            float depthMin=tier<2?.025f:tier>=8?.14f:.08f;
            float depthMax=tier<2?.045f:tier>=8?.19f:.13f;
            return new DongAppearance(Range(n,widthMin,widthMax),Range(Mix(n),lengthMin,lengthMax),
                Range(Mix(n+1),depthMin,depthMax),(Mix(n+17)&1)!=0, Mix(n+21)%5<2, (Mix(n+23)&1)!=0);
        }
    }
}
