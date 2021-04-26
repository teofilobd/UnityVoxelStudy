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
        /// Class representing a octree node
        /// </summary>
        public class OctreeNode
        {
            public OctreeNode ParentNode;
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

            public OctreeNode(OctreeNode parent, Vector3 minPoint, Vector3 maxPoint)
            {
                ParentNode = parent;
                MinPoint = minPoint;
                MaxPoint = maxPoint;
                CenterPoint = (maxPoint - minPoint) * 0.5f + minPoint;
                Dimensions = maxPoint - minPoint;
                HalfDimensions = Dimensions * 0.5f;
                ChildrenNodes = null;
                UV = Vector2.zero;
                Color = Color.white;

                // If a node reaches a dimension threshold, it becomes a leaf.
                if(Dimensions.x <= VoxelRenderer.kVoxelDimensions.x)
                {
                    IsLeaf = true;
                }

                Vertices = new Vector3[8];

                Vertices[0] = minPoint;
                Vertices[1] = minPoint + new Vector3(Dimensions.x, 0 , 0);
                Vertices[2] = minPoint + new Vector3(0, Dimensions.y, 0);
                Vertices[3] = minPoint + new Vector3(Dimensions.x, Dimensions.y, 0);
                Vertices[4] = minPoint + new Vector3(0, 0, Dimensions.z);
                Vertices[5] = minPoint + new Vector3(Dimensions.x, 0, Dimensions.z);
                Vertices[6] = minPoint + new Vector3(0, Dimensions.y, Dimensions.z);
                Vertices[7] = minPoint + new Vector3(Dimensions.x, Dimensions.y, Dimensions.z);
            }

            public void CreateChildren()
            {
                ChildrenNodes = new OctreeNode[8];
                ChildrenNodes[0] = new OctreeNode(this, MinPoint, MinPoint + HalfDimensions);
                ChildrenNodes[1] = new OctreeNode(this, MinPoint + new Vector3(HalfDimensions.x, 0, 0), MinPoint + new Vector3(HalfDimensions.x, 0, 0) + HalfDimensions);
                ChildrenNodes[2] = new OctreeNode(this, MinPoint + new Vector3(0, HalfDimensions.y, 0), MinPoint + new Vector3(0, HalfDimensions.y, 0) + HalfDimensions);
                ChildrenNodes[3] = new OctreeNode(this, MinPoint + new Vector3(HalfDimensions.x, HalfDimensions.y, 0), MinPoint + new Vector3(HalfDimensions.x, HalfDimensions.y, 0) + HalfDimensions);
                ChildrenNodes[4] = new OctreeNode(this, MinPoint + new Vector3(0, 0, HalfDimensions.z), MinPoint + new Vector3(0, 0, HalfDimensions.z ) + HalfDimensions);
                ChildrenNodes[5] = new OctreeNode(this, MinPoint + new Vector3(HalfDimensions.x, 0, HalfDimensions.z), MinPoint + new Vector3(HalfDimensions.x, 0, HalfDimensions.z) + HalfDimensions);
                ChildrenNodes[6] = new OctreeNode(this, MinPoint + new Vector3(0, HalfDimensions.y, HalfDimensions.z), MinPoint + new Vector3(0, HalfDimensions.y, HalfDimensions.z) + HalfDimensions);
                ChildrenNodes[7] = new OctreeNode(this, MinPoint + new Vector3(HalfDimensions.x, HalfDimensions.y, HalfDimensions.z), MinPoint + new Vector3(HalfDimensions.x, HalfDimensions.y, HalfDimensions.z) + HalfDimensions);
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

        public Octree(Vector3 minPoint, Vector3 maxPoint, MeshParams meshParams)
        {
            Root = new OctreeNode(null, minPoint, maxPoint);

            ProcessNode(Root, meshParams);
        }

        public void ProcessNode(OctreeNode node, MeshParams meshParams)
        {
            bool subdivide = false;

            for (int triangleId = 0; triangleId < meshParams.Triangles.Length; triangleId += 3)
            {
                int vertexId1 = meshParams.Triangles[triangleId];
                int vertexId2 = meshParams.Triangles[triangleId + 1];
                int vertexId3 = meshParams.Triangles[triangleId + 2];

                Vector3[] triangleVertices = new Vector3[3]
                {
                    meshParams.VerticesWS[vertexId1],
                    meshParams.VerticesWS[vertexId2],
                    meshParams.VerticesWS[vertexId3]
                };

                Vector3 triangleNormal = meshParams.TrianglesNormals[triangleId / 3];

                // Check if a triangle is inside the voxel.
                if (MathHelper.CheckAABBAndTriangleIntersection(node.MinPoint, node.MaxPoint, triangleVertices,
                    node.Vertices, triangleNormal))
                {
                    if (!node.IsLeaf)
                    {
                        subdivide = true;
                    }

                    node.Occupied = true;

                    // Get mesh properties (color, uv) from first vertex in triangle and set to voxel.
                    if(meshParams.UVs.Length > 0)
                    {
                        node.UV = meshParams.UVs[vertexId1];
                    }
                    if (meshParams.Colors.Length > 0)
                    {
                        node.Color = meshParams.Colors[vertexId1];
                    }
                    break;
                }                
            }

            if (subdivide)
            {
                node.CreateChildren();
                foreach(OctreeNode octreeNode in node.ChildrenNodes)
                {
                    ProcessNode(octreeNode, meshParams);
                }
            }
        }
    }
}