using Unity.Mathematics;
using UnityEngine;

namespace Box3D.Hybrid
{
    /// <summary>Capsule axis, matching Unity's CapsuleCollider.direction (0=X, 1=Y, 2=Z).</summary>
    public enum Box3DAxis
    {
        X,
        Y,
        Z,
    }

    /// <summary>A capsule shape, analogous to Unity's CapsuleCollider. Height is the total length
    /// including the hemispherical caps.</summary>
    [Icon("Packages/com.suvitruf.box3d/Box3D.Hybrid.Editor/Icons/Box3DCapsuleShape.png")]
    [AddComponentMenu("Box3D/Shapes/Capsule Shape")]
    public class Box3DCapsuleShape : Box3DShape
    {
        [SerializeField, Min(0f), Tooltip("Capsule radius in local units.")]
        private float Radius = 0.5f;

        [SerializeField, Min(0f), Tooltip("Total height including the caps (like Unity's CapsuleCollider.height).")]
        private float Height = 2f;

        [SerializeField, Tooltip("Axis the capsule runs along.")]
        private Box3DAxis Direction = Box3DAxis.Y;

        public float ShapeRadius => Radius;
        public float ShapeHeight => Height;

        public void ConfigureSensor(float radius, float height, Vector3 center, Box3DAxis direction)
        {
            Radius = radius; Height = height; Center = center; Direction = direction;
        }

        protected override Shape CreateShape(Body body, float3 localPosition, quaternion localRotation, float3 scale)
        {
            CapsuleRadius = Radius;
            CapsuleHeight = Height;
            Resolve(localPosition, localRotation, scale, out float3 center1, out float3 center2, out float radius);
            return body.CreateCapsuleShape(BuildDef(), new Capsule
            {
                Center1 = center1,
                Center2 = center2,
                Radius = radius,
            });
        }

        protected override Vector3 ClosestPointLocal(float3 localPoint)
        {
            Resolve(float3.zero, quaternion.identity, Vector3.one, out float3 c1, out float3 c2, out float r);
            float3 axis = c2 - c1;
            float lenSq = math.lengthsq(axis);
            if (lenSq < 1e-6f) return (Vector3)c1 + (Vector3)(math.normalize(localPoint - c1) * r);

            float t = math.dot(localPoint - c1, axis) / lenSq;
            t = math.clamp(t, 0f, 1f);
            float3 closestAxis = c1 + t * axis;
            return (Vector3)(closestAxis + math.normalize(localPoint - closestAxis) * r);
        }

        protected override void UpdateLiveGeometry()
        {
            Resolve(AttachedPosition, AttachedRotation, AttachedScale, out float3 center1, out float3 center2, out float radius);
            LiveShape.SetCapsule(new Capsule
            {
                Center1 = center1,
                Center2 = center2,
                Radius = radius,
            });
        }

        // The two hemisphere centers and radius after baking the local transform and scale —
        // shared by the shape and gizmo.
        private void Resolve(float3 localPosition, quaternion localRotation, float3 scale,
            out float3 center1, out float3 center2, out float radius)
        {
            float3 axis = AxisVector(Direction);
            float3 absScale = math.abs(scale);
            float axisScale = math.dot(absScale, axis);
            float radialScale = math.cmax(absScale * (1f - axis));
            radius = Radius * radialScale;

            float halfSegment = math.max(0f, Height * 0.5f * axisScale - radius);
            float3 center = ShapeCenter(localPosition, localRotation, scale);
            float3 offset = math.mul(localRotation, axis) * halfSegment;
            center1 = center - offset;
            center2 = center + offset;
        }

        private void OnDrawGizmosSelected()
        {
            SetGizmoFrame();
            // Gizmo draws in the shape's own frame, so local transform is identity here.
            Resolve(float3.zero, quaternion.identity, transform.lossyScale, out float3 c1, out float3 c2, out float radius);

            float3 axis = AxisVector(Direction);
            float3 side = math.abs(axis.y) < 0.99f
                ? math.normalize(math.cross(axis, new float3(0f, 1f, 0f)))
                : new float3(1f, 0f, 0f);
            float3 forward = math.cross(axis, side);

            Gizmos.DrawWireSphere((Vector3)c1, radius);
            Gizmos.DrawWireSphere((Vector3)c2, radius);
            Gizmos.DrawLine((Vector3)(c1 + side * radius), (Vector3)(c2 + side * radius));
            Gizmos.DrawLine((Vector3)(c1 - side * radius), (Vector3)(c2 - side * radius));
            Gizmos.DrawLine((Vector3)(c1 + forward * radius), (Vector3)(c2 + forward * radius));
            Gizmos.DrawLine((Vector3)(c1 - forward * radius), (Vector3)(c2 - forward * radius));
        }

        private static float3 AxisVector(Box3DAxis axis)
        {
            switch (axis)
            {
                case Box3DAxis.X: return new float3(1f, 0f, 0f);
                case Box3DAxis.Z: return new float3(0f, 0f, 1f);
                default: return new float3(0f, 1f, 0f);
            }
        }
    }
}
