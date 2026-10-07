using System;
namespace GWYF_CasinoChaos
{
    internal enum VisionMode { VanillaExtended, HardOverlay }
    internal static class VisionTuning
    {
        internal static VisionMode Mode=VisionMode.VanillaExtended;
        internal static float SingleEyeBlackStrength=1,SingleEyeBlurStrength=.95f,SingleEyeInnerReach=.60f,SingleEyeFeatherWidth=.40f;
        internal static float BothEyesBlackStrength=1,BothEyesBlurStrength=1,BothEyesCenterVisibility=.025f;
        internal static float Side(float x)
        {
            float start=Math.Max(0,SingleEyeInnerReach-SingleEyeFeatherWidth);
            float t=Math.Max(0,Math.Min(1,(x-start)/Math.Max(.01f,SingleEyeInnerReach-start)));
            float smooth=t*t*(3-2*t);
            return (float)Math.Pow(1-smooth,.45);
        }
        internal static void Mask(float x,bool left,bool right,out float dark,out float blur)
        {
            float severity=Math.Max(left?Side(x):0,right?Side(1-x):0);
            bool both=left&&right;
            // The both-missing mask uses screen Y: a narrow horizontal slice,
            // with a small plateau and feather to black above/below it.
            float t=Math.Max(0,Math.Min(1,(Math.Abs(x-.5f)-.055f)/.155f));
            float opening=1-t*t*t*(t*(6*t-15)+10);
            dark=both?BothEyesBlackStrength*(1-BothEyesCenterVisibility*opening):SingleEyeBlackStrength*severity;
            blur=both?BothEyesBlurStrength:SingleEyeBlurStrength*severity;
        }
    }
}
