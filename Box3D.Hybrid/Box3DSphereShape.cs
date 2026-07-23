using Unity.Mathematics;
using UnityEngine;

namespace Box3D.Hybrid
{
    /// <summary>A sphere shape, analogous to Unity's SphereCollider. Non-uniform scale uses the
    /// largest axis (same limitation as Unity).</summary>
    [Icon("Packages/com.suvitruf.box3d/Box3D.Hybrid.Editor/Icons/Box3DSphereShape.png")]
    [AddComponentMenu("Box3D/Shapes/Sphere Shape")]
    public class Box3DSphereShape : Box3DShape
    {
        [SerializeField, Min(0f), Tooltip("Sphere radius in local units.")]
        private float Radius = 0.5f;

        public float ShapeRadius => Radius;

        public void ConfigureSensor(float radius, Vector3 center)
        {
            Radius = radius; Center = center;
        }

        protected override Shape CreateShape(Body body, float3 localPosition, quaternion localRotation, float3 scale)
        {
            return body.CreateSphereShape(BuildDef(), BuildSphere(localPosition, localRotation, scale));
        }

        protected override void UpdateLiveGeometry()
        {
            LiveShape.SetSphere(BuildSphere(AttachedPosition, AttachedRotation, AttachedScale));
        }

        private Sphere BuildSphere(float3 localPosition, quaternion localRotation, float3 scale)
        {
            return new Sphere
            {
                Center = ShapeCenter(localPosition, localRotation, scale),
                Radius = Radius * math.cmax(math.abs(scale)),
            };
        }

        protected override Vector3 ClosestPointLocal(float3 localPoint)
        {
            var localCenter = (float3)Center;
            float3 dir = localPoint - localCenter;
            float dist = math.length(dir);
            if (dist <= Radius) return (Vector3)localPoint;
            return (Vector3)(localCenter + dir / dist * Radius);
        }

        private void OnDrawGizmosSelected()
        {
            SetGizmoFrame();
            float radius = Radius * math.cmax(math.abs((float3)transform.lossyScale));
            Gizmos.DrawWireSphere((Vector3)ScaledCenter, radius);
        }
    }
}
