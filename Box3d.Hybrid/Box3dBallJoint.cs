using UnityEngine;

namespace Box3D.Hybrid
{
    /// <summary>A ball-and-socket (spherical) joint, analogous to Unity's CharacterJoint. Allows
    /// rotation about the anchor with optional cone (swing) and twist limits — the ragdoll joint.</summary>
    [Icon("Packages/com.suvitruf.box3d/Box3D.Hybrid.Editor/Icons/Box3DJoint.png")]
    [AddComponentMenu("Box3D/Joints/Ball Joint")]
    public class Box3DBallJoint : Box3DJoint
    {
        [SerializeField, Tooltip("Local twist axis (the cone is centered on it).")]
        private Vector3 Axis = Vector3.up;

        [SerializeField, Tooltip("Limit the swing (cone) angle.")]
        private bool UseConeLimit;

        [SerializeField, Range(0f, 180f), Tooltip("Cone half-angle in degrees.")]
        private float ConeAngle = 45f;

        [SerializeField, Tooltip("Limit the twist about the axis.")]
        private bool UseTwistLimit;

        [SerializeField, Tooltip("Lower twist limit in degrees.")]
        private float MinTwist = -45f;

        [SerializeField, Tooltip("Upper twist limit in degrees.")]
        private float MaxTwist = 45f;

        [Header("Spring")]
        [SerializeField] private bool UseSpring = true;
        [SerializeField] private float SpringHertz = 3f;
        [SerializeField] private float SpringDamping = 0.3f;

        public void SetSpring(float hertz, float damping) { UseSpring = true; SpringHertz = hertz; SpringDamping = damping; if (NativeJoint.IsValid) { var j = new SphericalJoint { Id = NativeJoint.Id }; j.EnableSpring(true); j.SetSpringHertz(hertz); j.SetSpringDampingRatio(damping); } }
        public void SetConeLimit(bool enable, float angle) { UseConeLimit = enable; ConeAngle = angle; if (NativeJoint.IsValid) { var j = new SphericalJoint { Id = NativeJoint.Id }; j.SetConeLimit(enable ? angle * Mathf.Deg2Rad : float.MaxValue); } }
        public void SetTwistLimit(bool enable, float min, float max) { UseTwistLimit = enable; MinTwist = min; MaxTwist = max; if (NativeJoint.IsValid) { var j = new SphericalJoint { Id = NativeJoint.Id }; j.SetTwistLimits(enable ? min * Mathf.Deg2Rad : -float.MaxValue, enable ? max * Mathf.Deg2Rad : float.MaxValue); } }

        private SphericalJoint _ball;
        
        protected override Joint CreateJoint(BodyId bodyA, BodyId bodyB)
        {
            Vector3 worldAxis = transform.TransformDirection(Axis);

            SphericalJointDef def = SphericalJointDef.Default;
            if (UseSpring) { def.EnableSpring = true; def.Hertz = SpringHertz; def.DampingRatio = SpringDamping; }
            SetupBase(ref def.Base, bodyA, bodyB, LocalAxisFrame(worldAxis));

            if (UseConeLimit)
            {
                def.EnableConeLimit = true;
                def.ConeAngle = ConeAngle * Mathf.Deg2Rad;
            }
            if (UseTwistLimit)
            {
                def.EnableTwistLimit = true;
                def.LowerTwistAngle = Mathf.Min(MinTwist, MaxTwist) * Mathf.Deg2Rad;
                def.UpperTwistAngle = Mathf.Max(MinTwist, MaxTwist) * Mathf.Deg2Rad;
            }

            _ball = World.World.CreateSphericalJoint(def);
            return _ball;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Push Inspector edits to the live joint during play. Axis, Anchor and Connected Body
            // are baked into the joint frames at creation and stay fixed.
            if (!Application.isPlaying || !_ball.IsValid) return;
            _ball.EnableConeLimit(UseConeLimit);
            _ball.SetConeLimit(ConeAngle * Mathf.Deg2Rad);
            _ball.EnableTwistLimit(UseTwistLimit);
            _ball.SetTwistLimits(Mathf.Min(MinTwist, MaxTwist) * Mathf.Deg2Rad,
                                 Mathf.Max(MinTwist, MaxTwist) * Mathf.Deg2Rad);
            WakeBodies();
        }
#endif
    }
}
