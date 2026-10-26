# Gestural Boids prototype

## Current game: integrate the shadow

The weapon now uses a shorter (~1.2-unit) gently recurved profile inspired by the supplied reference: white cutting face, dark spine, small guard, shaped grip/flared pommel and inset blade line. Assembly, the tip trail, and swept collision follow the same curved centerline. This model change does not alter tracking filters or phone rotation-source behavior. The next-pass responsiveness and art priorities are recorded in PROJECT_PLAN.md; the user will test in a more suitable room later.

The default is now the 20-bird restoration encounter, not the legacy particle/orb demo. Enter Play, finish/check Room Setup, then press Tab. Twenty procedural low-poly birds orbit the chest without following the hand. A phone snap, Space, or 2 calls five into a rigid sword; an eight-second start button is available so you can step back from the keyboard. While combat is active, form switching is locked.

The remaining fifteen original birds gradually turn red. One climbs, shows a red dive line, then dives along a committed path. Strike through the incoming bird. Swept blade collision is retained, and deliberate swings gradually gain up to 14cm of extra radius beyond the 22cm base tolerance; a short white trail makes swings readable. During a real swing, the active attacker can also be hit while climbing/warning. A struck bird falls, folds its wings and waits injured on the floor instead of becoming immediately peaceful. Misses produce a hit cue/count without a forced reset; dodges let the bird retry. Tracking loss/Setup pauses combat and re-telegraphs interrupted dives.

After all fifteen are grounded, the sword unfolds into five loyal birds and meditation begins. Sit or kneel at least roughly 28cm lower than the observed standing chest height, keep both hands visible/resting below the chest near the lap, and settle. After two calm seconds, about 18 seconds of accumulated stillness revives the grounded birds progressively into white flight above you. Then all twenty spiral into the avatar, which becomes white, and a player-centered glow opens into a white room over four seconds. Movement gently reduces meditation progress; tracking loss pauses the sequence. An optional standing-meditation checkbox bypasses only the height condition. Pose recognition is a first-pass height/hand/stillness heuristic, not a formal seated-pose classifier.

Combat hides debug panels automatically; H restores controls. The eight-second meditation-ending preview lets you test the ending without replaying combat. New-round/reset buttons remain available. Phone snaps do not automatically restart the completed ending. The avatar now has bounded limb lengths and parent-relative fallbacks for missing joints rather than indefinitely stretched limbs; birds have varied heights/radii and smoothed flight. These refinements are awaiting user testing. Microphone, touch-region controls, ranged attacks and bird-shield defense remain deferred. Set Use Bird Encounter off before Play only to deliberately access the old particle demo.

Combat timing, blade alignment, hit tolerance and visuals are awaiting user testing. No automated Unity Play session was run for this change.

Open GesturalBoids.unity and press Play. If no scene exists yet, use **Boids > Create Prototype Scene**.

Play now starts in the actual-room Setup view. **Tab** switches between Setup and Play. Setup displays the raw depth room image with matching aspect ratio, projected skeleton, mirror control, stream ages and performance readings; it pauses flock simulation. Optional color display requires KinectManager > Get Color Frames = Color Texture before entering Play. No color capture setting or sensor restart is changed automatically. Calibration edits take effect in the current session and persist when Save Room Calibration is pressed. Room image/skeleton coordinates remain independent of avatar recentering. See PROJECT_PLAN.md at the project root for stage status and CLEANUP_AUDIT.md for the initial non-destructive asset inventory.

### Saved floor calibration
In Setup's Room Calibration controls, mark three spaced floor points on live depth, forming a broad triangle. Then click two measured 1m floor markers to inspect sensor distance and stage scale. Keep the default 1:1 scale unless an intentional correction is needed; the explicit marker-to-one-unit button changes scale and invalidates previous bounds. Pick two opposite corners of a clear floor rectangle and save the room. Picks automatically switch to depth and reject stale/no-depth pixels. Floor picks must be plausibly level relative to the sensor's current world mapping; bounds picks must lie near that floor. This is manual calibration, not room reconstruction.

