# Gestural Birds — implementation checklist

This is the working checklist for this project. Refer to it before each implementation stage, update it as work progresses, and keep new decisions here rather than relying on chat history.

## Current control baseline — experiment retired (2026-10-26)

User rejected the procedural animation result and requested restoration of body tracking and non-assisted sword control. That baseline is restored; birds are now 25% larger. [ASSISTED_SWORD_PLAN.md](ASSISTED_SWORD_PLAN.md) is retired experiment history, not the next-work checklist. Keep tabletop C calibration, mirrored startup, optional room tools, 20-bird story and meditation. All 15 free birds remain hittable by actual blade contact, including multiple birds per swing. Animation assets/pose-data work is outside current scope. Cleanup remains on hold.

### Current flock polish — awaiting user test

- [ ] **Implemented:** multiple actual blade intersections can ground multiple birds in one swing; already-injured birds cannot be counted twice.
- [ ] **Implemented:** idle/fight orbit bands raised by 25cm and vertical spacing widened from 23cm to 30cm. Meditation's higher white-bird orbit remains unchanged.
- [ ] **Implemented:** a strike briefly pushes orbit targets outward by up to 38cm and upward by 8cm, then eases back over about 1.2 seconds through the existing smooth flight. Repeated hits refresh a capped pulse rather than stacking displacement. Grounded birds stay grounded; committed attacker paths remain readable.
- [ ] Test a sweep through nearby birds, check upper flock remains comfortably visible/reachable, and watch the survivors expand then return. Confirm reset clears the pulse and meditation still starts after all 15 are grounded.

**Status convention:** `[ ]` = pending; `[x]` = completed and checked. If implemented but awaiting the user's test, leave unchecked and add **Awaiting user test**. Record a short result or limitation under the relevant task. Do not mark hardware-dependent behavior complete based only on code inspection.

## Goal and creative direction

Current direction: **20 dynamically chest-orbiting birds → 5 fuse into a rigid sword → remaining 15 redden and attack → sword strikes injure/ground them → seated stillness revives white birds above the player → all 20 merge into the player → avatar turns white → light fills the room**. Theme: integrating the shadow. Injury is temporary; healing happens through meditation rather than the sword hit itself.

The earlier humanoid-enemy/microphone/vulture narrative is deferred. Keep its ideas below for a later chapter; do not introduce a separate enemy in this first encounter.

- Exactly 20 birds initially circle the chest, independently of hand movement. Five become the sword; the remaining fifteen continue circling and become the attackers.
- Equipment is made from part of the flock, visibly reducing the free birds. Attacking and defending redistribute available birds; returning birds replenish the flock.
- Visual inspiration: MadWorld's high-contrast black, white and gray, with selective red. Use original assets and compositions.
- Red communicates attack warnings, vulnerability, microphone activity, readiness and impacts; avoid constant red everywhere.
- The avatar is a white low-poly figure. Birds should have readable silhouettes; weapons should feel substantial.
- Keep the user inside a calibrated physical area. Bring the arena around them instead of requiring travel through the room.
- Set the Rasengan aside in favor of a microphone suited to this world.

## Working agreement

- [x] Save the plan as this Markdown checklist.
- [ ] At the start of each stage, consult this file and select the next small, useful change.
  - 2026-10-23: consulted for the first implementation stage; selected initial cleanup audit plus the actual-room Setup view. Continue this practice at every stage.
- [ ] After each change, give the user a short, specific manual test and record the result here.
- Do not create duplicate Unity projects or launch lengthy automated Play sessions. The user prefers to test directly in the open project.
- Use focused source/dependency checks when useful. Do not interrupt or close the user's Unity session.
- The user must approach the keyboard to press buttons. A close last-tracked position does **not** establish where they were testing. Use delayed calibration and live readings.
- Preserve existing working Kinect and phone input while iterating. Do not delete assets as part of merely documenting this plan.
- Mark completion only after the appropriate check; distinguish implemented work from user-confirmed behavior.

## Existing baseline

