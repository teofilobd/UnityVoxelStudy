using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace VoxelEngine
{
    /// <summary>
    /// Class responsible for voxelizing mesh renderers using a naive approach.
    /// Checks for triangles in every voxel in the mesh renderer bounds, if found add to a list of voxels.
    /// I tried to make it faster using Tasks.
    /// </summary>
    public class MeshRendererNaiveVoxelizer : MeshRendererVoxelizerBase
    {
        private VoxelRenderer.Voxel[] m_Voxels;
        private List<Task> m_Tasks = new List<Task>();
        private Vector3 m_VoxelHalfDimensions;
        private Vector3[] m_VoxelVertices;
        protected float m_VoxelSize;

        // Helper struct.
        private struct TaskParams
        {
            public Vector3 VoxelCenter;
            public int VoxelId;
            public int[] Triangles;
            public Vector3[] VerticesWS;
            public Vector3[] TrianglesNormals;
            public Color[] Colors;
            public Vector2[] UVs;
        }

        public override void GenerateVoxels(float voxelSize)
        {
            m_VoxelSize = voxelSize;
            m_VoxelHalfDimensions = new Vector3(voxelSize, voxelSize, voxelSize) * 0.5f;
            m_VoxelVertices = new Vector3[]
            {
                Vector3.zero,
                new Vector3(voxelSize, 0, 0),
                new Vector3(voxelSize, voxelSize, 0),
                new Vector3(0, voxelSize, 0),
                new Vector3(0, 0, voxelSize),
                new Vector3(voxelSize, 0, voxelSize),
                new Vector3(0, voxelSize, voxelSize),
                new Vector3(voxelSize, voxelSize, voxelSize)
            };
            
            CreateVoxels(true);
        }

        private void CreateVoxels(bool doAsync)
        {
            if (m_MeshRenderer != null && m_MeshFilter != null)
            {
                Mesh mesh = m_MeshFilter.sharedMesh;
                int[] triangles = mesh.triangles;
                Color[] colors = mesh.colors;
                Vector2[] uvs = mesh.uv;

                // mesh vertices in World Space      
                Vector3[] verticesWS;
                Vector3[] trianglesNormals;
                InitializeVerticesAndNormals(m_MeshRenderer.transform, mesh.vertices, triangles, out verticesWS, out trianglesNormals);

                Bounds meshBoundsWS = m_MeshRenderer.bounds;
                Vector3Int meshDimensionInVoxels = Vector3Int.CeilToInt(meshBoundsWS.size / m_VoxelSize);                                
                Vector3 voxelsInitialPosition = meshBoundsWS.min + m_VoxelHalfDimensions;

                // To avoid issues manipulating the voxels list within threads, this array of voxels will be used.
                // Each voxel will write to their corresponding id.
                m_Voxels = new VoxelRenderer.Voxel[meshDimensionInVoxels.x * meshDimensionInVoxels.y * meshDimensionInVoxels.z];

                if(doAsync)
                {
                    m_Tasks.Clear();
                }

                int currentVoxelID = -1;
                for (int x = 0; x < meshDimensionInVoxels.x; ++x)
                {
                    for (int y = 0; y < meshDimensionInVoxels.y; ++y)
                    {
                        for (int z = 0; z < meshDimensionInVoxels.z; ++z)
                        {
                            currentVoxelID++;
                            Vector3 voxelCenter = voxelsInitialPosition + new Vector3(x, y, z) * m_VoxelSize;
                            
                            TaskParams taskParams = new TaskParams()
                            {
                                VoxelCenter = voxelCenter,
                                VoxelId = currentVoxelID,
                                Triangles = triangles,
                                VerticesWS = verticesWS,
                                TrianglesNormals = trianglesNormals,
                                Colors = colors,
                                UVs = uvs
                            };

                            if (doAsync)
                            {
                                m_Tasks.Add(Task.Factory.StartNew(ProcessMesh, taskParams));
                            }
                            else
                            {
                                ProcessMesh(taskParams);
                            }
                        }
                    }
                }

                if(doAsync)
                { 
                    Task t = Task.WhenAll(m_Tasks);
                    t.Wait();
                }
                    
                Voxels = m_Voxels.Select(v => v).Where(v => v.Size != 0).ToList();
            }
        }

        private void ProcessMesh(object voxelParameters)
        {
            TaskParams voxelParams = (TaskParams) voxelParameters;
            Vector3 voxelCenter = voxelParams.VoxelCenter;
            int voxelId = voxelParams.VoxelId;
            int[] triangles = voxelParams.Triangles;
            Vector3[] verticesWS = voxelParams.VerticesWS;
            Vector3[] trianglesNormals = voxelParams.TrianglesNormals;
            Color[] colors = voxelParams.Colors;
            Vector2[] uvs = voxelParams.UVs;

            Vector3 voxelMinPosition = voxelCenter - m_VoxelHalfDimensions;
            Vector3[] triangleVertices = new Vector3[3];

            for (int triangleId = 0; triangleId < triangles.Length; triangleId += 3)
            {
                int vertexId1 = triangles[triangleId];
                int vertexId2 = triangles[triangleId + 1];
                int vertexId3 = triangles[triangleId + 2];

                // Triangle vertices with respect to voxel.
                triangleVertices[0] = verticesWS[vertexId1] - voxelMinPosition;
                triangleVertices[1] = verticesWS[vertexId2] - voxelMinPosition;
                triangleVertices[2] = verticesWS[vertexId3] - voxelMinPosition;

                Vector3 triangleNormal = trianglesNormals[triangleId / 3];

                if (MathHelper.CheckAABBAndTriangleIntersection(m_VoxelVertices[0], m_VoxelVertices[7], triangleVertices,
                    m_VoxelVertices, triangleNormal))
                {
                    Vector2 uv = Vector2.zero;
                    Color color = Material.Color;

                    // Get mesh properties (color, uv) from first vertex in triangle and set to voxel.
                    if (uvs.Length > 0)
                    {
                        uv = uvs[vertexId1];
                    }
                    if (colors.Length > 0)
                    {
                        color *= colors[vertexId1];
                    }

                    VoxelRenderer.Voxel voxel = new VoxelRenderer.Voxel
                    {
                        Center = voxelCenter,
                        Size = m_VoxelSize,
                        Color = new Vector3(color.r, color.g, color.b),
                        UV = uv,
                    };

                    m_Voxels[voxelId] = voxel;
                    break;
                }
            }
        }
    }
}