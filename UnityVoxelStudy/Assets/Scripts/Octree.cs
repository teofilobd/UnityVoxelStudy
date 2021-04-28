using UnityEngine;

namespace VoxelEngine
{
    /// <summary>
    /// Basic Octree class for voxelization. Given a mesh, it will subdivide voxels with triangles within 
    /// them until they reach a given minimum dimension.
    /// </summary>
    public class Octree
    {
        /// <summary>
        /// Class representing a octree node.
        /// </summary>
        public class OctreeNode
        {
            public OctreeNode[] ChildrenNodes;
            // 8 vertices composing the voxel.
            public Vector3[] Vertices;            
            // A node is occupied if it has triangles in it.
            public bool Occupied;
            public bool IsLeaf { get; }                   
            public Vector3 MinPoint { get; }
            public Vector3 MaxPoint { get; }
            public Vector3 CenterPoint { get; }
            public Vector3 Dimensions { get; }
            public Vector3 HalfDimensions { get; }
            public Vector2 UV;
            public Color Color;

            public OctreeNode(Vector3 minPoint, Vector3 maxPoint, Vector3[] vertices, float minimumSize)
            {
                MinPoint = minPoint;
                MaxPoint = maxPoint;
                CenterPoint = (maxPoint - minPoint) * 0.5f + minPoint;
                Dimensions = maxPoint - minPoint;
                HalfDimensions = Dimensions * 0.5f;
                ChildrenNodes = new OctreeNode[8];
                UV = Vector2.zero;
                Color = Color.white;

                // If a node reaches a dimension threshold, it becomes a leaf.
                if(Dimensions.x <= minimumSize)
                {
                    IsLeaf = true;
                }

                Vertices = vertices;
            }
        }

        // Helper struct.
        public struct MeshParams
        {
            public int[] Triangles;
            public Vector3[] VerticesWS;
            public Vector3[] TrianglesNormals;
            public Color[] Colors;
            public Vector2[] UVs;
        }

        public OctreeNode Root;
        private float m_MinimumSize;

        private readonly Vector3[] m_MinPointRegion = new Vector3[]
        {
            Vector3.zero,
            Vector3.right,
            Vector3.up,
            Vector3.right + Vector3.up,
            Vector3.forward,
            Vector3.right + Vector3.forward,
            Vector3.up + Vector3.forward,
            Vector3.one
        };

        private Vector3[] GetNodeVertices(Vector3 minPoint, Vector3 maxPoint)
        {
            Vector3 dimensions = maxPoint - minPoint;
            return new Vector3[]
                {
                    minPoint,
                    minPoint + new Vector3(dimensions.x, 0, 0),
                    minPoint + new Vector3(0, dimensions.y, 0),
                    minPoint + new Vector3(dimensions.x, dimensions.y, 0),
                    minPoint + new Vector3(0, 0, dimensions.z),
                    minPoint + new Vector3(dimensions.x, 0, dimensions.z),
                    minPoint + new Vector3(0, dimensions.y, dimensions.z),
                    minPoint + new Vector3(dimensions.x, dimensions.y, dimensions.z)
                };
        }

        public Octree(Vector3 minPoint, Vector3 maxPoint, float minimumSize, MeshParams meshParams)
        {
            m_MinimumSize = minimumSize;
            Root = null;
            for (int triangleID = 0; triangleID < meshParams.Triangles.Length; triangleID+=3)
            {
                int vertexId1 = meshParams.Triangles[triangleID];
                int vertexId2 = meshParams.Triangles[triangleID + 1];
                int vertexId3 = meshParams.Triangles[triangleID + 2];

                Vector3[] triangleVertices = new Vector3[3]
                {
                    meshParams.VerticesWS[vertexId1],
                    meshParams.VerticesWS[vertexId2],
                    meshParams.VerticesWS[vertexId3]
                };

                Vector3 triangleNormal = meshParams.TrianglesNormals[triangleID / 3];
                Vector2 uv = Vector2.zero;
                Color color = Color.white;

                // Get mesh properties (color, uv) from first vertex in triangle and set to voxel.            
                if (meshParams.UVs.Length > 0)
                {
                    uv = meshParams.UVs[vertexId1];
                }
                if (meshParams.Colors.Length > 0)
                {
                    color = meshParams.Colors[vertexId1];
                }

                Vector3[] nodeVertices = Root != null ? Root.Vertices : GetNodeVertices(minPoint, maxPoint);

                if (MathHelper.CheckAABBAndTriangleIntersection(minPoint, maxPoint, triangleVertices,
                    nodeVertices, triangleNormal))
                {
                    ProcessRegion(ref Root, nodeVertices, minPoint, maxPoint, triangleVertices, triangleNormal, uv, color);
                }
            }
        }

        public OctreeNode ProcessRegion(ref OctreeNode currentNode, Vector3[] nodeVertices, Vector3 minPoint, Vector3 maxPoint, 
            Vector3[] triangleVertices, Vector3 triangleNormal, Vector2 uv, Color color)
        {
            currentNode ??= new OctreeNode(minPoint, maxPoint, nodeVertices, m_MinimumSize)
            {
                Occupied = true,
                UV = uv,
                Color = color
            };
         
            if (!currentNode.IsLeaf)
            {
                for (int regionId = 0; regionId < 8; ++regionId)
                {
                    OctreeNode regionNode = currentNode.ChildrenNodes[regionId];

                    Vector3 regionMinPoint;
                    Vector3 regionMaxPoint;
                    Vector3[] regionVertices;

                    if (regionNode == null)
                    {
                        regionMinPoint = currentNode.MinPoint + Vector3.Scale(m_MinPointRegion[regionId], currentNode.HalfDimensions);
                        regionMaxPoint = regionMinPoint + currentNode.HalfDimensions;
                        regionVertices = GetNodeVertices(regionMinPoint, regionMaxPoint);
                    } else
                    {
                        regionMinPoint = regionNode.MinPoint;
                        regionMaxPoint = regionNode.MaxPoint;
                        regionVertices = regionNode.Vertices;
                    }

                    if (MathHelper.CheckAABBAndTriangleIntersection(regionMinPoint, regionMaxPoint, triangleVertices,
                            regionVertices, triangleNormal))
                    {
                        ProcessRegion(ref currentNode.ChildrenNodes[regionId], regionVertices, regionMinPoint, regionMaxPoint, 
                            triangleVertices, triangleNormal, uv, color);
                    }
                }
            }            
            return currentNode;
        }
    }
}