- [x] First swarm/sword/orb prototype, keyboard preview and initial burst implemented.
- [x] White procedural faceted avatar and shared avatar/weapon coordinate mapping implemented.
- [x] Independent joint updates, brief confidence grace period and last-pose hold implemented.
- [x] JSON/OSC phone reception, quaternion rotation and gyro fallback implemented.
- [x] Network diagnostics, raw depth preview, approximate sensor cone and top-down space map implemented.
- [x] Eight-second centering countdown implemented so the user can step back from the keyboard.
- [x] Fix the confirmed Windows firewall block for Unity 6000.2.13f1 on the Public hotspot network. User subsequently reported that input works great.

These establish a prototype, not final room calibration, art, combat or gesture tuning. Existing spring-based formations and charge/release behavior are to be replaced/refined below.

## 1. Project cleanup and performance baseline

**Cleanup ON HOLD at the user's request (2026-10-23). Do not remove/archive assets or trim packages until resumed. Keep the initial audit for later. Performance instrumentation may still be used.**

- [ ] Record performance with Kinect active: frame rate/frame time, flock count, and editor responsiveness. Identify actual bottlenecks before optimizing.
  - Instrumentation implemented: rolling FPS/average/peak frame time and flock-update CPU time. Setup pauses the flock; capture baseline in Play (Tab). **Awaiting user test.**
- [ ] Inventory project assets and dependencies; distinguish active game content, required packages/native libraries, old tennis content and unused examples.
  - Initial size inventory and selected serialized-reference scan recorded in `CLEANUP_AUDIT.md`. Full dependency/backend review remains pending. Nothing removed.
- [ ] Identify removable tennis scenes, racket/character models, music, demo scenes and other unused content.
- [ ] Prepare a recoverable backup/archive before removing old work. Check references before deletion.
- [ ] Remove only agreed obsolete content; preserve required Kinect SDK/model/native dependencies, extOSC and ZigSim functionality.
- [ ] Recheck sensor startup, phone reception, avatar and active scene after cleanup.
- [ ] Review package dependencies and remove only those demonstrably unused.
- [ ] Compare project/import/editor loading improvements with baseline.

**Distinction:** removing unused assets primarily reduces disk size and import/editor overhead. Gameplay gains require reducing active processing/rendering costs. Library/Temp are generated caches, not content to delete routinely for FPS.

## 2. Room view, physical calibration and scale

- [x] Create a dedicated Setup view dominated by the Kinect's **actual room image**, with skeleton and calibration guides over it. The existing schematic is not a substitute for seeing the walls, floor and ceiling.
  - Implemented in `BoidRoomSetup`: starts in Setup, Tab switches Setup/Play, flock simulation paused during Setup. User reported "this is much better" and requested the next step. Precise alignment/color checks remain below.
- [ ] Offer live color and depth viewing where available; align skeleton overlays correctly with the chosen image.
  - Depth view keeps the sensor aspect ratio and projects raw joints into depth-image coordinates. Optional color uses color-camera projection and requires Color Texture capture enabled before Play. No sensor settings/restarts changed automatically. **Awaiting user test, including image orientation/alignment.**
- [ ] Display freshness/confidence so held poses and stale frames are visibly different from live tracking.
  - Room view shows depth/color/body ages; skeleton green for tracked, yellow for inferred, red when stale/held. **Awaiting user test.**
- [ ] Choose a suitable room and sensor position/angle using the live room view. Keep feet, sideways reach and raised hands visible.
- [ ] Establish the floor plane and sensor height/orientation; identify walls, furniture and ceiling limits as needed. Do not assume automatic room geometry detection exists.
  - Three live-depth floor picks now define an explicit level plane and floor origin. Rejects collinear/nearly vertical selections; no wall/ceiling reconstruction. **Awaiting user test.**
- [ ] Set near/far/left/right usable bounds inside both clear physical space and reliable tracking coverage.
  - Two opposite floor picks define a room-aligned rectangle, projected onto the image in green; live inside/outside status is advisory. **Awaiting user test.**
