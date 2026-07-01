# Project Bogey conventions
## _MAY BE OUTDATED_

Most of this is enforced automatically, so it doesn't all require me being vigilant at all times. However, there are some things that could be seen only during review, those are in the review section below.

## Code style

Enforced by `.editorconfig` + analyzers.Check them out yourself if you really want to. They're gonna be caught either way during the CI if required to.

## Architecture rules **(review)**

- Sealed classes by default. You really should only unseal if there's a real reason.
- All fields and auto-properties come before any methods in a class.
- Components are DATA ONLY. All the logic is in systems.
- Inter-system comms go through the event bus.
- Organize by feature. Folders are `Components/`, `Systems/`, `Prototypes/`, `Engine/`, `Content/`, `Tracks/`, `Events/`. No `misc`/`util`/`helpers`.
- Reused logic becomes a shared thing (e.g. `DetectionMath`, `PrototypeFactory`), so no copy pasting around!!!

## Determinism (not optional)

Same seed + same scenario means an identical run. This is for replays and networking in the future.

- One seeded `System.Random` is injected into the sim and is the only source of randomness.
- Time is only counted by `SimClock` (1 tick = 1 simulated second).
- Entity queries return ids in ascending order, and prototypes load in filename order, so the RNG draw sequence is stable across runs and machines.

## The minimal engine

For now it is quite thin (intentionally), but it'll grow. an entity is just an `int`. `EntityManager` keeps components in typed per component collections. `SystemManager` injects dependencies into `[Dependency]` marked private readonly fields by reflection at startup and then runs the systems in registration order each tick
