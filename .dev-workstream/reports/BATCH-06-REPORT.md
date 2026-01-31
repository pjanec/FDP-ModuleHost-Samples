I have completed the tasks designated in BATCH-06.

**Summary of Changes:**

1. **Geographic Module Extraction \(EXT-3-3, EXT-3-4\)**:

    - Created Fdp.Modules.Geographic project.
    - Moved WGS84Transform logic from Core.
    - Implemented `GeographicModule` to register systems.
    - Added comprehensive unit tests in Fdp.Modules.Geographic.Tests.
    - Verified all tests pass.
2. **Battle Royale Decoupling \(EXT-4-1\)**:

    - Created local copies of components \(Position, Velocity, Health\) in Components to remove dependency on Core components.
    - Refactored Position and Velocity to use System.Numerics.Vector3 for better performance and API consistency.
    - Updated all systems in BattleRoyale \(AIModule, PhysicsModule, NetworkSyncModule, EntityFactory\) to use the new Vector3 based components.
    - Verified the example project builds and runs successfully.

The Fdp.Examples.BattleRoyale compilation issues caused by the component structure update have been fully resolved.

You can verify the work by inspecting EXTRACTION-SUMMARY-BATCH-06.md or running the Battle Royale example yourself.