- [ ] Use two measured floor markers one meter apart to verify **one physical meter = one Unity unit** initially.
  - Two image picks show sensor distance and scaled stage distance; scale changes only via an explicit button. Default is 1:1. **Awaiting user test.**
- [ ] Disable deliberate depth compression during calibration. Keep avatar, weapon and room references in one consistent mapping.
  - With a marked floor, a uniform room scale replaces legacy depth compression/per-axis scale. Floor is Y=0. Before marking a floor, legacy mapping remains available. **Awaiting user test.**
- [ ] Test center and boundaries: sideways movement, overhead reach, both arms extended, crouching and slow weapon gestures.
- [ ] Bring boundaries inward where tracking becomes unreliable. Sensor lens coverage is not a guarantee of body tracking.
- [ ] Save room calibration, scale and boundaries across sessions. Recenter the player separately without changing the saved room calibration.
  - Local save/load implemented with Unity PlayerPrefs. Saved floor/scale/bounds load on Play; R shifts player X/Z only. Moving/reorienting the sensor requires recalibration. **Awaiting user save/restart/recenter test.**
- [ ] Keep Play view focused on the avatar/game, with an optional small live room preview and hideable diagnostics.

**User check:** walk between measured markers and around the intended area; compare avatar alignment, room view and tracking status. Confirm full gesture range before tuning combat.

## 3. Intentional phone touch controls

- [ ] Receive and expose ZigSim touch data for JSON and OSC, checking actual coordinate ranges and touch lifecycle on the user's phone.
- [ ] Divide the touch surface into left **Defend**, right **Attack**, and a separate bottom **Form** strip. Decide boundary behavior and multi-touch handling from actual device data.
- [ ] Right-side fresh tap launches one group of birds; holding must not generate unintended repeated taps.
- [ ] Left-side hold gathers birds into a defensive screen; release returns them to orbit.
- [ ] A fresh touch on the Form strip plus a deliberate acceleration snap changes equipment once. Require finger lift and acceleration reset before another switch.
- [ ] Prevent attack/defense touches and gestures already underway from accidentally switching equipment.
- [ ] Add clear touch-region/action feedback. Avoid relying on a custom phone UI unless we decide to build one; ZigSim touch regions can initially be conceptual mappings shown in Unity.
- [ ] Handle stale packets/disconnection by safely releasing held controls without firing unintended actions.
- [ ] Tune thresholds, cooldowns and responsiveness with the user.

**User check:** repeatedly attack, hold/release defense and change forms while moving naturally. Confirm no accidental switches or stuck actions.

## 4. Living flock and rigid equipment

**First encounter implementation (2026-10-23):** `BirdRestorationEncounter` now replaces the 420-point simulation by default with 20 procedural low-poly birds, individually animated wings and chest-centered orbits. Five converge into a solid hand-attached sword over a fixed 1.1-second assembly. No spring lag in the completed blade. Remaining birds persist. **Awaiting user test** for silhouettes, allocation, assembly, hand tracking and scale. Legacy particle mode is retained behind `useBirdEncounter = false`; nothing deleted.

- [ ] Replace square point visuals with simple low-poly bird shapes: wedge-like bodies and animated wings.
- [ ] Orient birds along flight direction, vary wingbeat timing, and start with a readable flock size rather than hundreds of indistinct points.
- [ ] Establish the idle roost: birds orbit the whole avatar, react to movement, and optionally settle nearby.
- [ ] Define bird allocation for free orbit, weapon formation, outgoing attacks, defense and returning birds.
- [ ] Form equipment from a portion of the flock; keep the remaining birds present with either sword or microphone equipped.
- [ ] Build a short, fixed-duration bird-to-shard assembly animation.
- [ ] After assembly, lock the sword into a rigid shape attached to the weapon hand. Tip and hilt must move together; no spring lag in the completed blade.
- [ ] Give the sword a solid blade/silhouette underneath optional fragments, plus restrained orbiting birds and a brief swing trail.
- [ ] Make attack groups visibly peel off, travel, hit and return. Keep replenishment quick and readable initially.
- [ ] Make defense a wing-like screen on the threatened side. Introduce resource competition gently, with visible availability rather than an invisible meter.
- [ ] Apply the monochrome art direction consistently to avatar, birds and equipment, using red selectively.

