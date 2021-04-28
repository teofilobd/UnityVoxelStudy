using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine
{
    /// <summary>
    /// Describes a Voxelizer. For now, a voxelizer has to provide a list of voxels to be used by the renderer, a material, 
    /// voxels bounds and methods to bind/unbind with VoxelRenderer.
    /// </summary>
    public interface IVoxelizer
    {
        public List<VoxelRenderer.Voxel> Voxels { get; }
        public VoxelRenderer.VoxelMaterial Material {get;}
        public Vector3 VoxelsVolumeMin { get; }
        public Vector3 VoxelsVolumeMax { get; }

        public void Bind(VoxelRenderer renderer);
        public void Unbind(VoxelRenderer renderer);
    }
}
