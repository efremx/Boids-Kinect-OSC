# Assisted sword control — review plan

Status: retired on 2026-10-26 at the user's request. Retained as experiment history, not an active implementation checklist. Do not resume assisted cuts, procedural fighting poses or locomotion without a new user request.

## Restored baseline

Latest override: the user subsequently requested multiple birds per direct swing. The single-hit-per-swing restriction described in the rollback entry below has been removed. Current flock polish is tracked in PROJECT_PLAN.md; the animation experiment remains retired.

Body-tracked bounded mannequin and direct hand/phone sword control restored. Removed assisted gesture/target animation, weapon-arm IK, guard stance and shuffle code and UI. Birds enlarged 25% with existing size variation and healing/integration scaling preserved. All 15 airborne birds remain physically hittable without waiting for their dive turn; one bird per continuous swing, rearmed when blade speed settles. Tabletop C calibration, mirrored startup, room tools and story/meditation remain. User considers authored animation data/model work outside project scope. Source compilation passed; awaiting manual direct-control and bird-size test.

### Stronger swipe and fighting footwork — awaiting test (2026-10-26)

User requested a higher swipe threshold and fighting-game-style walking, with more expressive idle. Increased minimum screen-plane travel from 10cm to 17cm, average speed from 0.75m/s to 1.15m/s and sustained motion from 0.055s to 0.07s. Thresholds are public fields for later tuning; no faster/violent swing requirement intended.

Added procedural alternating shuffle steps driven by the displayed character's actual X/Z movement. One foot remains planted while the other lifts and steps over 0.22s toward the movement direction. Small movement deadzone, smoothed locomotion weight, oversize-step reset and completed recovery steps reduce noise-driven foot motion. Restored a wider, lower staggered guard with stronger breathing/weight transfer, shoulder turn, raised free hand and small locomotion lean/bob. No external model, animation package or imported assets needed for this prototype.

Manual check: small phone/hand adjustments should not slash; a deliberate comfortable swipe should. Step left/right, forward/back, reverse and stop; verify alternating feet, grounded stance and no walking while standing still. Then attack while moving and check recovery. Hardware animation quality remains awaiting user test; compile checked separately without launching Unity.

### Current direction — position-driven fighter (2026-10-26)

User approved an animated fighting character instead of exact body-pose reproduction in combat. This supersedes the prototype's return-to-tracked-hand behavior below.

- [ ] **Implemented, awaiting user test:** idle/assembly/fight use an authored bent-knee staggered stance, guard hand, diagonal blade, subtle breathing and weight shift. Chest X/Z follows the player with 0.10s damping; standing height stays stable relative to calibrated/estimated floor. Feet/arms no longer copy Kinect joint positions in these phases.
- [ ] **Implemented, awaiting user test:** raw tracked hand remains the gesture input, independently of displayed avatar arms. Quick swipes retain nearest eligible bird targeting, one hit maximum per swipe and collision-confirmed strikes.
- [ ] **Implemented, awaiting user test:** sword's free pose and post-cut recovery use the authored guard grip/rotation, with phone influence capped to 10 degrees. Arm IK follows that shared grip throughout equipped idle, strike and recovery. Animated reach is bounded against the authored shoulder, rather than the player's tracked shoulder.
- [ ] **Implemented, awaiting user test:** flock, warning lines and incoming attack collision use the displayed fighter's smoothed center during combat. Transition to existing tracking/meditation behavior blends avatar pose over 0.3 seconds. Physical meditation detection remains unchanged; a dedicated seated animation is deferred.
- [ ] User check: move arms slowly without swiping (avatar should keep guard); step sideways (character follows); slash one bird (arm/weapon cut then return to guard); repeat in the other direction; pause tracking and resume; finish all birds and test sitting/meditation. Watch for grip gaps, recovery snaps or feet floating.

Validation: runtime source compilation with Unity reference assemblies passed with existing project warnings. No Unity launch or physical playthrough. Existing direct-control comparison mode retains tracked-avatar behavior.

### Current correction — one bird per swipe (2026-10-26)