**User check:** assess bird readability, persistent flock, visible allocation/return and immediate sword response during fast hand movement.

## 5. First fight — restore the flock

**Revision (2026-10-23):** User confirmed the first encounter works beautifully, then requested a shift from parry-restoration to strikes/injury followed by seated healing and integration. The older strike-immediately-restores bullets below describe the previous accepted version; they are superseded by section 5A. Touch/ranged/mic remain deferred. Cleanup remains ON HOLD.

- [ ] Begin in a quiet chest-centered orbit of exactly 20 birds. **Implemented; awaiting user test.**
- [ ] Sword activation assembles exactly five loyal birds; the remaining fifteen stay present. **Implemented; awaiting user test.**
- [ ] Original flock members gradually turn red, rather than spawning unrelated enemies. **Implemented; awaiting user test.**
- [ ] Begin with one attacker at a time: corrupt → climb → warning line → locked-path dive → recovery. Increase pace modestly as birds are restored. **Implemented; awaiting user test.**
- [ ] Use the visible rigid blade to intercept incoming birds, including swept-motion checks and a forgiving block radius. **Implemented; awaiting user test.**
- [ ] Successful blocks turn attackers white, return them to orbit, and permanently exclude them from further corruption for that round. **Implemented; awaiting user test.**
- [ ] Missed attacks produce a restrained red cue and hit count; dodges let attackers recover and retry. Initial training encounter has no forced death/reset. **Implemented; awaiting user test.**
- [ ] Pause attacks when body/weapon-hand tracking is lost or Setup opens; re-telegraph an interrupted dive before resuming. **Implemented; awaiting user test.**
- [ ] At 15 restored attackers, unfold the sword's five birds and reunite the full twenty. No automatic new round from a stray snap after victory. **Implemented; awaiting user test.**
- [ ] Offer quiet reset and an eight-second start/new-round countdown; existing phone snap/Space/2 can call the sword from idle. Combat locks switching until completion/reset. **Implemented; awaiting user test.**
- [ ] Tune attack readability, block tolerance, pace, positioning and reward feedback with actual Kinect/phone movement.
- [ ] Add richer environment/narrative transitions, audio and ink effects after the basic encounter feels dependable.

Touch-based intentional selection, right-tap ranged attacks and left-hold bird defense remain pending/deferred while this sword-block restoration loop is tested. No counterattack against a humanoid enemy is required in this version.

**User check:** 20 birds follow chest rather than hand; five become a rigid sword; red birds clearly announce dives; intercepting them restores them; losing tracking pauses attacks; restoring all fifteen returns a full peaceful flock of twenty.

## 5A. Shadow integration refinement — current work

- [x] First 20-bird/sword/attacking-flock encounter works after startup fixes. User confirmed "everything is working beautifully."
- [ ] Simplify avatar geometry and stop missing feet/ankles from stretching the body. **Implemented:** simpler oval head, fewer foot-dependent parts, bounded limb lengths, parent-relative missing-joint fallback, lower-limb floor clamp with calibration. Awaiting user movement/sitting test.
- [ ] Improve graphic lighting/background and show the avatar's transformation. **Implemented:** shaded grayscale avatar, soft room gradient/horizon and player-centered halo; avatar brightens during integration and room fades to white afterward. Awaiting visual test.
- [ ] Make flock movement more fluid and varied. **Implemented:** varied flight levels/radii/phases, gentle radial motion, smooth position/orientation changes, higher orbits during healing. Awaiting user test.
- [ ] Make deliberate swings more forgiving without trivializing hits. **Implemented:** original swept blade collision, filtered tip-speed bonus up to 14cm beyond the 22cm base radius, short white swing trace, and early hits on the active climbing/warning attacker during an actual swing. Other orbiting birds are not indiscriminately hit. Awaiting tuning test.
- [ ] Sword hits ground birds instead of immediately restoring them. **Implemented:** short fall arc, folded injured ground pose, no further attacks from injured birds; all fifteen grounded starts meditation. Awaiting user test.
- [ ] Seated/kneeling meditation restores birds gradually. **Implemented:** lowered chest relative to observed standing height, both hands low/near lap, modest stillness threshold; no foot tracking required. Two-second settling period plus roughly 18 seconds of accumulated stillness. Progress eases back slowly instead of resetting on movement. Optional standing variant exposed in controls. Awaiting actual pose test.
- [ ] White birds rise above the seated player, then all twenty are absorbed. **Implemented:** staged revival, overhead orbit, staggered spiral absorption, avatar whitening, four-second white-room transition. Awaiting complete ending test.
- [ ] Keep controls accessible without approaching the camera mid-scene. **Implemented:** eight-second ending-preview button; combat automatically hides diagnostic panels, H restores controls; simple on-screen pose cues/progress remain. Awaiting user test.
- [ ] Tune meditation posture/stillness thresholds, swing assistance and final visual pacing from user feedback.

