using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Box3D.Hybrid
{
    public interface IBox3DSensorHandler { Box3DShape SensorShape { get; } void OnBox3DSensorEnter(Box3DShape other); void OnBox3DSensorExit(Box3DShape other); }

    public static class Box3DQuery
    {
        public const int MaxHits = 64;

        public static bool Raycast(Vector3 origin, Vector3 direction, out Box3DRaycastHit hit, float maxDistance, int layerMask)
        {
            hit = default;
            var world = Box3DWorld.Instance.World;
            var filter = QueryFilterForMask(layerMask);
            RayResult result = world.CastRayClosest(origin, direction * maxDistance, filter);
            if (!result.Hit) return false;
            hit = new Box3DRaycastHit
            {
                point = result.Point,
                normal = result.Normal,
                fraction = result.Fraction,
                shapeId = result.ShapeId,
                hit = true,
            };
            return true;
        }

        public static int RaycastAll(Vector3 origin, Vector3 direction, Box3DRaycastHit[] results, float maxDistance, int layerMask)
        {
            var world = Box3DWorld.Instance.World;
            var filter = QueryFilterForMask(layerMask);
            Span<RayHit> hits = stackalloc RayHit[MaxHits];
            int count = world.CastRay(origin, direction * maxDistance, filter, hits);
            int resultCount = Mathf.Min(count, results.Length);
            for (int i = 0; i < resultCount; i++)
            {
                results[i] = new Box3DRaycastHit
                {
                    point = hits[i].Point,
                    normal = hits[i].Normal,
                    fraction = hits[i].Fraction,
                    shapeId = hits[i].ShapeId,
                    hit = true,
                };
            }
            return resultCount;
        }

        public static int OverlapSphere(Vector3 position, float radius, Box3DRaycastHit[] results, int layerMask)
        {
            var world = Box3DWorld.Instance.World;
            var filter = QueryFilterForMask(layerMask);
            Span<ShapeId> shapeIds = stackalloc ShapeId[MaxHits];
            Span<float3> proxy = stackalloc float3[1];
            proxy[0] = float3.zero;
            int count = world.OverlapShape(position, proxy, radius, filter, shapeIds);
            int resultCount = Mathf.Min(count, results.Length);
            for (int i = 0; i < resultCount; i++)
            {
                results[i] = new Box3DRaycastHit { shapeId = shapeIds[i], hit = true };
            }
            return resultCount;
        }

        internal static QueryFilter QueryFilterForMask(int layerMask)
        {
            return new QueryFilter { CategoryBits = ulong.MaxValue, MaskBits = (ulong)layerMask };
        }

        public static Box3DShape ShapeIdToComponent(ShapeId shapeId)
        {
            return Box3DWorld.Instance.GetShapeComponent(shapeId);
        }
    }

    public struct Box3DRaycastHit
    {
        public Vector3 point;
        public Vector3 normal;
        public float fraction;
        public ShapeId shapeId;
        public bool hit;

        public Box3DShape ShapeComponent => hit ? Box3DWorld.Instance.GetShapeComponent(shapeId) : null;
        public T GetComponentInParent<T>() => ShapeComponent ? ShapeComponent.GetComponentInParent<T>() : default;
    }

    /// <summary>Scene-level owner of a Box3D simulation world. Step() must be called externally
    /// (by ManualPhysicsController.OnAnimatorMove) at a fixed tick rate. Syncs kinematic and
    /// dynamic bodies before the step writes moved bodies back after.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class Box3DWorld : MonoBehaviour
    {
        [SerializeField, Tooltip("Gravity vector applied to every dynamic body.")]
        private Vector3 Gravity = new Vector3(0f, -9.81f, 0f);

        [SerializeField, Min(1), Tooltip("Solver sub-steps per step. Higher = stiffer, slower.")]
        private int SubStepCount = 4;

        [SerializeField, Min(1), Tooltip("Box3D worker threads. 1 = deterministic, single-threaded. Use 1 for rollback netcode.")]
        private int WorkerCount = 1;

        /// <summary>Set to true during rollback replay to prevent LateUpdate from pushing Transform changes to the body.</summary>
        public static bool IsReplaying;

        [SerializeField, Tooltip("Overlay physics debug geometry in the Scene view each frame (None = off). " +
            "Enable Contacts / Islands / Forces / Bounds to see the solver's view of the world. " +
            "For the Game view, turn on its Gizmos toggle.")]
        private DebugDrawFlags DebugDraw = DebugDrawFlags.None;

        [SerializeField, Min(1f), Tooltip("Half-size of the box around the origin that debug drawing covers.")]
        private float DebugDrawRadius = 200f;

        // Only kinematic bodies need per-frame attention (they follow their Transform). Dynamic
        // bodies sync back through move events — which report only bodies that actually moved — so
        // they never appear here. Bodies map back to their component through a GCHandle in userData,
        // so no all-bodies list is kept.
        private readonly List<Box3DBody> _kinematicBodies = new List<Box3DBody>();
        private readonly List<Box3DBody> _dynamicBodies = new List<Box3DBody>();
        private readonly List<Box3DShape> _standaloneShapes = new List<Box3DShape>();
        private readonly List<IBox3DSensorHandler> _sensorHandlers = new List<IBox3DSensorHandler>();
        private readonly Dictionary<ulong, Box3DShape> _shapeMap = new Dictionary<ulong, Box3DShape>();

        private World _world;
        private Body _anchor;
        private Vector3 _lastGravity;
        private static Box3DWorld _instance;

        /// <summary>The underlying Box3D world (valid after this component is enabled).</summary>
        public World World => _world;

        /// <summary>When true, the world stops stepping (bodies stay put). The visual replayer sets this
        /// so live physics doesn't fight the replayed transforms.</summary>
        public bool Paused { get; set; }

        /// <summary>The configured gravity vector (readable without a live world — the rope's
        /// editor preview settles under the same gravity the simulation will use).</summary>
        public Vector3 GravityVector => Gravity;

        /// <summary>A shared static body at the origin, used as the fixed endpoint for joints whose
        /// connected body is null (like Unity's null connectedBody = attach to the world).</summary>
        public int WorldSubStepCount => SubStepCount;
        public Body WorldAnchor
        {
            get
            {
                if (!_anchor.IsValid)
                {
                    EnsureCreated();
                    _anchor = _world.CreateBody(BodyDef.Default);
                }
                return _anchor;
            }
        }

        /// <summary>The active world component, creating one if the scene has none.</summary>
        public static Box3DWorld Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = FindAnyObjectByType<Box3DWorld>();
                    if (!_instance)
                    {
                        _instance = new GameObject("Box3D World").AddComponent<Box3DWorld>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance && _instance != this)
            {
                Debug.LogWarning("[Box3D] Multiple Box3DWorld components — only the first is used.", this);
            }
            _instance = this;
            EnsureCreated();
        }

        public void EnsureCreated()
        {
            if (_world.IsValid) return;

            WorldDef def = WorldDef.Default;
            def.Gravity = Gravity;
            def.WorkerCount = (uint)WorkerCount;
            _world = World.Create(def);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Push Inspector edits to the live world during play. SubStepCount and DebugDraw are
            // read every frame anyway; WorkerCount is baked at world creation.
            if (!Application.isPlaying || !_world.IsValid) return;
            _world.SetGravity(Gravity);
        }
#endif

        internal void AddKinematic(Box3DBody body)
        {
            if (!_kinematicBodies.Contains(body)) _kinematicBodies.Add(body);
        }

        internal void RemoveKinematic(Box3DBody body)
        {
            _kinematicBodies.Remove(body);
        }

        internal void RegisterStandaloneShape(Box3DShape shape) { if (!_standaloneShapes.Contains(shape)) _standaloneShapes.Add(shape); }
        internal void UnregisterStandaloneShape(Box3DShape shape) { _standaloneShapes.Remove(shape); }

        internal void AddDynamic(Box3DBody body)
        {
            if (!_dynamicBodies.Contains(body)) _dynamicBodies.Add(body);
        }

        internal void RemoveDynamic(Box3DBody body)
        {
            _dynamicBodies.Remove(body);
        }

        public void UnsyncBody(Box3DBody body) { _kinematicBodies.Remove(body); _dynamicBodies.Remove(body); }

        public int CollectDynamicBodies(ref Box3DBody[] buffer)
        {
            if (buffer == null || buffer.Length < _dynamicBodies.Count)
                buffer = new Box3DBody[_dynamicBodies.Count];
            _dynamicBodies.CopyTo(buffer);
            return _dynamicBodies.Count;
        }

        internal void RegisterShape(ShapeId shapeId, Box3DShape shape)
        {
            _shapeMap[shapeId.ToUInt64()] = shape;
        }

        internal void UnregisterShape(ShapeId shapeId)
        {
            _shapeMap.Remove(shapeId.ToUInt64());
        }

        public Box3DShape GetShapeComponent(ShapeId shapeId)
        {
            _shapeMap.TryGetValue(shapeId.ToUInt64(), out var shape);
            return shape;
        }

        public void RegisterSensorHandler(IBox3DSensorHandler h) { if (!_sensorHandlers.Contains(h)) _sensorHandlers.Add(h); }
        public void UnregisterSensorHandler(IBox3DSensorHandler h) { _sensorHandlers.Remove(h); }

        public void Step(float deltaTime)
        {
            if (Paused || !_world.IsValid) return;

            if (_lastGravity != Gravity)
            {
                _lastGravity = Gravity;
                _world.SetGravity(Gravity);
            }

            for (int i = 0; i < _kinematicBodies.Count; i++)
            {
                Box3DBody body = _kinematicBodies[i];
                if (body) body.SyncFromTransform();
                if (body) body.PushKinematic(deltaTime);
            }

            for (int i = 0; i < _standaloneShapes.Count; i++)
            {
                Box3DShape shape = _standaloneShapes[i];
                if (shape) shape.SyncTransform();
            }

            for (int i = 0; i < _dynamicBodies.Count; i++)
            {
                Box3DBody body = _dynamicBodies[i];
                if (body) body.SyncFromTransform();
            }

            _world.Step(deltaTime, SubStepCount);

            foreach (BodyMoveEvent moveEvent in _world.GetBodyMoveEvents())
            {
                if (moveEvent.UserData == IntPtr.Zero) continue;
                if (GCHandle.FromIntPtr(moveEvent.UserData).Target is Box3DBody body && body.BodyType == Box3DBodyType.Dynamic)
                {
                    body.ApplyMoveEvent(moveEvent.Transform);
                }
            }

            if (_sensorHandlers.Count > 0)
            {
                SensorEvents events = _world.GetSensorEvents();
                foreach (var begin in events.Begin)
                {
                    var a = GetShapeComponent(begin.SensorShapeId); var b = GetShapeComponent(begin.VisitorShapeId);
                    if (!a || !b || !a.enabled || !b.enabled) continue;
                    for (int hi = 0; hi < _sensorHandlers.Count; hi++) { var h = _sensorHandlers[hi]; if (h.SensorShape == a) h.OnBox3DSensorEnter(b); else if (h.SensorShape == b) h.OnBox3DSensorEnter(a); }
                }
                foreach (var end in events.End)
                {
                    var a = GetShapeComponent(end.SensorShapeId); var b = GetShapeComponent(end.VisitorShapeId);
                    if (!a || !b || !a.enabled || !b.enabled) continue;
                    for (int hi = 0; hi < _sensorHandlers.Count; hi++) { var h = _sensorHandlers[hi]; if (h.SensorShape == a) h.OnBox3DSensorExit(b); else if (h.SensorShape == b) h.OnBox3DSensorExit(a); }
                }
            }
        }

        private void LateUpdate()
        {
            // Debug overlay: Box3D emits its geometry through the debug-draw bridge as Scene-view lines.
            // Drawn after the step + transform sync so it reflects the current pose.
            if (DebugDraw != DebugDrawFlags.None && _world.IsValid)
            {
                _world.DrawDebug(DebugDraw, DebugDrawRadius);
            }
        }

        // Matches the world component's purple icon so the arrow reads as "the world's gravity".
        private static readonly Color GravityGizmoColor = new Color(0.75f, 0.53f, 0.92f, 0.95f);

        private void OnDrawGizmosSelected()
        {
            float magnitude = Gravity.magnitude;
            if (magnitude < 1e-4f) return; // zero gravity — nothing to point at

            Vector3 origin = transform.position;
            Vector3 dir = Gravity / magnitude;
            // Shaft length tracks strength (1 g ≈ 1.5 m), clamped so extreme values stay readable.
            float length = Mathf.Clamp(1.5f * magnitude / 9.81f, 0.4f, 4f);
            Vector3 tip = origin + dir * length;

            // A basis perpendicular to the arrow for the head fins (gravity is usually straight
            // down, where Vector3.up is degenerate — fall back to right).
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(dir, Vector3.right);
            side.Normalize();
            Vector3 side2 = Vector3.Cross(dir, side);

            Gizmos.color = GravityGizmoColor;
            Gizmos.DrawLine(origin, tip);
            float head = Mathf.Min(0.3f * length, 0.4f);
            foreach (Vector3 fin in new[] { side, -side, side2, -side2 })
            {
                Gizmos.DrawLine(tip, tip - dir * head + fin * (head * 0.5f));
            }
        }

        private void OnDestroy()
        {
            if (_world.IsValid) _world.Destroy();
            if (_instance == this) _instance = null;
        }
    }
}
