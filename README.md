# Unity Voxelizer

![UnityVoxelizer](https://todo)

The objective of this project was to develop a **Voxel Renderer** in **Unity** using either **Universal Render Pipeline (URP)** or **HD Render Pipeline (HDRP)**. This version uses URP and a compute shader to render voxels using ray marching.

## Summary

This document is organized in the following sections:
- <a href="#general-information-">General Information</a>
- <a href="#usage-">Usage</a>
- <a href="#android-build-">Android Build</a>
- <a href="#known-issues-">Known Issues</a>
- <a href="#challenges-and-future-work-">Challenges and Future Work</a>
- <a href="#contributor-">Contributor</a>
- <a href="#license-">License</a>
- <a href="#third-party-">Third Party</a>
- <a href="#references-">References</a>

## General Information <a href="#summary">↑</a>

### Folder structure

This project has the following folder structure under `Assets`:
- `Art` - All art assets are here. 
- `Scenes` - With a few demo scenes:
  - `NaiveVoxelizerTest` - Demo scene for a naive voxelizer.
  - `OctreeVoxelizerTest` - Demo scene for an octree voxelizer.
  - `VoxelRendererTest` - Demo scene to test the renderer.
- `Scripts` - All scripts are here.
- `Shaders` - All shaders are here.
- `URP` - URP settings assets are here (Forward renderer and Pipeline asset).

### Implementation

All the classes and interfaces in the project will be described next:

#### IVoxelizer

Interface that describes a Voxelizer, i.e., something that can generate Voxels.

#### RandomVoxels

A simple `MonoBehaviour` that generates random colored voxels within a given range. This class implements `IVoxelizer`.

#### MeshRendererVoxelizerBase

An abstract class (also `MonoBehaviour`) that implements `IVoxelizer` and provides common functionalities to voxelizers that want to use a `MeshRenderer` as source. 

#### MeshRendererNaiveVoxelizer

Class that inherits from `MeshRendererVoxelizerBase` and implements a brute force way of voxelizing a `MeshRenderer`.
The algorithm consists of basically:
- Get dimensions of the mesh bounding box and adjust them to the global voxel dimensions.
- For each voxel in the bounding box, if any triangle of the mesh intersects a voxel, adds that voxel to the voxel list. 

When a voxel is created, it stores the color and uv (if any) from the first triangle that was found intersecting it.
If the `Material` of the `MeshRenderer` used by the voxelizer has a texture, the voxel uv will be used to sample that texture during the rendering.
If a voxel has no color or texture, a fallback color is used to paint it.

#### MeshRendererOctreeVoxelizer

Class that inherits from `MeshRendererVoxelizerBase` and uses a `Octree` data structure to voxelize a `MeshRenderer`.
The `Octree` class will be described next.

The color of a voxel is determined in the same way as in the `MeshRendererNaiveVoxelizer`.

#### Octree

A basic `Octree` data structure implementation. The algorithm consists of:
- Get dimensions of the mesh bounding box and adjust them to the global voxel dimensions.
- Check if any triangle intersects the current bounding box, if so subdivide into 8 regions.
- For each new region, check if any triangle intersects that region, if so subdivide into 8 regions.
- Keeps subdiving until a minimum region dimensions (in this case the global voxel dimensions) is reached. 

In the end, every leaf of the `Octree` that is occupied (has intersection with a triangle) is a voxel.

#### VoxelRenderer

The voxels renderer itself. The class looks for every `IVoxelizer` in the scene and joins their existing voxels in a single list.
This list is then added to a `ComputeBuffer` that is submitted to the `ComputeShader` responsible for rendering the voxels.

#### VoxelRendererFeature

This class is a `ScriptableRendererFeature` and is used to call `VoxelRenderer.Render(CommandBuffer)`. This `Render` call dispatches the `ComputeShader` and then blits 
the resulting `RenderTexture` to the screen. This render pass is injected after all rendering is done. 

#### MathHelper

Simple class with static methods to help to check [AABB-Triangle intersections](https://fileadmin.cs.lth.se/cs/Personal/Tomas_Akenine-Moller/code/tribox_tam.pdf).

### Rendering

The voxels are rendered using a `ComputeShader` called `VoxelRendererShader`. This shader was adapted from the [Inigo Quilez's](https://iquilezles.org/) shadertoy [raymarching example](https://www.shadertoy.com/view/Xds3zN), where much of it was removed, remaining basically box signed distance functions (SDFs). The shader algorithm can be explained as follows:
- For each pixel in the render target, a ray is traced from the camera passing through it.
  - If the ray hits a bounding box of a voxels volume, get the minimum distance (if any) among all the voxels in the volume and shade using the voxel and volume properties.

 This naive implementation raymarching is quite heavy for the purpose of this project, but I will talk more about it in the <a href=#challenges-and-future-work->Challenges</a> section.

## Android Build <a href="#summary">↑</a>

The project is configured to support for building for **Android** using **Vulkan** as graphics API. In case you do not want to build it, there is a build called `Voxelizer.apk` available in the folder `Android`.
It is a very simple scene using both voxelizers (naive and octree-based).

## Known Issues <a href="#summary">↑</a>

- **[BUG]** You can control the light direction in the scene, but the orientation in the renderer is different.
- **[BUG]** Soft shadows have some glitches.
- **[BUG]** AABB-Triangle intersection check fails if triangles are aligned with boxes faces. 
- Performance in general is bad, specially in the renderer. This will be discussed in the next section <a href="challenges-and-future-work-">Challenges and Future Work</a>

## Challenges and Future Work <a href="#summary">↑</a>

## Contributor <a href="#summary">↑</a>

  - Teofilo Dutra - [teodutra](https://teodutra.com)

## License <a href="#summary">↑</a>

This project is licensed under the [MIT License](/LICENSE.md).

## Third Party <a href="#summary">↑</a>

This project uses the following asset:

  - [Space Robot Kyle](https://assetstore.unity.com/packages/3d/characters/robots/space-robot-kyle-4696) - [Standard Unity Asset Store EULA](https://unity3d.com/legal/as_terms)
  
## References <a href="#summary">↑</a>

- [AABB-Triangle intersections paper](https://fileadmin.cs.lth.se/cs/Personal/Tomas_Akenine-Moller/code/tribox_tam.pdf).
- [Inigo Quilez's website](https://iquilezles.org/) 
- [Shadertoy raymarching example](https://www.shadertoy.com/view/Xds3zN)