**Manual test:** restart Play, Tab, inspect avatar/flock and try a few sword hits; check that fallen birds stay on the floor. For the ending alone, open controls with H and choose Preview Meditation Ending in 8 Seconds, then step back, sit/kneel, rest both visible hands near the lap and become still. Observe white birds rising, absorption, avatar whitening and white-room finale. If the pose will not register, report its on-screen cue; optional standing meditation can isolate the height requirement. No automated Unity session or project copy was used.

## Current scope — polish only

User confirms the curved weapon feels much better. Preserve the current control feel. Earlier diagnostic/control proposals below are parked unless a reproducible bug requires a targeted fix. No new mechanics, microphone chapter, combat-plane experiment or asset cleanup in this pass.

- [x] Curved weapon feel accepted by user.
- [x] Set startup orientation to the state previously reached by pressing M once (serialized mirrorX off; Kinect axis conventions determine the visible result). M remains available as an override. Awaiting next Play confirmation.
- [ ] Polish hit feedback: brief feather/ink accents, a concise impact sound and readable injured landing.
- [ ] Polish attack readability: clear red build-up and wind-up before committing to a dive.
- [ ] Polish grounding: restrained contact shadows beneath avatar and injured birds, stable silhouette and readable floor.
- [ ] Polish flock motion: varied wingbeat timing, smoother turns and less avatar clipping without changing the 20-bird budget.
- [ ] Polish meditation/integration pacing: quiet breathing-like ambience, distinct revival cues, player-centered expanding light and a calm white ending.
- [ ] Complete a full manual playthrough in the better room; fix concrete glitches without broad control retuning.

## Earlier refinement proposals — parked

User accepted the shadow-integration revision, but reports glitching/responsiveness makes wielding unpleasant. A more suitable room will be available later. Do not attribute this entirely to physical depth coverage without measurement. Cleanup remains ON HOLD. The following control changes are planned, not implemented by this roadmap entry.

### P0 / make wielding trustworthy

- [ ] Compare raw sensor hand/wrist, mapped hand, avatar hand, weapon grip, phone orientation and frame age in one small diagnostic view. Test stillness, a slow arc and a quick comfortable swing in the current room and later in the better room; capture active-game frame time. Distinguish tracking dropouts, position jitter, rotation jitter, visual mismatch and input latency.
- [ ] Use one shared weapon-hand pose for avatar and grip. Current bounded avatar arms can end at a different point from raw `owner.Hand`, which still anchors the sword. Fix that mismatch without restoring limb stretching.
- [ ] Prefer confident hand data, fall back to wrist data when holding the phone occludes the hand, and transition predictably on brief loss/reacquisition. Avoid sudden freezes/reacquisition jumps.
- [ ] Add explicit grip-axis/neutral-orientation calibration. Current direct phone quaternion versus arm-direction fallback can change abruptly when phone orientation expires; make source transitions stable and visible during setup.
- [ ] Audit Kinect smoothing plus BoidBodyPose's additional smoothing. Tune rest jitter separately from swing latency; consider adaptive damping rather than adding another slow filter.
- [ ] Separate true elapsed animation time from simulation/collision substeps. Current `dt` cap can slow animation at lower FPS and exaggerate computed swing speed; make timing and swing assistance frame-rate consistent.
- [ ] Try an optional 2.5D combat plane so small front/back tracking errors do not decide apparently obvious hits. Compare it with full 3D using visible hit-path feedback before choosing a default.
- [ ] Keep assistance bounded and earned by deliberate motion. Tune actual blade reach, impact timing and hit feedback before increasing the radius further. No violent/fast swing requirement.

