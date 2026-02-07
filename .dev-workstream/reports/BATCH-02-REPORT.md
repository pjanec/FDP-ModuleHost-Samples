The `FDPLT_016` test is now **passing**.

The root cause of the failure was twofold:

1. **Incorrect Authority Setup**: Node A was configured to believe the owner of the Turret was ID `1`. Since Node A's local internal ID is `1`, it treated itself as the owner and ignored updates from Node B.
    - *Fix*: Updated OwnershipTests.cs to set the owner on Node A to `2` \(the Internal ID of Node B relative to Node A\).
2. **Missing Entity Registration**: The test uses a manual SpawnTank\(\) helper that creates the entity directly in the World, bypassing the normal network spawn process. As a result, the entity was **not registered** in the NetworkEntityMap on Node A. When Node A received TurretState updates from Node B, the GenericDescriptorTranslator couldn't find the target entity and silently dropped the data.
    - *Fix*: Exposed NetworkEntityMap in NetworkDemoApp and updated the SpawnTank extension method to explicitly register the new entity in the map.

### Summary of Changes

**1. NetworkDemoApp.cs**
Exposed the NetworkEntityMap so test helpers can register manually created entities.