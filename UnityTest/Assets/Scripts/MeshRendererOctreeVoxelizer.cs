using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine
{
    /// <summary>
    /// Class responsible for voxelizing mesh renderers using an octree.
    /// Checks for triangles in voxels and keeps subdividing until reaching a minimum dimension threshold.
    /// </summary>
    public class MeshRendererOctreeVoxelizer : MeshRendererVoxelizerBase
    {
        private Octree m_Octree;

        public override void GenerateVoxels(float voxelSize)
        {
            if (m_MeshRenderer != null && m_MeshFilter != null)
            {
                Mesh mesh = m_MeshFilter.sharedMesh;
                int[] triangles = mesh.triangles;

                // mesh vertices in World Space      
                Vector3[] verticesWS;
                Vector3[] trianglesNormals;
                InitializeVerticesAndNormals(m_MeshRenderer.transform, mesh.vertices, triangles, out verticesWS, out trianglesNormals);

                Bounds meshBoundsWS = m_MeshRenderer.bounds;

                Octree.MeshParams meshParams = new Octree.MeshParams()
                {
                    Triangles = triangles,
                    VerticesWS = verticesWS,
                    TrianglesNormals = trianglesNormals,
                    Colors = mesh.colors,
                    UVs = mesh.uv
                };

                Vector3Int meshDimensionInVoxels = Vector3Int.CeilToInt(meshBoundsWS.size / voxelSize);
                
                // In order to keep voxels with uniform size, the max dimension is used as bounds dimension.
                int maxDimension = Mathf.Max(meshDimensionInVoxels.x, Mathf.Max(meshDimensionInVoxels.y, meshDimensionInVoxels.z));                
                Vector3 maxPointInVoxelsVolume = meshBoundsWS.min + Vector3.one * maxDimension * voxelSize;

                m_Octree = new Octree(meshBoundsWS.min, maxPointInVoxelsVolume, voxelSize, meshParams);

                Queue<Octree.OctreeNode> nodes = new Queue<Octree.OctreeNode>();

                if (m_Octree.Root != null)
                {
                    nodes.Enqueue(m_Octree.Root);
                }

                while (nodes.Count > 0)
                {
                    Octree.OctreeNode currentNode = nodes.Dequeue();

                    // If node is leaf and has triangles inside it, create a voxel.
                    if (currentNode.IsLeaf && currentNode.Occupied)
                    {
                        Color color = Material.Color * currentNode.Color;
                        VoxelRenderer.Voxel voxel = new VoxelRenderer.Voxel
                        {
                            Center = currentNode.CenterPoint,
                            Size = currentNode.Dimensions.x,
                            Color = new Vector3(color.r, color.g, color.b),
                            UV = currentNode.UV
                        };
                        Voxels.Add(voxel);
                    }
                    else
                    {
                        if (currentNode.ChildrenNodes != null)
                        {
                            foreach (Octree.OctreeNode octreeNode in currentNode.ChildrenNodes)
                            {
                                if (octreeNode != null)
                                {
                                    nodes.Enqueue(octreeNode);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