### P1 / readable, cohesive visual design

- [x] Reference-inspired weapon: shorter gently recurved blade, compact guard, shaped/flared dark handle, dark spine, white cutting face and inset blade line. **Implemented and user confirms much better feel.** Approximately 1.2-unit blade; curve-based swept hit path, fusion points and tip trail match. No tracking filter changes bundled with this model change.
- [ ] Add ground contact cues beneath the avatar and fallen birds, and a restrained arena/floor motif so depth and grounding read clearly.
- [ ] Give attackers a stronger silhouette and clearer wind-up/commitment cue; keep red concentrated on threat, injury and key transitions.
- [ ] Refine the avatar silhouette and grip attachment as one design; limit armor/detail until the motion is stable. Avoid effects masking tracking faults.
- [ ] Stage camera/framing for readable incoming paths and the full curved blade. Prefer readable silhouettes over realistic perspective; test a subtle alternate view only after control alignment is trustworthy.
- [ ] Make corruption, falling, injury, revival and absorption visually distinct through wing pose, speed, contrast and small ink/feather accents.
- [ ] Add restrained audio: wingbeats, a short swing sound, clear impact, quiet breathing/ambient tone in meditation, and a sustained release at integration. Check that feedback helps timing rather than overloading it.
- [ ] Replace the final flat fade with an expanding player-centered light treatment, while preserving a comfortable gradual transition and final white space.

**Superseded priority:** the diagnostic/control roadmap above is parked. Follow the current polish-only checklist first.

## 6. Microphone and vulture aftermath (deferred)

**Deferred:** the current approved ending restores the original flock. Revisit this later as a separate chapter rather than attaching scavengers to nonexistent dead enemies.

- [ ] On enemy defeat, quiet the arena and introduce larger, slower vultures circling above the fallen enemy before descending.
- [ ] Distinguish vultures from the companion flock through silhouette, scale and motion.
- [ ] Transform a portion of the flock into a battered announcer-style microphone; keep the remaining birds circling the avatar.
- [ ] Receive mic-level input through the actual ZigSim format and show its live values during setup.
- [ ] Calibrate ambient noise and normalize the usable voice range. Normal speaking volume should work; shouting should not be required.
- [ ] Decide activation gating: for example, microphone equipped and held near the mouth. Choose a forgiving rule that does not conflict with aiming or accessibility.
- [ ] Use Kinect hand/arm pose for aim and phone mic level for sound strength. Mic level does not provide speech meaning or sound direction; speech recognition is not required.
- [ ] Light a red ring at the microphone in response to voice input.
- [ ] Emit broken white expanding lines around the avatar, stretching into directional jagged sound waves toward the aimed area.
- [ ] Make the affected region and response visible: vultures recoil, scatter or briefly flash into skeletal white silhouettes/ink fragments.
- [ ] Tune decay, thresholds and targeting so background noise does not constantly trigger the effect.
- [ ] End the encounter when the sky clears; return the equipment to birds and transition back to idle.

**User check:** speak at a comfortable volume, see immediate sound feedback, aim at a vulture cluster, and understand why birds scatter or remain.

## 7. Whole-sequence polish and optimization

