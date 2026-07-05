using Unity.Mathematics;
using UnityEngine;

namespace Box3d.Hybrid
{
    /// <summary>A fixed (weld) joint, analogous to Unity's FixedJoint: rigidly locks this body to
    /// its connected body (or the world) at their current relative pose. Optional spring softness.</summary>
    [AddComponentMenu("Box3d/Box3d Fixed Joint")]
    public class Box3dFixedJoint : Box3dJoint
    {
        [SerializeField, Min(0f), Tooltip("Linear spring stiffness (Hz). 0 = perfectly rigid.")]
        private float LinearHertz;

        [SerializeField, Min(0f), Tooltip("Angular spring stiffness (Hz). 0 = perfectly rigid.")]
        private float AngularHertz;

        protected override Joint CreateJoint(BodyId bodyA, BodyId bodyB)
        {
            WeldJointDef def = WeldJointDef.Default;
            // Weld holds the two frames coincident; identity frame B + derived frame A freeze the
            // bodies at their current relative pose.
            SetupBase(ref def.Base, bodyA, bodyB, quaternion.identity);
            def.LinearHertz = LinearHertz;
            def.AngularHertz = AngularHertz;

            return World.World.CreateWeldJoint(def);
        }
    }
}