User reports calibration required face-down/top-away and multi-bird swipes feel poor. Reversed tabletop neutral basis from +90 to -90 degrees around X to correct the reported inversion. Retest C with screen up and top edge toward camera; quaternion and gyro paths share this basis. Changed assisted collision to the single nearest bird locked at swipe start. Other birds crossed by the blade cannot take damage; a grounded target cannot award a second hit or retarget within the same slash. All 15 flying birds remain eligible for selection, without waiting for a dive turn. This supersedes multi-hit behavior below. No gesture thresholds or animation timing changed in this correction. Awaiting manual calibration and one-hit test.

### Latest approved behavior — all flying birds hittable (2026-10-26)

This supersedes the earlier active-attacker-only targeting and mandatory settle rules below. Every distinct quick translational swipe when the previous cut/recovery has finished can start a cut; there is no long hold requirement. Input during an existing cut/recovery is not queued. Wrist-only detection is still deferred.

- [ ] **Implemented, awaiting user test:** nearest uninjured flying bird within 1.55m of the grip and 2.1m of the chest is selected, irrespective of corruption, dive selection or swipe direction. All 15 free birds qualify; the five fused into the sword remain its material. Injured/healing birds are excluded.
- [ ] **Implemented, awaiting user test:** any eligible bird intersecting the animated blade can be struck, including multiple birds in a single cut. No need to wait for a bird to climb or dive. Target selection still does not award a hit by itself.
- [ ] **Implemented, awaiting user test:** striking a circling bird does not cancel another bird's ongoing attack. Grounded count and meditation continue to use all 15 birds.
- [ ] **Implemented, awaiting user test:** procedural two-bone weapon-arm strike pose follows the animated sword grip, with bounded reach and small neck/head follow-through; blends back over recovery to the user's ordinary tracked avatar. Full-body animation/rig replacement is not required.
- [ ] User check: strike birds before their dive turn, compare nearest targeting on either side, cut through a cluster, and verify arm/weapon return without a snap. Finish all 15 and verify meditation starts normally.

Source validation: compiled current runtime project sources plus GesturalBoids scripts with Unity's bundled C# compiler and Unity reference assemblies successfully. Existing dependency/obsolete-API warnings remain. Output written only to a temporary check directory; Unity was not launched and no project assemblies were replaced. Hardware/animation quality still awaits the user.

### Assisted-cut prototype — awaiting user test (2026-10-26)

User accepted setup with mild sword sensitivity, prioritizes C-only phone calibration, and requests assisted cuts next. C and the phone calibration button now use a screen-up tabletop reference (top edge toward the sensor); the flat blade points forward, lifting the phone raises it. The optional delayed ready-pose capture retains its upright convention explicitly. Neither C nor the new cuts requires floor picking.

Implemented hand-translation swipe detection: settle 0.12 seconds, then at least 10cm of predominantly screen-plane travel within 0.24 seconds, with minimum average speed 0.75m/s and sustained motion. Single-frame jumps and stale tracking cancel input. One slash per rearm; wrist-only/gyro gesture detection is not included yet.

Cuts last 0.22 seconds, recover over 0.16 seconds toward the latest tracked hand. Only the current active threat qualifies, within 1.42m of the grip and 2.1m of the chest and in a broad directional region. Prediction is capped at 16cm; target selection never awards a hit. Curved-blade swept checks remain authoritative, enabled only during a targeted cut; recovery and ordinary positioning cannot deal assisted hits. Missing targets produce a small slash. Setup, stale tracking, C, reset and meditation clear attack state. The HUD offers an assisted/direct comparison toggle (H restores the panel).

The sword and grip marker share the animated pose. Full weapon-arm IK remains the next stage, so the arm can still separate slightly from the grip during this prototype. Impact audio/feather effects are pending.

Validation: inspected input, attack gating, cancellation and collision paths. A standalone compile attempt encountered stale cached Unity assembly/API references (existing GetSensorData usage), so it does not establish a successful current Unity build. No Unity launch or hardware play test. Let the open editor compile and report errors if any.

Manual test: screen-up phone on table, top edge toward sensor, press C; lift into a comfortable grip and verify direction. Call sword, settle, then swipe left/right or diagonally toward a close red attacker. Check single cuts, reachable hit timing, misses, recovery and no retrigger while repositioning. Compare assistance off in H panel. Report direction errors, missed obvious cuts, accidental triggers or recovery snaps before tuning and adding IK.

