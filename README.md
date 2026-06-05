# AR-AluminumCan-Crush-Simulation

A perceptually driven framework for hand-motion-based faceted plastic deformation in smart glasses. Real-time AR aluminum can crushing simulation using hand tracking on XREAL Air 2 Ultra glasses.

> Juyeon Ryu, Jong-Hyun Kim — College of Software and Convergence, Inha University

---

## Overview

This repository provides the source code for a hand-tracking-based mesh deformation framework that enables real-time representation of plastic and thin can-like object deformation in smart-glass environments.

The framework converts finger-curl variations and pinch-distance changes obtained from hand tracking into continuous input intensities, and generates local object deformation based on the user's contact position and grip direction.

The project evolved through two architectural generations:

**Version 1 — Gaussian CPU Mesh Deformation** (`MeshDeformation/`): Direct CPU-side vertex displacement using a Gaussian falloff function. Produces smooth, stable local indentation around contact points. Simple to implement but causes CPU–GPU data transfer bottlenecks at scale and cannot represent the flattened facets and fold lines of real crushed cans.

**Version 2 — GPU Faceted Crush Solver** (`VertexShader/`): Compact crush-zone parameters computed from hand tracking are transferred to a GPU vertex shader, where a cross-sectional crush function evaluates fold-line angle, facet depth, and shader-side normal correction per vertex. Generates flattened facet regions and sharp fold-line transitions resembling real can deformation, with near-zero CPU overhead.

---

## Pipeline

### Version 1 — Gaussian CPU Mesh Deformation

```
Hand Tracking Input
        │
        ▼
Finger Curl / Pinch Distance
        │
        ▼
Contact Detection (MeshCollider Raycast)
        │
        ▼
Gaussian Falloff Displacement (CPU)
  · distance-based weight w(d) = exp(-α(d/R)²)
  · displacement: Δv = -n · D · s · w(d)
        │
        ▼
mesh.vertices update → RecalculateNormals()
        │
        ▼
Deformed Mesh (per frame)
```

### Version 2 — GPU Faceted Crush Solver

```
Hand Tracking Input
        │
        ▼
┌─────────────────────────────────────┐  CPU (per frame)
│  Intensity Computation              │
│  · Finger curl delta → sf           │
│  · Pinch distance delta → sthumb    │
│                                     │
│  Grip-Aware Direction Estimation    │
│  · Thumb: θthumb = atan2(x, z)      │
│  · Fingers: θfinger = circular mean │
│                                     │
│  Crush-Zone Parameter Construction  │
│  · Ck = (θk, hk, sk, ρk)           │
│  · Up to 8 slots (_Crush0~_Crush7) │
└─────────────────────────────────────┘
        │  shader uniforms
        ▼
┌─────────────────────────────────────┐  GPU Vertex Shader
│  Cross-Sectional Crush Function     │
│  · δ = 0.9 · severity               │
│  · α = arccos(1 - δ)  [fold angle] │
│  · hf = -(1 - cosα)   [facet depth]│
│  · Axial Gaussian falloff           │
│  · Cap mask (top/bottom protection) │
│                                     │
│  Shader-Side Normal Correction      │
│  · ∂d/∂θ via central differences   │
│  · n' = normalize(n - t·slope)     │
└─────────────────────────────────────┘
        │
        ▼
Deformed Mesh + Corrected Normals (real-time)
```

---

## Features

**Version 1 (MeshDeformation)**
- Gaussian falloff-based local indentation around contact points
- CPU vertex array update with `RecalculateNormals()`
- Plastic-like deformation accumulation with `Dmax` saturation
- Multi-object support via runtime mesh instancing
- Grab detection with side-grip and top-grip modes
- Deformation reset with full state restoration

**Version 2 (VertexShader)**
- GPU vertex shader deformation — zero CPU mesh modification per frame
- Cross-sectional crush function: flattened facets + fold-line transitions
- Shader-side normal correction using angular derivative of displacement
- Grip-aware compression direction estimation (thumb vs. fingers, circular mean)
- Up to 4 simultaneous independent grip zones (8 crush slots total)
- Validated in real time on XREAL Air 2 Ultra + XREAL Beam Pro

---

## Repository Structure

