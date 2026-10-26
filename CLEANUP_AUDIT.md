# Initial cleanup audit — 2026-10-23

Read-only inventory and initial serialized-GUID reference scan. **No content or packages removed.** Sizes are rounded and exclude generated Library/Temp data. This is a starting dependency review, not proof that every unreferenced asset is safe to delete: script references, dynamic loads, native dependencies and other scenes also matter.

## What occupies space

| Assets folder | Approx. MB | Initial disposition |
|---|---:|---|
| AzureKinectExamples | 411.5 | Keep SDK/scripts; review demo content separately |
| Scene | 9.7 | Old tennis/collider/multiuser content; candidate after backup and dependency resolution |
| TextMesh Pro | 3.8 | Review usage across retained scenes/UI before removing |
| CityPeople_Free | 0.6 | Referenced by old tennis prefab |
| BrokenVector | 0.5 | Referenced by old tennis scene |
| extOSC | 0.5 | Runtime OSC parser is used by current phone input; examples/editor tools can be reviewed |
| ZigSimTools | 0.2 | Current receiver uses its sensor data classes; preserve those |
| GesturalBoids | ~0.1 | Current prototype; retain |
| TutorialInfo / _Recovery / Materials | <0.1 each | Review references and archive candidates |

Within AzureKinectExamples: SDK ~310.9 MB, KinectDemos ~97.8 MB, KinectScripts ~1.7 MB, Resources ~0.2 MB, readme assets ~0.8 MB.

The root also contains over 2 GB of native CUDA/cuDNN/TensorRT/ONNX DLLs, plus an approximately 159 MB body-tracking model. The largest include cudnn_cnn_infer64_8.dll (~600.5 MB), cudnn_ops_infer64_8.dll (~346.2 MB), nvinfer.dll (~344.9 MB), cufft64_10.dll (~344.8 MB) and cublasLt64_11.dll (~269 MB). These are sensor inference dependencies or alternative backends, not ordinary game art. Do not delete them by size alone. Backend-specific trimming needs a recoverable archive and live sensor startup checks.

## Initial reference findings

The scan checked GUID references in Assets `.unity`, `.prefab`, `.asset`, `.mat`, `.controller` and `.overrideController` files for selected cleanup candidates.

- An asset under `Scene/TennisProjectFinal` is referenced by `AzureKinectExamples/KinectDemos/AvatarDemo/KinectAvatarsDemo4.unity`.
- `Scene/TennisProjectFinal/SkeletonColliderDemo.unity` references BrokenVector content.
- `Scene/TennisProjectFinal/Female_Adult_CustomSkinB.prefab` references CityPeople_Free content.
- `Assets/Readme.asset` references TutorialInfo.
- No outside serialized references to `_Recovery` were found in this limited scan.
- No selected candidate references were found in the current GesturalBoids scene, and no explicit Resources.Load/selected candidate-name references were found in GesturalBoids C# sources. This does not establish complete dependency closure.
- Build Settings currently lists no scenes, so it does not identify a final player build scope yet.

## Next cleanup batch

1. Choose the retained scenes. Archive the old scene/demo set together with its referenced assets before deleting anything.
2. Resolve/remove the retained-demo-to-tennis reference if that demo is to remain.
3. Check code, Resources/dynamic loads and package dependencies beyond this initial GUID scan.
4. Remove one recoverably backed-up batch, then ask the user to check scene loading, Kinect tracking and phone input.
5. Review native backends only after measuring and confirming which backend is actually required.

## Runtime baseline

New room Setup/Play UI exposes rolling FPS, average/peak frame time, and the CPU time spent updating the current flock. Setup pauses flock simulation; use **Play mode (Tab)** to capture an active-flock baseline with Kinect connected. No FPS or hardware performance measurements have been claimed yet. The all-pairs CPU flock search remains a known optimization candidate, not a measured dominant bottleneck.
