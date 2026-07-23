using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Box3D.Hybrid
{
    /// <summary>Base class for shape components, analogous to Unity's Collider. When the GameObject
    /// (or an ancestor) has a <see cref="Box3DBody"/>, the shape attaches to it — including shapes
    /// on child GameObjects (compound colliders). A shape with no body anywhere above it creates
    /// its own static body, mirroring Unity's "collider without a rigidbody is static".
    /// Adding a shape in the editor auto-adds a <see cref="Box3DBody"/> when the hierarchy has
    /// none (set its type to Static for non-moving geometry).
    /// Friction and restitution can be changed at runtime; density is baked at creation.</summary>
    public abstract class Box3DShape : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Density in kg/m³ (mass = density × volume). Baked at creation.")]
        private float Density = 1000f;

        [SerializeField, Min(0f), Tooltip("Coulomb friction coefficient.")]
        private float Friction = 0.6f;

        [SerializeField, Range(0f, 1f), Tooltip("Bounciness. Only applies above the world's restitution speed threshold (~1 m/s), so gentle settling never bounces.")]
        private float Restitution;

        [SerializeField, Tooltip("Local offset of the shape from the body origin.")]
        protected Vector3 Center = Vector3.zero;

        [SerializeField, Tooltip("Is this shape a sensor (trigger)?")]
        public bool IsSensor;

        [SerializeField, Tooltip("Enable sensor events for trigger-style detection.")]
        private bool EnableSensorEvents = true;

        [SerializeField, Tooltip("Enable contact events for OnCollision-style detection.")]
        private bool EnableContactEvents;

        private Shape _shape;
        private Body _ownBody;
        private GCHandle _selfHandle;

        public ShapeId ShapeId => _shape.Id;
        public Shape Shape => _shape;
        public bool IsSensorShape => IsSensor;

        // The attach frame from AttachTo, kept so Inspector edits can rebuild geometry in place.
        private float3 _attachPosition;
        private quaternion _attachRotation = quaternion.identity;
        private float3 _attachScale = new float3(1f);

        protected float3 LocalCenter => Center;
        public float CapsuleRadius { get; protected set; }
        public float CapsuleHeight { get; protected set; }

        /// <summary>The live native shape (valid between Awake and OnDestroy).</summary>
        protected Shape LiveShape => _shape;

        protected float3 AttachedPosition => _attachPosition;
        protected quaternion AttachedRotation => _attachRotation;
        protected float3 AttachedScale => _attachScale;
        
        public Vector3 ShapeLocalCenter => Center;

        //protected float3 LocalCenter => Center;
        
        /// <summary>Sets friction, updating the live shape if it exists.</summary>
        public void SetFriction(float value) { Friction = value; if (_shape.IsValid) _shape.SetFriction(value); }
        public void SetRestitution(float value) { Restitution = value; if (_shape.IsValid) _shape.SetRestitution(value); }
        public void SetDensity(float value) { Density = value; if (_shape.IsValid) _shape.SetDensity(value, updateBodyMass: true); }

#if UNITY_EDITOR
        // Runs when the component is added in the editor (and on context-menu Reset). Like
        // RequireComponent, but hierarchy-aware: a compound shape on a child of a body must NOT
        // get its own body (a nested Box3DBody splits the compound), so only orphan shapes get one.
        // For static geometry, set the auto-added body's type to Static.
        private void Reset()
        {
            if (!GetComponentInParent<Box3DBody>(true))
            {
                UnityEditor.Undo.AddComponent<Box3DBody>(gameObject);
            }
        }
#endif

        private void Awake()
        {
            // A body on this GameObject or an ancestor will gather and attach this shape (including
            // as a compound child). Otherwise the shape is an orphan → give it a static body.
            if (GetComponentInParent<Box3DBody>()) return;

            Box3DWorld world = Box3DWorld.Instance;
            world.EnsureCreated();
            BodyDef def = BodyDef.Default;
            def.Position = transform.position;
            def.Rotation = transform.rotation;
            _ownBody = world.World.CreateBody(def);
            AttachTo(_ownBody, float3.zero, quaternion.identity, transform.lossyScale);
            Box3DWorld.Instance.RegisterStandaloneShape(this);
        }

        internal void SyncTransform()
        {
            if (!_ownBody.IsValid) return;
            _ownBody.SetTransform(transform.position, transform.rotation);
        }

        private void OnEnable() { if (_ownBody.IsValid) _ownBody.Enable(); }
        
        private void OnDisable() { if (_ownBody.IsValid) _ownBody.Disable(); }

        private void OnDestroy()
        {
            Box3DWorld.Instance.UnregisterStandaloneShape(this);
            if (_shape.IsValid)
            {
                Box3DWorld.Instance.UnregisterShape(_shape.Id);
            }
            if (_selfHandle.IsAllocated) _selfHandle.Free();

            // Only the self-created static body is ours to tear down; body-managed shapes are
            // released by their Box3DBody after it destroys the body.
            if (_ownBody.IsValid)
            {
                _ownBody.Destroy();
                ReleaseGeometry();
            }
        }

        protected ShapeDef BuildDef()
        {
            ShapeDef def = ShapeDef.Default;
            def.Density = Density;
            def.BaseMaterial.Friction = Friction;
            def.BaseMaterial.Restitution = Restitution;
            def.Filter.CategoryBits = 1UL << gameObject.layer;
            def.Filter.MaskBits = CollisionMaskForLayer(gameObject.layer);
            def.IsSensor = IsSensor;
            def.EnableSensorEvents = EnableSensorEvents;
            def.EnableContactEvents = EnableContactEvents;

            _selfHandle = GCHandle.Alloc(this);
            def.UserData = GCHandle.ToIntPtr(_selfHandle);
            return def;
        }

        // Builds a box3d mask from Unity's layer collision matrix: bit L is set for every layer
        // this layer is allowed to collide with.
        internal static ulong CollisionMaskForLayer(int layer)
        {
            ulong mask = 0;
            for (int other = 0; other < 32; other++)
            {
                if (!Physics.GetIgnoreLayerCollision(layer, other)) mask |= 1UL << other;
            }
            return mask;
        }

        internal void AttachTo(Body body, float3 localPosition, quaternion localRotation, float3 scale)
        {
            _attachPosition = localPosition;
            _attachRotation = localRotation;
            _attachScale = scale;
            if (_ownBody.IsValid && _ownBody.Id.ToUInt64() != body.Id.ToUInt64())
            { _ownBody.Destroy(); _ownBody = default; }
            _shape = CreateShape(body, localPosition, localRotation, scale);
            if (_shape.IsValid)
            {
                Box3DWorld.Instance.RegisterShape(_shape.Id, this);
                _shape.UserData = BuildDef().UserData;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Push Inspector edits to the live shape during play. SetDensity with updateBodyMass
            // re-derives mass from the current geometry, so size edits update mass too.
            if (!Application.isPlaying || !_shape.IsValid) return;
            _shape.SetFriction(Friction);
            _shape.SetRestitution(Restitution);
            UpdateLiveGeometry();
            _shape.SetDensity(Density, updateBodyMass: true);
        }
#endif

        /// <summary>Pushes edited geometry to the live native shape where the engine supports
        /// in-place replacement (sphere and capsule). Other shapes keep their creation geometry.</summary>
        protected virtual void UpdateLiveGeometry() { }

#if UNITY_EDITOR
        /// <summary>Creates this shape on a body in a throwaway preview world (rope editor
        /// preview), leaving component state alone. The body must already sit at this shape's
        /// transform pose.</summary>
        internal Shape CreateDetachedShape(Body body)
        {
            return CreateShape(body, float3.zero, quaternion.identity, transform.lossyScale);
        }

        /// <summary>Frees native geometry a detached preview shape allocated (mesh shapes).
        /// Called after the preview world is destroyed.</summary>
        internal virtual void ReleaseDetachedGeometry() { }
#endif

        protected abstract Shape CreateShape(Body body, float3 localPosition, quaternion localRotation, float3 scale);

        internal virtual void ReleaseGeometry() { }

        protected float3 ShapeCenter(float3 localPosition, quaternion localRotation, float3 scale)
        {
            return localPosition + math.mul(localRotation, LocalCenter * scale);
        }

        public Vector3 GetClosestPoint(Vector3 point)
        {
            var body = _shape.GetBody();
            var bodyPos = body.Position;
            var bodyRot = body.Rotation;
            var localPt = math.mul(math.inverse(bodyRot), (float3)point - bodyPos);
            var localResult = ClosestPointLocal(localPt);
            var worldResult = bodyPos + math.mul(bodyRot, (float3)localResult);
            return worldResult;
        }

        protected virtual Vector3 ClosestPointLocal(float3 localPoint)
        {
            return transform.position;
        }

        private static readonly Color GizmoColor = new Color(0.5f, 0.9f, 0.6f, 0.9f);

        protected void SetGizmoFrame()
        {
            Gizmos.color = GizmoColor;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        }

        protected float3 ScaledCenter => (float3)Center * (float3)transform.lossyScale;
    }
}
