using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine
{
    /// <summary>
    /// Abstract class describing some features needed for mesh renderers voxelizers.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public abstract class MeshRendererVoxelizerBase : MonoBehaviour, IVoxelizer
    {
        // If no material or vertex color is found, this color is used.
        public Color FallbackColor;

        public List<VoxelRenderer.Voxel> Voxels { get; protected set; }
        public VoxelRenderer.VoxelMaterial Material { get; protected set; }

        protected MeshRenderer m_MeshRenderer;
        protected MeshFilter m_MeshFilter;

        public Vector3 VoxelsVolumeMin { get; protected set; }

        public Vector3 VoxelsVolumeMax { get; protected set; }

        private void Awake()
        {
            if (m_MeshRenderer == null)
            {
                m_MeshRenderer = GetComponent<MeshRenderer>();
            }
            if (m_MeshFilter == null)
            {
                m_MeshFilter = GetComponent<MeshFilter>();
            }

            Voxels = new List<VoxelRenderer.Voxel>();

            Material meshMaterial = m_MeshRenderer.sharedMaterial;

            Material = new VoxelRenderer.VoxelMaterial()
            {
                Color = meshMaterial.HasProperty("_BaseColor") ? meshMaterial.GetColor("_BaseColor") : FallbackColor,
                Texture = meshMaterial.HasProperty("_BaseMap") ? meshMaterial.GetTexture("_BaseMap") as Texture2D : null
            };

            VoxelsVolumeMin = m_MeshRenderer.bounds.min;
            VoxelsVolumeMax = m_MeshRenderer.bounds.max;
        }

        private void OnValidate()
        {
            m_MeshRenderer = GetComponent<MeshRenderer>();
            m_MeshFilter = GetComponent<MeshFilter>();
        }

        /// <summary>
        /// Used to compute vertices in world space and triangle normals.
        /// </summary>
        protected void InitializeVerticesAndNormals(Transform meshTransform, Vector3[] meshVerticesLS, int[] meshTriangles, out Vector3[] meshVerticesWS,
           out Vector3[] trianglesNormals)
        {
            meshVerticesWS = new Vector3[meshVerticesLS.Length];

            for (int vertexId = 0; vertexId < meshVerticesLS.Length; ++vertexId)
            {
                meshVerticesWS[vertexId] = meshTransform.TransformPoint(meshVerticesLS[vertexId]);
            }

            trianglesNormals = new Vector3[meshTriangles.Length / 3];

            for (int triangleVertexID = 0; triangleVertexID < meshTriangles.Length; triangleVertexID += 3)
            {
                Vector3 v1 = meshVerticesWS[meshTriangles[triangleVertexID]];
                Vector3 v2 = meshVerticesWS[meshTriangles[triangleVertexID + 1]];
                Vector3 v3 = meshVerticesWS[meshTriangles[triangleVertexID + 2]];
                trianglesNormals[triangleVertexID / 3] = Vector3.Cross(v2 - v1, v3 - v1).normalized;
            }
        }

        void IVoxelizer.Bind(VoxelRenderer renderer)
        {
            GenerateVoxels(renderer.VoxelSize);
            renderer.Register(this);
        }

        void IVoxelizer.Unbind(VoxelRenderer renderer)
        {
            renderer.Deregister(this);
            Voxels.Clear();
        }

        public abstract void GenerateVoxels(float voxelSize);
    }
}