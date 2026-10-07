namespace GWYF_CasinoChaos
{
    internal static class FartRules
    {
        internal static bool CanAttempt(BodyState state)=>state.Revision!=0;
        internal static bool CanPlay(BodyState state)=>state.Revision!=0&&state.Has(CustomBodyPart.Butt);
        internal static byte ChooseClip(BodyState state,System.Random random)=>
            CanPlay(state)?(byte)random.Next(FartClipData.NormalCount):FartClipData.DryPuff;
        internal static byte ChoosePitch(byte clip,System.Random random)=>
            clip==FartClipData.DryPuff?(byte)random.Next(92,109):(byte)100;
    }
}
