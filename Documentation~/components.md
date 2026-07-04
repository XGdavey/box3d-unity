# Component layer (experimental)

> **Experimental (0.2.x).** A MonoBehaviour layer that lets you author physics in the Inspector
> instead of writing C#, mirroring Unity's Rigidbody/Collider model. It covers bodies plus sphere
> and box shapes today; capsules/hulls/meshes, compound (child) shapes, and joints are not wired up
> yet. The pointer-level API in the rest of these docs remains the full-featured path. Lives in a
> separate `Box3d.Hybrid` assembly.

If you know Unity's physics components, you already know these:

| Component | Unity analog | Role |
|---|---|---|
| `Box3dWorld` | the physics scene | Owns the simulation, steps it, syncs Transforms. Auto-created. |
| `Box3dBody` | `Rigidbody` | A physics body: static / kinematic / dynamic. |
| `Box3dSphereShape` | `SphereCollider` | A sphere shape on a body. |
| `Box3dBoxShape` | `BoxCollider` | A box shape on a body. |

## Quick start

1. Create a GameObject, add **Box3dBody** (leave it Dynamic) and **Box3dSphereShape**. Put it a few
   metres up.
2. Make a floor: another GameObject with **Box3dBody** set to **Static** and **Box3dBoxShape**,
   scaled wide and flat.
3. Press play. The sphere falls and lands. No `Box3dWorld` needed — one is created automatically.

Add a `MeshRenderer` (or start from a primitive) to see the objects; the components drive the
Transform, so any visual on the same GameObject follows.

## Box3dWorld

Optional — placed automatically the first time a body needs it. Add one explicitly to tune:

- **Gravity** — the world gravity vector.
- **Sub Step Count** — solver sub-steps per step (higher = stiffer joints/stacks, slower).
- **Worker Count** — physics threads; 0 = auto (about half the logical cores).

Only one world is used; a second `Box3dWorld` logs a warning.

## Box3dBody

The body type mirrors Unity exactly:

- **Static** — never moves. Floors, walls, level geometry. Cheapest.
- **Kinematic** — moved by *its Transform*, ignores gravity and forces, but still pushes dynamic
  bodies out of the way. Moving platforms, doors, player-driven objects.
- **Dynamic** — moved by the solver: gravity, collisions, forces.

Behaves like a Unity component:

- **Enabling/disabling** the component (or its GameObject) removes it from / returns it to the
  simulation without recreating it. A disabled body is frozen *and non-solid* — other objects pass
  through it.
- **Moving from code**: set `Box3dBody.Position` / `Box3dBody.Rotation` (like `Rigidbody.position`),
  which teleports the body and wakes it. Don't set `transform.position` directly on a dynamic body
  — same advice as Unity. (In the editor, dragging the Transform in the Scene view during play does
  work, as a convenience.)
- **Changing the type at runtime** (`Box3dBody.BodyType = …`, or the Inspector during play) re-types
  the live body.

## Shapes

Add a shape component to the *same GameObject* as the body. Material fields live on the shape:

- **Density** (kg/m³) — determines mass; baked at creation.
- **Friction**, **Restitution** (bounciness) — 0–1; changeable at runtime via
  `SetFriction` / `SetRestitution` or the Inspector during play.
- **Center** — local offset from the body origin (like `Collider.center`).

Restitution notes that match the engine, not the component:

- Two surfaces combine as **max** — a bouncy ball bounces off a non-bouncy floor.
- Bounces only apply above the world's **restitution speed threshold** (~1 m/s), so gently settling
  objects stop bouncing. That's deliberate — it prevents endless micro-jitter.

`transform.lossyScale` is baked into shape dimensions at creation (spheres use the largest axis, as
Unity does).

## Current limits

- Shapes are read from the body's own GameObject only — child-collider compounds aren't gathered yet.
- Sphere and box shapes only; a shape with no body does nothing (auto-static bodies are planned).
- No joint components yet — use the code API for joints.

For anything beyond this, drop to the code API ([getting started](getting-started.md)) — the
components and the API share the same world and interoperate (`Box3dBody.Body`, `Box3dWorld.World`).
