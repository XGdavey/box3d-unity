using Unity.Mathematics;
using UnityEngine;

namespace Box3d.Hybrid
{
    /// <summary>Capsule axis, matching Unity's CapsuleCollider.direction (0=X, 1=Y, 2=Z).</summary>
    public enum Box3dAxis
    {
        X,
        Y,
        Z,
    }

    /// <summary>A capsule shape, analogous to Unity's CapsuleCollider. Height is the total length
    /// including the hemispherical caps.</summary>
    [AddComponentMenu("Box3d/Box3d Capsule Shape")]
    public class Box3dCapsuleShape : Box3dShape
    {
        [SerializeField, Min(0f), Tooltip("Capsule radius in local units.")]
        private float Radius = 0.5f;

        [SerializeField, Min(0f), Tooltip("Total height including the caps (like Unity's CapsuleCollider.height).")]
        private float Height = 2f;

        [SerializeField, Tooltip("Axis the capsule runs along.")]
        private Box3dAxis Direction = Box3dAxis.Y;

        protected override Shape CreateShape(Body body, float3 scale)
        {
            float3 axis = AxisVector(Direction);
            float3 absScale = math.abs(scale);

            // Radius scales with the two axes perpendicular to the capsule; length with its own axis.
            float axisScale = math.dot(absScale, axis);
            float radialScale = math.cmax(absScale * (1f - axis));
            float scaledRadius = Radius * radialScale;

            // Half the cylindrical segment between the two hemisphere centers.
            float halfSegment = math.max(0f, Height * 0.5f * axisScale - scaledRadius);
            float3 center = LocalCenter * scale;
            float3 offset = axis * halfSegment;

            return body.CreateCapsuleShape(BuildDef(), new Capsule
            {
                Center1 = center - offset,
                Center2 = center + offset,
                Radius = scaledRadius,
            });
        }

        private static float3 AxisVector(Box3dAxis axis)
        {
            switch (axis)
            {
                case Box3dAxis.X: return new float3(1f, 0f, 0f);
                case Box3dAxis.Z: return new float3(0f, 0f, 1f);
                default: return new float3(0f, 1f, 0f);
            }
        }
    }
}
