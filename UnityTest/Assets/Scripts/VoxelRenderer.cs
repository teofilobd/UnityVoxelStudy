using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace VoxelEngine
{
    /// <summary>
    /// Class in charge of setting up voxels compute buffers and dispatching voxel renderer compute shader.
    /// The class will look for IVoxelizers in the scene and generate voxels for them.
    /// 
    /// I adapted a few functionalities from my ray tracer https://github.com/teofilobd/URP-RayTracer
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class VoxelRenderer : MonoBehaviour
    {
        #region Structs
        /// <summary>
        /// Describes a voxel. Other than its center and size, a voxel also has 
        /// color and uv (picked from a vertex within the voxel, if any),
        /// and an ID for volume (set of voxels of same mesh) properties in 
        /// a list of properties.
        /// This struct has an equivalent in the compute shader.
        /// </summary>
        public struct Voxel
        {
            public Vector3 Center;
            public float Size;
            public Vector3 Color;
            public Vector2 UV;
            public int VoxelsVolumePropertiesID;
        }

        public struct VoxelMaterial
        {
            public Texture2D Texture;
            public Color Color;
        }

        /// <summary>
        /// Properties of a set of voxels representing a mesh.
        /// MaterialTextureID keeps an id for a texture in a texture array.
        /// This struct has an equivalent in the compute shader.
        /// </summary>
        public struct VoxelsVolumeProperties
        {
            public int MaterialTextureID;
            public int VoxelStartID;
            public int VoxelsCount;
            public Vector3 MaterialColor;
            public Vector3 VolumeCenter;
            public Vector3 VolumeHalfDimensions;
        }
        #endregion

        #region Properties
        public ComputeShader VoxelRendererShader;
        public Light DirectionalLight;
        public static float kVoxelSize = 0.1f;
        public static Vector3 kVoxelDimensions = new Vector3(kVoxelSize, kVoxelSize, kVoxelSize);
        public static Vector3 kVoxelHalfDimensions = kVoxelDimensions / 2f;
        public static Vector3[] kVoxelVertices =
        {
            Vector3.zero,
            new Vector3(VoxelRenderer.kVoxelSize, 0, 0),
            new Vector3(VoxelRenderer.kVoxelSize, VoxelRenderer.kVoxelSize, 0),
            new Vector3(0, VoxelRenderer.kVoxelSize, 0),
            new Vector3(0, 0, VoxelRenderer.kVoxelSize),
            new Vector3(VoxelRenderer.kVoxelSize, 0, VoxelRenderer.kVoxelSize),
            new Vector3(0, VoxelRenderer.kVoxelSize, VoxelRenderer.kVoxelSize),
            new Vector3(VoxelRenderer.kVoxelSize, VoxelRenderer.kVoxelSize, VoxelRenderer.kVoxelSize)
        };

        private int m_KiMain = -1;
        private int m_ScreenWidth;
        private int m_ScreenHeight;
        private Camera m_Camera;
        private RenderTexture m_Target;
        private ComputeBuffer m_VoxelsBuffer;
        private ComputeBuffer m_VoxelsVolumePropertiesBuffer;
        private Texture2DArray m_TextureBuffer;
        private HashSet<IVoxelizer> m_Voxelizers = new HashSet<IVoxelizer>();
        private List<Texture2D> m_Textures = new List<Texture2D>();
        private List<VoxelsVolumeProperties> m_VoxelVolumeProperties = new List<VoxelsVolumeProperties>();
        private bool m_VoxelsBufferNeedUpdate = false;
        private List<Voxel> m_Voxels = new List<Voxel>();
        private const float m_ThreadNumber = 32.0f;
        private int m_ThreadGroupsX = Mathf.CeilToInt(Screen.width / m_ThreadNumber);
        private int m_ThreadGroupsY = Mathf.CeilToInt(Screen.height / m_ThreadNumber);
        private readonly int m_CameraToWorldPropertyID = Shader.PropertyToID("_CameraToWorld");
        private readonly int m_CameraInverseProjectioPropertyID = Shader.PropertyToID("_CameraInverseProjection");
        private readonly int m_DirectionalLightPropertyID = Shader.PropertyToID("_DirectionalLightDirection");
        private readonly int m_VoxelsCountPropertyID = Shader.PropertyToID("_VoxelsCount");
        #endregion

        private void Awake()
        {
            m_KiMain = VoxelRendererShader.FindKernel("CSMain");
            m_Camera = GetComponent<Camera>();
            m_ScreenWidth = Screen.width;
            m_ScreenHeight = Screen.height;
            m_ThreadGroupsX = Mathf.CeilToInt(Screen.width / m_ThreadNumber);
            m_ThreadGroupsY = Mathf.CeilToInt(Screen.height / m_ThreadNumber);

            // Same seed for testing.
            Random.InitState(0);
        }

        private void Start()
        {
            Assert.IsNotNull(DirectionalLight, "There is no directional light in the scene.");
            Assert.IsNotNull(VoxelRendererShader, "Renderer shader was not assigned.");

            BindVoxelizers();
        }

        void BindVoxelizers()
        {
            var voxelizers = FindObjectsOfType<MonoBehaviour>().OfType<IVoxelizer>().ToList();

            if (voxelizers != null)
            {
                for(int voxelizerId = voxelizers.Count - 1; voxelizerId >=0; --voxelizerId)                
                {
                    voxelizers[voxelizerId]?.Bind(this);
                }
            }
        }

        void UnbindVoxelizers()
        {
            var voxelizers = FindObjectsOfType<MonoBehaviour>().OfType<IVoxelizer>().ToList();

            if (voxelizers != null)
            {
                for (int voxelizerId = voxelizers.Count - 1; voxelizerId >= 0; --voxelizerId)
                {
                    voxelizers[voxelizerId]?.Unbind(this);
                }
            }
        }

        private void OnDestroy() => UnbindVoxelizers();

        private void OnEnable() => BindVoxelizers();

        // This is bad because I'm reconstructing all the buffers every time a voxelizer is bind/unbind.
        // It has to be improved in the future.
        private void UpdateVoxelsBuffer()
        {
            m_Voxels.Clear();
            m_VoxelVolumeProperties.Clear();
            m_Textures.Clear();

            foreach(IVoxelizer voxelizer in m_Voxelizers)
            {
                VoxelsVolumeProperties voxelVolumeProperties = new VoxelsVolumeProperties
                {
                    MaterialColor = new Vector3(voxelizer.Material.Color.r, voxelizer.Material.Color.g, voxelizer.Material.Color.b),
                    VolumeCenter = (voxelizer.VoxelsVolumeMax - voxelizer.VoxelsVolumeMin) * 0.5f + voxelizer.VoxelsVolumeMin,
                    VolumeHalfDimensions = (voxelizer.VoxelsVolumeMax - voxelizer.VoxelsVolumeMin) * 0.5f,
                    VoxelStartID = m_Voxels.Count,
                    VoxelsCount = voxelizer.Voxels.Count
                };

                if (voxelizer.Material.Texture != null)
                {
                    m_Textures.Add(voxelizer.Material.Texture);
                    voxelVolumeProperties.MaterialTextureID = m_Textures.Count - 1;
                }
                else
                {
                    voxelVolumeProperties.MaterialTextureID = -1;
                }

                m_VoxelVolumeProperties.Add(voxelVolumeProperties);

                int voxelVolumePropertiesId = m_VoxelVolumeProperties.Count - 1;

                for (int voxelId = 0; voxelId < voxelizer.Voxels.Count; ++voxelId)
                {
                    var voxel = voxelizer.Voxels[voxelId];
                    voxel.VoxelsVolumePropertiesID = voxelVolumePropertiesId;
                    voxelizer.Voxels[voxelId] = voxel;
                }

                m_Voxels.AddRange(voxelizer.Voxels);
            }

            Debug.Log($"Number of voxels: {m_Voxels.Count}");

            if (m_Textures.Count > 0)
            {
                m_TextureBuffer = CreateTextureArray(m_Textures.ToArray());
            } else
            {
                m_TextureBuffer = CreateTextureArray(new Texture2D[] { Texture2D.whiteTexture });
            }
            VoxelRendererShader.SetTexture(m_KiMain, "_TextureBuffer", m_TextureBuffer);

            int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Voxel));
            CreateComputeBuffer(ref m_VoxelsBuffer, m_Voxels, stride);
            SetComputeBuffer(m_KiMain, "_Voxels", m_VoxelsBuffer);

            stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(VoxelsVolumeProperties));
            CreateComputeBuffer(ref m_VoxelsVolumePropertiesBuffer, m_VoxelVolumeProperties, stride);
            SetComputeBuffer(m_KiMain, "_VoxelsVolumeProperties", m_VoxelsVolumePropertiesBuffer);             
        }

        private void Update()
        {
            if (m_ScreenWidth != Screen.width || m_ScreenHeight != Screen.height)
            {
                m_ScreenWidth = Screen.width;
                m_ScreenHeight = Screen.height;
                m_ThreadGroupsX = Mathf.CeilToInt(Screen.width / m_ThreadNumber);
                m_ThreadGroupsY = Mathf.CeilToInt(Screen.height / m_ThreadNumber);
            }
        }

        private void OnDisable()
        {
            m_VoxelsBuffer?.Release();
            m_VoxelsVolumePropertiesBuffer?.Release();
            
            if (m_TextureBuffer != null)
            {
                Destroy(m_TextureBuffer);
            }

            UnbindVoxelizers();
        }

        private void OnValidate()
        {
            if (DirectionalLight == null)
            {
                var lights = FindObjectsOfType<Light>();
                foreach (var light in lights)
                {
                    if (light.type == LightType.Directional)
                    {
                        DirectionalLight = light;
                        break;
                    }
                }
            }
        }

        public void Render(CommandBuffer cmd)
        { 
            InitRenderTexture();

            bool somethingHasChanged = false;

            if (m_Camera.transform.hasChanged)
            {
                VoxelRendererShader.SetMatrix(m_CameraToWorldPropertyID, m_Camera.cameraToWorldMatrix);
                VoxelRendererShader.SetMatrix(m_CameraInverseProjectioPropertyID, m_Camera.projectionMatrix.inverse);

                m_Camera.transform.hasChanged = false;
                somethingHasChanged = true;
            }

            if (DirectionalLight.transform.hasChanged)
            {
                Vector3 lightForward = -DirectionalLight.transform.forward;
                VoxelRendererShader.SetVector(m_DirectionalLightPropertyID, new Vector4(lightForward.x, lightForward.y, lightForward.z, DirectionalLight.intensity));

                DirectionalLight.transform.hasChanged = false;
                somethingHasChanged = true;
            }

            if (m_VoxelsBufferNeedUpdate)
            {
                UpdateVoxelsBuffer();
                VoxelRendererShader.SetInt(m_VoxelsCountPropertyID, m_Voxels.Count);

                m_VoxelsBufferNeedUpdate = false;
                somethingHasChanged = true;
            }

            if (somethingHasChanged)
            {
                VoxelRendererShader.SetTexture(m_KiMain, "Result", m_Target);
                VoxelRendererShader.Dispatch(m_KiMain, m_ThreadGroupsX, m_ThreadGroupsY, 1);
            }

            // Blit the result texture to the screen.       
            cmd.Blit(m_Target, BuiltinRenderTextureType.CurrentActive);
        }

        private void InitRenderTexture()
        {
            if (m_Target == null || m_Target.width != Screen.width || m_Target.height != Screen.height)
            {
                // Release render texture if we already have one.
                if (m_Target != null)
                {
                    m_Target.Release();
                }
                
                // Get a render target for compute shader.
                m_Target = new RenderTexture(Screen.width, Screen.height, 24,
                    RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                m_Target.enableRandomWrite = true;
                m_Target.Create();
            }
        }

        public void Register(IVoxelizer voxelizer)
        {
            m_Voxelizers.Add(voxelizer);
            m_VoxelsBufferNeedUpdate = true;
        }

        public void Deregister(IVoxelizer voxelizer)
        {
            m_Voxelizers.Remove(voxelizer);
            m_VoxelsBufferNeedUpdate = true;
        }

        #region Helpers
        // Reference: https://catlikecoding.com/unity/tutorials/hex-map/part-14/
        private Texture2DArray CreateTextureArray(Texture2D[] textures)
        {
            if (textures == null || textures.Length == 0)
            {
                return null;
            }

            Texture2D firstTex = textures[0];
            Texture2DArray texArray = new Texture2DArray(firstTex.width,
                                                        firstTex.height,
                                                        textures.Length,
                                                        firstTex.format,
                                                        firstTex.mipmapCount > 0);
            texArray.anisoLevel = firstTex.anisoLevel;
            texArray.filterMode = firstTex.filterMode;
            texArray.wrapMode = firstTex.wrapMode;

            for (int i = 0; i < textures.Length; i++)
            {
                for (int m = 0; m < firstTex.mipmapCount; m++)
                {
                    Graphics.CopyTexture(textures[i], 0, m, texArray, i, m);
                }
            }

            texArray.Apply();

            return texArray;
        }

        private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
    where T : struct
        {
            // Do we already have a compute buffer?
            if (buffer != null)
            {
                // If no data or buffer doesn't match the given criteria, release it.
                if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
                {
                    buffer.Release();
                    buffer = null;
                }
            }
            if (data.Count != 0)
            {
                // If the buffer has been released or wasn't there to
                // begin with, create it.
                if (buffer == null)
                {
                    buffer = new ComputeBuffer(data.Count, stride);
                }

                // Set data on the buffer.
                buffer.SetData(data);
            }
        }

        private void SetComputeBuffer(int kernelIndex, string name, ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                VoxelRendererShader.SetBuffer(kernelIndex, name, buffer);
            }
        }
        #endregion
    }
}