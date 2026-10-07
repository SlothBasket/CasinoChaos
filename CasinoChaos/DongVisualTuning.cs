using UnityEngine;

namespace GWYF_CasinoChaos
{
    internal static class DongVisualTuning
    {
        internal const string BodyPath = "Model/Body/BodyModel";
        internal const string MeshPath = "BodyMesh";
        internal const float Mass = .04f, LinearDamping = .35f, AngularDamping = 2f;
        internal const float SwingLimitDegrees = 55f, MaximumAngularSpeed = 6f;
        internal const float TeleportDistance = 1.5f;
        // BodyMesh spans approximately Y=-.397..+.813 and Z=-.36..+.36
        // in BodyModel coordinates. Appearance.AttachmentDepth supplies size-dependent Z; the pivot embeds the top in the lower front
        // and leaves the resting tip above the body's lowest point.
        internal static readonly Vector3 AttachmentOffset = new Vector3(0, .03f, 0);
        // Render-only pose; isolated hinge physics and world attachment stay intact.
        internal const float OutwardTiltDegrees = -10f;
        internal static readonly Vector3 FirstPersonAdjustment = new Vector3(0, -.04f, -.265f);
    }
}