Saved floor, uniform scale and bounds use local Unity PlayerPrefs and load on the next Play session. Calibrated mapping levels the floor at Y=0 and bypasses legacy depth compression/per-axis scaling. R retains its eight-second delay and recenters only player X/Z; it does not rewrite the room floor or bounds. Green projected bounds and inside/outside messages are advisory. Move the sensor or change its pose estimation settings only with a subsequent recalibration. Floor/marker points are stored in the sensor's world frame, so changing that frame can invalidate a saved setup. Temporary chest-centered mode remains available without erasing the saved room.

Cleanup is currently on hold. Calibration behavior is implemented and awaiting the user's manual test.

## Preview controls
- Mouse: move the weapon hand on the world Z=0 plane.
- Space: cycle Swarm > Sword > Orb. Keys 1/2/3 select directly.
- C: make the current phone orientation neutral. Hold the phone upright first.
- R: start an eight-second centering countdown so you can step away from the keyboard. M: toggle horizontal mirroring and schedule the same countdown. Centering waits for a live chest sample after the countdown.
- P: explicitly switch between mouse preview and Kinect after acquiring a body.
- O: show/hide physical space overlay. H: show/hide the compact controls panel.
- Hold Q: charge. Release Q after full charge to fire. F fires immediately.
- In Orb form with Kinect tracking: bring hands within 0.35 m for 1.2 seconds, then separate beyond 0.65 m to release an experimental energy burst.

## Phone
Default: automatic **JSON or OSC / UDP** detection, computer's LAN IP, port **7777**, ACCEL and QUATERNION enabled. GYRO is supported as a fallback when quaternion data is absent. The old tennis receiver is not needed. Keep both devices on the same network. If prompted by Windows, allow Unity on your private network.

Automatic mode accepts OSC messages and bundles with addresses ending in `/accel` (x,y,z), `/gyro` (x,y,z), or `/quaternion` (x,y,z,w), including device IDs and different capitalization. Turn off Auto Detect Protocol to select a strict JSON/OSC protocol and custom address patterns. Do not run the tennis receiver on the same port. The HUD displays packet count, last format/address, gyro values and quaternion count: zero packets indicates a network/IP/port issue; packets without rotation indicate missing sensors or unrecognized addresses. Test rotation in Sword mode and press C while holding the phone neutral. Quaternion gives absolute orientation; gyro-only rotation integrates angular rate and can drift. Gyro In Radians defaults on; Gyro Axis Signs and Orientation Offset Euler allow device-axis tuning.

Snap defaults: acceleration magnitude >= 2.4 triggers; below 1.2 rearms; 0.8-second cooldown. These are raw sensor units, so use the on-screen reading to tune for your device. Slow swings should stay below threshold. Orientation mapping is initially direct quaternion mapping with neutral calibration; device axis conventions may need adjustment after hardware testing.

## Kinect
Enable the scene's Kinect Rig when an Azure Kinect is connected. The controller uses the first tracked body and right hand by default. Left Handed selects the other hand. A faceted white mannequin is generated at Play time. Avatar and weapon use the exact same mapped joints. BoidBodyPose and BoidAvatar components are added automatically to existing scenes; no scene rebuild is needed.

Auto Center anchors the chest's initial sensor position at stage (0, 1.35, 0), preserves lateral and vertical movement in meters, and compresses depth to 0.35 so changing distance does not shrink the interaction. R centers again; M reverses horizontal mapping. Tracking Scale controls each axis (default 1:1); Tracking Offset applies only when Auto Center is disabled. BoidAvatar frames the main camera orthographically for consistent size; disable Frame Camera to use your own view. Swarm Hand Influence now defaults to 0.85; formed weapons move directly with the mapped hand while particles retain their shape animation.