```
AR-AluminumCan-Crush-Simulation/
│
├── MeshDeformation/               # Version 1: CPU Gaussian deformation
│   ├── Core/
│   │   ├── AluminumCanSimulation.cs   # Main controller
│   │   ├── CanMesh.cs                 # Runtime mesh + buckle region management
│   │   └── BucklingAnimator.cs        # Animated deformation coroutine
│   ├── HandTracking/
│   │   ├── MRTKHandAdapter.cs         # OpenXR joint detection and curl computation
│   │   ├── HandState.cs               # Hand data structure
│   │   ├── HandCanContactDetector.cs  # Finger-to-can contact detection
│   │   └── GripPatternRecognizer.cs   # Grip type classification
│   ├── Interaction/
│   │   ├── CanGrabSystem.cs           # Grab/release with physics
│   │   ├── CanResetManager.cs         # Per-object reset
│   │   └── ResetAllCans.cs            # Scene-wide reset
│   └── Debug/
│       └── SimpleDebugDisplay.cs      # On-device AR debug overlay
│
├── VertexShader/                  # Version 2: GPU faceted crush solver
│   ├── Core/
│   │   ├── AluminumCanSimulation.cs   # Main controller (UV-extraction variant)
│   │   └── CanNormalMapDeformer.cs    # Crush-zone management + shader parameter push
│   ├── Shaders/
│   │   └── CanCrushShader.shader      # Vertex shader: crush function + normal correction
│   ├── HandTracking/
│   │   ├── MRTKHandAdapter.cs
│   │   ├── HandState.cs
│   │   ├── HandCanContactDetector.cs  # UV extraction via MeshCollider.Raycast
│   │   └── GripPatternRecognizer.cs
│   ├── Interaction/
│   │   └── CanGrabSystem.cs
│   └── Debug/
│       └── SimpleDebugDisplay.cs
│
└── README.md
```

---

## Requirements

| Component | Version |
|-----------|---------|
| Unity | 2022.3.35f1 (Built-in Render Pipeline) |
| XREAL SDK | 3.0.0 |
| MRTK3 | Mixed Reality Toolkit 3 |
| Target Platform | Android (XREAL Beam Pro) |
| Display | XREAL Air 2 Ultra (optical see-through) |
| Hand Tracking | OpenXR Hand Tracking |

---

## Installation

1. Clone this repository.
2. Open Unity Hub and add a new project. Use Unity 2022.3.35f1 with Built-in Render Pipeline.
3. Import MRTK3 and XREAL SDK 3.0.0 into the project.
4. Copy the scripts from `MeshDeformation/` or `VertexShader/` into your project's `Assets/` folder.
5. For the vertex shader version, register `Custom/CanCrushShader` in **Project Settings → Graphics → Always Included Shaders** to ensure it survives Android builds.
6. Attach the scripts to your can GameObject according to the component dependencies below.
7. Build and deploy to XREAL Beam Pro.

### Component Setup (VertexShader version)

The can GameObject requires the following components:
- `MeshFilter` + `MeshRenderer` + `MeshCollider`
- `Rigidbody`
- `AluminumCanSimulation` (assign `MRTKHandAdapter` and original mesh asset)
- `CanGrabSystem`
- `CanNormalMapDeformer` (added automatically via `AddComponent` at runtime)

### 3D Model Note

The aluminum can 3D model is not included in this repository due to licensing restrictions. Any cylindrical mesh with proper UV mapping (UV.y range approximately −0.233 to 0.362 for this implementation) can be used.

---

## Key Implementation Notes

**UV Coordinate Remapping (VertexShader)**
The can model uses a non-standard UV range. Before passing UV.y to the deformation system, normalize it:
```csharp
const float UV_MIN = -0.233f;
const float UV_MAX = 0.362f;
float height = Mathf.Clamp01((uv.y - UV_MIN) / (UV_MAX - UV_MIN));
```

**Scale Mismatch**
The can's local scale (~0.04684) means world-space and object-space values differ significantly. All deformation radius and depth comparisons must account for `lossyScale`.

**Shader Registration**
Custom shaders must be registered in Project Settings → Graphics → Always Included Shaders to survive XREAL device builds.

**Editor Testing**
MRTK editor simulation uses the right hand for contact detection. Test grab and deformation using the right hand in Play Mode.

---

## Results

### Version 1 — Gaussian-based Deformation

The baseline Gaussian model produces stable smooth local indentation in real time. Changes in hand motion are immediately reflected per frame, and similar deformation shapes are repeatedly produced under the same interaction conditions. However, the deformation shape is generally smooth and curved — flattened compressed regions and sharp fold lines observed in real thin cans cannot be represented.

![Gaussian Deformation Results](Images/Fig9.png)

### Version 2 — Faceted Crush Solver

The extended solver generates flattened facet regions and sharp fold-line transitions depending on contact position, input intensity, and grip direction. Grip-aware compression direction estimation produces asymmetric deformation from both thumb and finger sides simultaneously, closer to real grasping behavior.

![Faceted Crush Solver Results](Images/Fig10.png)

### Comparison: Real vs. Virtual

Different crush patterns are generated depending on the user's finger contact position and input intensity — narrow-waist compression, asymmetric side collapse, and twisted faceted deformation. The proposed solver produces visually plausible can-like deformation patterns in real time while reflecting variations in user interaction.

![Real vs Virtual Can Comparison](Images/Fig11.png)

---

## Related Publication

> J. Ryu, J.-H. Kim, "A Perceptually Driven Framework for Hand-Motion-Based Faceted Plastic Deformation in Smart Glasses," Inha University, 2026.

---

## Acknowledgements

This work was supported by INHA UNIVERSITY Research Grant and the National Research Foundation of Korea (NRF) grant funded by the Korea government (MSIT) (RS-2026-25474535).

---

## License

MIT License. See `LICENSE` for details.