- [ ] Play through idle → sword → fight → defeat → microphone → vultures cleared → idle.
- [ ] Add restrained ink textures/bursts, hard shadows, impact flashes, slash trails and scene transitions.
- [ ] Add sound for flock movement, formation, enemy wind-ups, blocks, hits and aftermath; ensure effects do not self-trigger microphone input excessively.
- [ ] Keep warnings, block success, charge/activation and hit outcomes unmistakable without covering the avatar with UI.
- [ ] Review comfort and readability: flashes, voice effort, rapid gestures and tracking-loss behavior.
- [ ] Measure the flock with Kinect active. Replace the current all-pairs neighbor search with spatial partitioning if it remains a bottleneck.
- [ ] Keep bird meshes simple; use batching/instancing or another measured rendering improvement as appropriate.
- [ ] Budget active birds, shadows, trails, transparency and effects; compare against the baseline rather than assuming fewer project assets improve FPS.
- [ ] Save a repeatable scene/room setup and concise start/retry instructions.

## Priority if time is limited

**Reliable room calibration → intentional controls → rigid sword/readable flock → one readable fight → microphone/vultures → polish.**

Do cleanup early enough to remove genuine obstacles, but do not let a broad asset purge delay the first playable encounter. Keep optimization driven by measured bottlenecks.

## Decisions still to settle through testing

- Actual room/sensor placement and playable dimensions.
- Touch coordinate orientation, region sizes, multi-touch and form order/selection.
- Bird attack aim/target selection and defense direction.
- Bird budget/allocation, return times and resource limits.
- Enemy silhouette, exact attack direction, block detection and fail/retry rules.
- Microphone activation/aiming pose and reliable mic-level normalization.
- Final narrative/environment detail after the interaction loop works.

## Progress log

- **2026-10-26 / multi-hit and flock pulse:** User requested multiple hits, higher spread and a small hit-driven flock expansion. Removed single-hit swing latch, raised/widened orbit bands and added capped outward/upward pulse with smooth return. Direct control and larger bird size retained. Runtime source compilation passed with existing project warnings; awaiting user visual/combat test.

- **2026-10-26 / restore direct tracking:** At user request removed assisted cuts, procedural combat stance/shuffle and strike IK. Restored bounded tracked avatar and direct hand/phone curved sword. Increased bird mesh scale 25%; preserved all-free-bird physical hit eligibility and single-hit-per-swing behavior. Retired assisted plan; no imported animations or cleanup. Runtime source compilation passed with existing warnings; awaiting manual test.

- **2026-10-26 / deliberate swipe and shuffle:** Raised swipe minimum to 17cm/1.15m/s with 0.07s sustained motion. Added alternating planted-foot shuffle from character displacement, foot lift, movement deadzone and blend to stronger bent-knee guard/breathing. Uses existing procedural avatar, no imported assets. Awaiting user stillness/move/reverse/stop/swipe test; no Unity launch or cleanup.

- **2026-10-26 / authored fighter stance:** User approved position-following gesture-controlled fighter. Added procedural bent-knee guard, breathing/weight shift, smoothed X/Z following and fixed standing height. Sword idle/recovery now returns to guard instead of exact tracked hand; phone motion is restrained, while Kinect hand still detects swipes. Arm IK follows the shared guard/cut grip; combat flock/attacks align with displayed center. One bird per swipe retained. Blended exit to existing physical meditation; dedicated seated art deferred. Source compilation passed before final allocation-only cleanup; no Unity launch/asset cleanup. Awaiting manual feel and full-encounter test.

- **2026-10-26 / calibration inversion and single-target cuts:** User reports phone required face-down/top-away. Reversed tabletop pitch basis; awaiting screen-up/top-toward-camera verification. Each assisted swipe now damages only its locked nearest bird, with all 15 free birds still eligible. Supersedes multi-hit behavior. Animation/input timing unchanged pending focused feel feedback.

- **2026-10-26 / free bird targeting and avatar strike:** User clarified all flying birds must be hittable without waiting for dive selection. Added nearest eligible free-bird targeting, hit checks against all 15 birds during animated cuts, multi-hit intersections and preserved active attacks when a different bird is grounded. Added a bounded two-bone weapon-arm strike pose and upper-body follow-through returning to tracked pose. Removed long settle requirement between completed cuts. Runtime source compilation passed with existing dependency warnings; no Unity launch. Awaiting manual targeting/arm-return/full-encounter test. Supersedes active-attacker-only prototype behavior.

