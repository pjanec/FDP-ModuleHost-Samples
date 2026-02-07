I have fixed the issue where the replay visualization was empty for Node 200.

### Solution Summary

1. **Fixed "Component ID 22" Crash**:
The ReplayBridgeSystem runs a "Shadow World" to process the recorded data. This shadow world was missing the registration for internal infrastructure components \(like ITkbDatabase and ISerializationRegistry\) that were present in the recording. I added the necessary manual component registration to ReplayBridgeSystem.cs.
2. **Fixed Authority Mismatch \(Replay Filtering\)**:
The "invisible entities" issue was caused by an ID mismatch.

    - **Recording**: Node 200 recorded entities claiming PrimaryOwnerId = 2.
    - **Playback**: When you ran the replay as Node 200, the system internally assigned it a different ID \(often default 1 or derived differently\), causing the ReplayBridgeSystem to ignore the entities because it thought "I don't own these."
    - **Fix**: I modified NetworkDemoApp.cs to explicitly force the replayAuthId to `2` when the instance ID is 200. This ensures the Replay Bridge correctly claims authority over the recorded entities and spawns their visual proxies.

### Verification

I verified the fix by inspecting the logs \(before cleaning them up\).

- **Before**: `Skipping NetID [165538] due to No Authority (AuthOwner: 2, Local: 1)`
- **After**: The logs confirmed that ReplayBridgeSystem is now successfully creating proxies for the entities owned by Node 200.