### Stage 1 progress — awaiting user test

Implemented optional advanced room controls, full-depth uncalibrated mapping, and an eight-second delayed player/phone capture. Capture requires fresh body/hand data and live orientation, followed by 0.8 seconds of low motion; it times out with a retry message after 20 seconds. Existing saved floor mapping remains active. Neutral capture aligns the phone to the existing upright blade convention; C remains phone-only zeroing and R remains delayed recenter. Gyro-only zeroing now uses the current integrated orientation rather than an old quaternion sample.

Physical grip offset/shared target and reach visualization remain pending with the arm/weapon work. No assisted slash or IK has been added yet. No Unity launch; runtime validation remains with the user.

Test: start Play, select player + phone capture, step back and hold the preferred ready grip. Check that capture waits for stillness and refuses missing data. Tab to Play, call the sword and check neutral blade direction plus left/right, up/down and forward/back reach. Report any offset, inversion, snap or excessive sensitivity. If no saved floor exists, verify the estimated floor is acceptable for now.

## Intended experience

Keep the existing story, curved sword, 20-bird allocation, injury and meditation ending. Between attacks, the sword follows the player's hand and phone. A deliberate swipe triggers a short directional cut; modest assistance completes the cut through a reachable attacking bird. The player controls timing and direction, while the game handles the final precision.

This is a targeted control refinement. Cleanup, new weapons, microphone gameplay and a wider mechanics overhaul remain on hold.

## 1. Simplify setup and establish a trustworthy grip

- [ ] Make detailed room calibration optional. Keep floor picking, measurement and boundaries available in advanced setup.
- [ ] Use consistent physical meter scale in both calibrated and uncalibrated paths. Remove the uncalibrated path's 35% depth compression; validate framing and reach afterward. Existing floor calibration remains usable.
- [ ] Offer a delayed player/grip calibration so the user can step away from the keyboard. Require stable live body and phone data before capture; retry clearly if unavailable.
- [ ] Capture player center, comfortable phone neutral orientation and a defined phone-to-blade axis alignment. Use a tunable grip offset; do not imply one neutral pose measures the physical offset automatically.
- [ ] Provide a short reach check: left/right, up/down and forward/back. Show tracking readiness and the sword grip without requiring three floor clicks.
- [ ] Keep the existing room view available for positioning. Uncalibrated floor height is an estimate: keep ground effects consistent with that estimate and offer optional floor correction if floating/sinking is visible.
- [ ] Preserve the agreed startup mirror orientation. Verify hand movement and swipe direction agree with the mirrored screen; define phone rotation alignment in the same coordinate convention.

User check: start without detailed room picking, calibrate from the playable position, and move the sword through a comfortable reach. Confirm the neutral blade direction feels natural.

## 2. Prototype deliberate swipe detection

- [ ] Measure hand movement and phone angular movement independently. Start with hand velocity/direction as the main swipe signal, with phone motion as supporting evidence; validate before making both mandatory. Translation should work with the phone held steadily, and wrist-only attacks are deferred until tested.
- [ ] Require sustained motion over a short window and a minimum travel distance, rather than a single acceleration spike. Express thresholds in seconds and physical units, with frame-rate-independent timing.
- [ ] Map the observed swipe into a screen-readable direction. Begin with horizontal and diagonal cuts; add vertical cuts only if reliably distinguishable in testing.
- [ ] Add a clear rearm rule: motion must settle before another swipe. Ignore input during the committed cut, prevent buffered accidental chains, and keep a small recovery interval.
- [ ] Reject tracking jumps, stale phone data and reacquisition spikes. Define behavior when either input becomes unavailable; never invent an attack from missing data.
- [ ] Add a temporary test toggle for current direct control versus assisted control, plus compact swipe/target feedback. Keep it out of the normal presentation.

User check: comfortable swipes trigger once each; holding still, repositioning the phone, small adjustments and tracking recovery do not trigger cuts. Compare with the existing control feel.

## 3. Add bounded targeting and a short sword animation

