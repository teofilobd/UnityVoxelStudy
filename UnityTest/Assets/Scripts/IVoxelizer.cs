using System.Collections.Generic;

namespace VoxelEngine
{
    /// <summary>
    /// Describes a Voxelizer. For now, a voxelizer has to provide a list of voxels to be used by the renderer.
    /// </summary>
    public interface IVoxelizer
    {
        public List<VoxelRenderer.Voxel> Voxels { get; }
    }
}
