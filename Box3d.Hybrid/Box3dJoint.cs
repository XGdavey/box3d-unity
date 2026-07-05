using Unity.Mathematics;
using UnityEngine;

namespace Box3d.Hybrid
{
    /// <summary>Base class for joint components, analogous to Unity's joints. Goes on a GameObject
    /// that has a <see cref="Box3dBody"/> (the constrained body); connects it to another
    /// <see cref="ConnectedBody"/>, or to the world if that is null. The joint is created after all
    /// bodies exist (Start), so both endpoints are ready.</summary>
    [RequireComponent(typeof(Box3dBody))]
    public abstract class Box3dJoint : MonoBehaviour
    {
        [SerializeField, Tooltip("Body this joint attaches to. Null = the world (a fixed point).")]
        private Box3dBody ConnectedBody;

        [SerializeField, Tooltip("Local anchor point on this body (the joint pivot).")]
        private Vector3 Anchor = Vector3.zero;

        [SerializeField, Tooltip("Let the two connected bodies collide with each other.")]
        private bool CollideConnected;

        private Joint _joint;

        /// <summary>The world owning this joint (valid after Start).</summary>
        protected Box3dWorld World { get; private set; }

        /// <summary>The joint pivot in world space.</summary>
        protected Vector3 WorldAnchor => transform.TransformPoint(Anchor);

        /// <summary>Sets the connected body (null = world). Must be set before the joint is created
        /// (Start).</summary>
        public void SetConnectedBody(Box3dBody body)
        {
            ConnectedBody = body;
        }

        /// <summary>Sets the local anchor point. Must be set before the joint is created (Start).</summary>
        public void SetAnchor(Vector3 localAnchor)
        {
            Anchor = localAnchor;
        }

        private void Start()
        {
            World = Box3dWorld.Instance;

            Box3dBody self = GetComponent<Box3dBody>();
            BodyId bodyB = self.Body.Id;
            BodyId bodyA = ConnectedBody ? ConnectedBody.Body.Id : World.WorldAnchor.Id;

            _joint = CreateJoint(bodyA, bodyB);
        }

        private void OnDestroy()
        {
            if (_joint.IsValid) _joint.Destroy();
        }

        /// <summary>Creates the native joint between the two bodies (bodyA = connected/world,
        /// bodyB = this).</summary>
        protected abstract Joint CreateJoint(BodyId bodyA, BodyId bodyB);

        /// <summary>Fills the shared joint-def base. Frame B uses <paramref name="frameRotationB"/>
        /// (this body's local frame orientation); frame A is derived so the two frames coincide in
        /// world space at the current pose — so creating the joint doesn't snap the bodies.</summary>
        protected void SetupBase(ref JointDefBase baseDef, BodyId bodyA, BodyId bodyB, quaternion frameRotationB)
        {
            baseDef.BodyIdA = bodyA;
            baseDef.BodyIdB = bodyB;
            baseDef.CollideConnected = CollideConnected;

            Vector3 anchor = WorldAnchor;
            baseDef.LocalFrameB = new B3Transform
            {
                Position = transform.InverseTransformPoint(anchor),
                Rotation = frameRotationB,
            };

            // World orientation of frame B, expressed back in each body's local space for frame A.
            quaternion frameWorld = math.mul((quaternion)transform.rotation, frameRotationB);
            baseDef.LocalFrameA = ConnectedBody
                ? new B3Transform
                {
                    Position = ConnectedBody.transform.InverseTransformPoint(anchor),
                    Rotation = math.mul(math.inverse((quaternion)ConnectedBody.transform.rotation), frameWorld),
                }
                : new B3Transform { Position = anchor, Rotation = frameWorld }; // world anchor: identity body
        }

        /// <summary>The frame rotation (in this body's local space) that maps box3d's frame z-axis
        /// onto <paramref name="worldAxis"/> — for axis-based joints (hinge z, etc.).</summary>
        protected quaternion LocalAxisFrame(Vector3 worldAxis)
        {
            Vector3 localAxis = transform.InverseTransformDirection(worldAxis);
            return Quaternion.FromToRotation(Vector3.forward, localAxis.sqrMagnitude > 1e-8f ? localAxis.normalized : Vector3.forward);
        }
    }
}
