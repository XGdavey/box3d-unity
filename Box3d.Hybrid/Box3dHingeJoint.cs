using UnityEngine;

namespace Box3d.Hybrid
{
    /// <summary>A hinge (revolute) joint, analogous to Unity's HingeJoint. Rotates around
    /// <see cref="Axis"/> through the anchor, with optional angle limits and a motor.</summary>
    [AddComponentMenu("Box3d/Box3d Hinge Joint")]
    public class Box3dHingeJoint : Box3dJoint
    {
        [SerializeField, Tooltip("Local rotation axis of the hinge.")]
        private Vector3 Axis = Vector3.up;

        [SerializeField, Tooltip("Limit the hinge angle.")]
        private bool UseLimits;

        [SerializeField, Tooltip("Lower angle limit in degrees.")]
        private float MinAngle = -45f;

        [SerializeField, Tooltip("Upper angle limit in degrees.")]
        private float MaxAngle = 45f;

        [SerializeField, Tooltip("Drive the hinge with a motor.")]
        private bool UseMotor;

        [SerializeField, Tooltip("Motor target speed in degrees/second.")]
        private float MotorSpeed = 90f;

        [SerializeField, Min(0f), Tooltip("Maximum motor torque in N·m.")]
        private float MaxMotorTorque = 10f;

        /// <summary>Sets the local hinge axis. Must be set before the joint is created (Start).</summary>
        public void SetAxis(Vector3 localAxis)
        {
            Axis = localAxis;
        }

        protected override Joint CreateJoint(BodyId bodyA, BodyId bodyB)
        {
            Vector3 worldAxis = transform.TransformDirection(Axis);

            RevoluteJointDef def = RevoluteJointDef.Default;
            SetupBase(ref def.Base, bodyA, bodyB, LocalAxisFrame(worldAxis));

            if (UseLimits)
            {
                def.EnableLimit = true;
                def.LowerAngle = Mathf.Min(MinAngle, MaxAngle) * Mathf.Deg2Rad;
                def.UpperAngle = Mathf.Max(MinAngle, MaxAngle) * Mathf.Deg2Rad;
            }
            if (UseMotor)
            {
                def.EnableMotor = true;
                def.MotorSpeed = MotorSpeed * Mathf.Deg2Rad;
                def.MaxMotorTorque = MaxMotorTorque;
            }

            return World.World.CreateRevoluteJoint(def);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 anchor = WorldAnchor;
            Vector3 axis = transform.TransformDirection(Axis).normalized * 0.5f;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawLine(anchor - axis, anchor + axis);
            Gizmos.DrawWireSphere(anchor, 0.06f);
        }
    }
}