- **2026-10-26 / tabletop zero and assisted-cut prototype:** User reports mild acceptable sensitivity, requests C-only screen-up tabletop calibration and assisted cuts. Added tabletop reference to C/button; preserved optional ready-pose capture. Added deliberate translation swipe/rearm, bounded current-attacker targeting, 0.22s rigid cut and 0.16s return, collision-only hit awards, no-target misses, tracking/setup/reset cancellation and HUD comparison toggle. Weapon arm IK and final feedback remain pending. Awaiting manual user test; no Unity launch or cleanup. Standalone compile could not validate against stale cached Kinect API assemblies.

- **2026-10-23 / polish scope and startup orientation:** User confirms much better weapon feel and requests polishing only. Changed controller default and active scene orientation to the state previously selected with one M press. Preserved controls and gameplay; recorded the focused polish checklist above. No cleanup or Unity launch. Startup orientation awaits manual confirmation.

- **2026-10-23 / curved weapon and next-pass plan:** User accepted the shadow-integration direction but reported poor wielding feel and provided a curved-blade reference. Replaced the straight 1.5-unit blade with a shorter approximately 1.2-unit gently recurved mesh, smaller guard/flared grip, dark spine and inset lines. Swept hit checks now follow the actual segmented curve; assembly points and trail match it. Documented a prioritized responsiveness/visual roadmap based on current source risks. Tracking/filter/source-selection behavior left unchanged pending focused diagnosis and the user's later room test. No cleanup or Unity session. Awaiting user curved-weapon test.

- **2026-10-23 / shadow integration:** User confirmed the encounter works and requested a simpler stable avatar, stronger visual atmosphere, fluid multi-height flock, modest swing assistance, injured birds and seated healing/integration ending. Implemented these as the new active narrative; previous immediate-white-on-block ending is superseded. Local source/API checks only; hardware/pose/visual validation remains with the user. No cleanup.

- **2026-10-23 / encounter startup fix:** User logs showed successful sensor opening followed by forbidden MaterialPropertyBlock construction in a MonoBehaviour field initializer, then a cascading null reference. Moved creation into encounter Initialize before mesh tinting. The separate sensor zero-quaternion warning traced to uninitialized sensorPoseRotation; initialized it to identity before body-tracker threads can request a matrix. No Unity launch. Awaiting user restart/encounter test.

- **2026-10-23:** Consolidated the agreed plan and existing baseline. No new gameplay, cleanup or scene changes made as part of creating this checklist. Next implementation starts with a focused cleanup/performance inventory and room-view/calibration work.
- **2026-10-23 / first implementation:** Added the actual-room Setup view, aligned projection code, live/stale/confidence display, optional available color feed and rolling performance readings. Created `CLEANUP_AUDIT.md`; identified demo-to-tennis references and preserved all assets. No Unity launch, test copy or automated Play session. **User test:** restart Play; confirm room image is upright, not stretched and mirrored as expected; step back and check skeleton at hands/feet; use delayed recenter; Tab to Play and report FPS/flock CPU timing. Do not mark this stage complete until feedback arrives.
- **2026-10-23 / room calibration:** User accepted the room-view improvement and put cleanup on hold. Added live-depth floor picking, meter-marker measurement, explicit uniform scale adjustment, picked floor bounds, saved local room settings, and floor-preserving player recenter. No cleanup, Unity launch or automated Play session. **Next user test:** select three floor points, measure 1m markers, pick bounds, save; center after stepping back, verify avatar feet/floor and inside/outside status; restart Play and verify room calibration persists.
- **2026-10-23 / restoration encounter:** User reported calibration works and requested the story/gameplay step. Approved 20 chest-orbiting birds, five fusing into a sword, remaining birds slowly turning red and diving; chose restoration on successful blocks. Implemented the first complete encounter and monochrome/red procedural meshes. Existing controls call the sword; combat ignores further form-switch snaps. Cleanup and microphone sequence remain on hold. No Unity launch or automated play session. **Awaiting manual combat test.**