Joints update independently, including inferred joints. An occluded free hand does not discard the weapon hand. Missing joints hold their last position, with a 0.7-second confidence grace period. After losing a previously acquired body, the avatar and weapon hold instead of jumping to mouse preview. P explicitly enables preview. Automatic charge/release requires both hands to be recent. The HUD distinguishes live tracking from held poses and shows sensor-frame age and raw/mapped hand coordinates. If the sensor stops reporting a body at the edge of its physical view, coordinate scaling cannot recover it: center yourself/step back and use the live diagnostics to distinguish that from screen framing.

## Scope
420 particles implement local separation, alignment and cohesion in swarm mode, and spring toward sword/orb targets in formed modes. The current CPU neighbor search is quadratic: start with the default count before increasing it. Shape transitions preserve particle positions and velocities. Release produces a temporary traveling particle burst, then recalls particles to the current form.

This is a first interaction prototype. Rasenshuriken, throw recognition, collisions, sound, trails, and advanced pose classification are future work. The two-hand burst currently recognizes distance and duration, not a complete Kamehameha pose. Verify physical sensor alignment and snap thresholds before relying on gesture recognition.

## Validation
Unity 6000.2.13f1 compiled the project and generated the scene. A real Play-mode smoke check passed using loopback JSON/UDP packets: first snap selects Sword, sustained high acceleration does not retrigger even after cooldown, and a settled second snap selects Orb. The check also exercised burst/recall, verified live particle count and finite positions, and confirmed that disabling phone input releases UDP port 7777. Swarm, sword and orb camera renders were captured in Logs and visually inspected for the formed states. Physical Kinect/phone input, OSC mode and automatic pose recognition still require live testing. Existing third-party deprecation/duplicate-assembly warnings remain.

Boid Count takes effect on the next Play session; other behavior settings can be tuned during Play.

## Physical-space and connection diagnostics
The controls panel lists active adapter IPv4 addresses, the listening UDP port (editable during Play), the last sender, phone packet count, and a local loopback check. Use the address of the PC adapter on the phone's network as ZigSim's destination, with UDP and the matching port. Both JSON and OSC work in automatic mode. Enable sensors and start streaming in ZigSim. A passed loopback check confirms local reception only; it does not rule out LAN/firewall/phone-permission issues. Self-check packets do not count as phone packets. Restart Receiver rebinds the port and refreshes addresses. Detailed readings include a short packet preview to identify format changes once packets arrive.

The right-side physical overlay shows a top-down sensor-space meter grid, estimated depth-camera coverage, and an adjustable green play-area rectangle. Yellow marks a live body; red marks the last known body position. The cone uses reported depth-camera horizontal/vertical FOV when available and labels a manual estimate otherwise. The pinhole cone is approximate, especially with wide-angle distortion; body tracking may fail inside it. Near/far/width bounds are visual calibration guides, not enforced tracking restrictions. The center button also recenters the avatar. These temporary bounds reset on the next Play session.

A downsampled raw depth preview shows the actual sensor image independently of body tracking. Compare depth age and body-sample age to distinguish a lost body from a stalled depth stream. The scene also draws a front-facing 0.5-stage-unit grid and mapped camera frustum. Only the top-down map and raw sensor distances represent uncompressed physical meters; the scene frustum follows the avatar's depth compression and tracking scale. No automatic validation was run for these changes, per the requested manual-testing workflow.

The hotspot was classified as Public, and an enabled Public inbound block targeted this exact Unity 6000.2.13f1 executable. With approval, Tools/Fix-BoidsFirewall.ps1 disabled that conflicting block and added an inbound UDP 7777 allowance limited to this Unity executable and LocalSubnet on Private/Public profiles. Logs/BoidsFirewallFix.log records the result and original block-rule ID. If you change the phone port, this firewall allowance still applies only to 7777. Phone delivery remains a live manual check.
