using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;

namespace Aoiru.ltle
{
    [AddComponentMenu("Aoiru/iliToon Light Edit Marker")]
    public class AoiruLtleMarker : AvatarTagComponent
    {
        public float minLightLimit;
        public float maxLightLimit = 1f;
        public float monochromeLighting;
        public float shadowEnvStrength;
        public float asUnlit;

        public VRCExpressionsMenu installTargetMenu;
    }
}
