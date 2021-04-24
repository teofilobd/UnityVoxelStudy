using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine
{
    /// <summary>
    /// Creates random voxels. Used just to test renderer.
    /// </summary>
    public class RandomVoxels : MonoBehaviour, IVoxelizer
    {
        public Vector3Int MinRange;
        public Vector3Int MaxRange;

        public List<VoxelRenderer.Voxel> Voxels { get; private set; }

        void Awake()
        {
            Voxels = new List<VoxelRenderer.Voxel>();
            Vector3Int voxelVolumeDimensions = (MaxRange - MinRange);
            int maxNumberOfVoxels = voxelVolumeDimensions.x * voxelVolumeDimensions.y * voxelVolumeDimensions.z;

            for (int i = 0; i < maxNumberOfVoxels; i++)
            {
                VoxelRenderer.Voxel voxel = new VoxelRenderer.Voxel()
                {
                    Origin = new Vector3(Random.Range(MinRange.x, MaxRange.x), 
                                         Random.Range(MinRange.y, MaxRange.y) + VoxelRenderer.kVoxelSize, 
                                         Random.Range(MinRange.z, MaxRange.z)) * 2 * VoxelRenderer.kVoxelSize,
                    Size = VoxelRenderer.kVoxelSize,
                    Color = new Vector3(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f))
                };

                // Skip if trying to place a voxel at an occupied position.
                if(Voxels.FindIndex(v => v.Origin == voxel.Origin) != -1)
                {
                    continue;
                }    

                Voxels.Add(voxel);
            }
        }

        // Start is called before the first frame update
        void OnEnable()
        {
            VoxelRenderer.RegisterVoxelizer(this);
        }

        void OnDisable()
        {
            VoxelRenderer.UnregisterVoxelizer(this);
        }
    }
}