- [ ] Select only the current attacking bird, within a forgiving but limited reach and directional cone. Do not target passive flock members or birds already on the ground.
- [ ] Lock the selected target at attack start. Use a bounded estimate of its movement to shape a cut path; do not chase it indefinitely or redirect mid-cut. A miss remains possible.
- [ ] Animate a curved path matching the swipe direction. Initial tuning range: 0.15–0.25 seconds for the cut, followed by approximately 0.10–0.20 seconds to return. These are trial values, not fixed requirements.
- [ ] Keep animation start continuous with the current sword pose; avoid teleporting into a wind-up. Limit assisted reach to the calibrated arm's comfortable envelope.
- [ ] When no target qualifies, perform a small directional slash without awarding a hit or extending toward distant birds.
- [ ] Use one authoritative sword pose for the mesh, trail, avatar grip and swept hit checks. Retain the curved blade's existing collision path; award a hit on actual intersection during the cut, not simply on target selection.
- [ ] Blend back toward the latest live hand pose, rather than the hand's old position at attack start. Limit correction so a moving player does not cause a snap.
- [ ] Define cancellation on tracking loss, setup entry, reset and narrative transitions. Clear attack history so recovery cannot create phantom hits. Suspend safely and resume with the existing enemy retelegraph behavior.
- [ ] Preserve the direct-control hit behavior when assistance is disabled; explicitly gate hit checks so assisted cuts and ordinary positioning do not award conflicting hits.

User check: a correctly timed nearby swipe looks like a clean cut, follows its intended direction, cannot hit across the arena, and returns naturally to the player's hand.

## 4. Make the avatar hold the animated sword using arm IK

- [ ] Establish one shared grip target. During free movement it comes from tracking; during a cut it comes from the attack animation; during recovery it blends between them.
- [ ] Add a lightweight two-bone solve for the weapon arm: shoulder → elbow → hand. Use measured stable arm lengths where reliable, with sensible defaults. No external rig package required for the current procedural mannequin.
- [ ] Use tracked elbow direction as a bend hint when confident; retain a stable fallback when it is obscured.
- [ ] Keep the shoulder anchored to the torso, clamp unreachable targets and keep the sword grip consistent with the final solved hand. Do not stretch the arm to satisfy an animation.
- [ ] Leave the rest of the avatar on its existing stable tracking path. Avoid a full-body IK rewrite.

User check: avatar hand remains attached to the hilt throughout free movement, slash and recovery; elbow does not flip and arm does not stretch.

## 5. Tune responsiveness and finish feedback

- [ ] Compare raw input, smoothed hand, solved grip and final sword pose during stillness, slow arcs and comfortable swipes.
- [ ] Audit Kinect smoothing and BoidBodyPose's Lerp together. Tune only after the assisted prototype is testable; avoid stacking another slow filter on the sword. Consider stronger rest smoothing and faster motion response if tests justify it.
- [ ] Smooth phone-to-forearm fallback transitions without allowing source changes to trigger a swipe. Assess hand-to-wrist fallback if phone occlusion remains a demonstrated problem.
- [ ] Add a restrained white cut arc, concise impact sound and small feather/ink burst on confirmed contact. Do not freeze tracking for impact feedback or use large flashes to hide faults.
- [ ] Keep attack wind-up and red threat cues readable enough to support intentional timing. Adjust difficulty only after input reliability is established.

## Validation and rollout

- [ ] Implement in small reviewable stages: setup/grip → swipe recognition → assisted sword prototype → arm IK → tuning/feedback. Give a short manual test after each stage.
- [ ] Compare direct and assisted control in the same room. Test both swipe directions, slow repositioning, repeated swipes, no target, out-of-reach target and brief tracking loss.
- [ ] Verify behavior at different frame rates, especially swipe thresholds, animation duration and swept hits. Use focused checks; do not launch duplicate Unity projects or lengthy automated Play sessions.
- [ ] Test the full encounter through meditation and integration, including sitting, reset and switching into Setup. Preserve the 20-bird count and five-bird weapon allocation.
- [ ] Test again in the better room before treating room coverage problems as solved by software.
- [ ] After user acceptance, make assisted control the normal mode and hide prototype diagnostics. Retain a developer comparison option if useful.

Acceptance: deliberate comfortable swipes produce one predictable cut, nearby birds are reasonably hittable without precise depth matching, grip stays attached to the avatar, and return-to-tracking feels continuous. No surprise attacks from ordinary movement or tracking recovery.
