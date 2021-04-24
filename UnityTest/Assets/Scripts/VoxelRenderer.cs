using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace VoxelEngine
{
    /// <summary>
    /// Class in charge of setting up voxels compute buffer and dispatching voxel renderer compute shader.
    /// 
    /// I adapted a few functionalities from my ray tracer https://github.com/teofilobd/URP-RayTracer
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class VoxelRenderer : MonoBehaviour
    {
        public struct Voxel
        {
            public Vector3 Origin;
            public float Size;
            public Vector3 Color;
        }

        public ComputeShader VoxelRendererShader;
        public Light DirectionalLight;
        public static float kVoxelSize = 0.25f;

        private int m_KiMain = -1;
        private int m_ScreenWidth;
        private int m_ScreenHeight;
        private Camera m_Camera;
        private RenderTexture m_Target;
        private ComputeBuffer m_VoxelsBuffer;
        private static List<IVoxelizer> m_Voxelizers = new List<IVoxelizer>();
        private static bool m_VoxelsBufferNeedUpdate = false;
        private List<Voxel> m_Voxels = new List<Voxel>();

        private void Awake()
        {
            m_KiMain = VoxelRendererShader.FindKernel("CSMain");
            m_Camera = GetComponent<Camera>();
            m_ScreenWidth = Screen.width;
            m_ScreenHeight = Screen.height;
            Random.InitState(0);
        }

        private void Start()
        {
            Assert.IsNotNull(DirectionalLight, "There is no directional light in the scene.");
            Assert.IsNotNull(VoxelRendererShader, "Renderer shader was not assigned.");
        }

        private void UpdateVoxelsBuffer()
        {
            m_Voxels.Clear();
            foreach(IVoxelizer voxelizer in m_Voxelizers)
            {
                m_Voxels.AddRange(voxelizer.Voxels);
            }

            CreateComputeBuffer(ref m_VoxelsBuffer, m_Voxels, 28);
            SetComputeBuffer(m_KiMain, "_Voxels", m_VoxelsBuffer);
        }

        private void Update()
        {
            if (m_ScreenWidth != Screen.width || m_ScreenHeight != Screen.height)
            {
                m_ScreenWidth = Screen.width;
                m_ScreenHeight = Screen.height;
            }
        }

        private void OnDisable()
        {
            m_VoxelsBuffer?.Release();
        }

        public void Render(CommandBuffer cmd)
        { 
            InitRenderTexture();
            SetShaderParameters();

            if(m_VoxelsBufferNeedUpdate)
            {
                UpdateVoxelsBuffer();
                m_VoxelsBufferNeedUpdate = false;
            }    

            VoxelRendererShader.SetTexture(m_KiMain, "Result", m_Target);
            int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
            VoxelRendererShader.Dispatch(m_KiMain, threadGroupsX, threadGroupsY, 1);

            // Blit the result texture to the screen.       
            cmd.Blit(m_Target, BuiltinRenderTextureType.CurrentActive);
        }

        private void SetShaderParameters()
        {
            VoxelRendererShader.SetMatrix("_CameraToWorld", m_Camera.cameraToWorldMatrix);
            VoxelRendererShader.SetMatrix("_CameraInverseProjection", m_Camera.projectionMatrix.inverse);

            Vector3 lightForward = -DirectionalLight.transform.forward;        
            VoxelRendererShader.SetVector("_DirectionalLightDirection", new Vector4(lightForward.x, lightForward.y, lightForward.z, DirectionalLight.intensity));
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

        public static void RegisterVoxelizer(IVoxelizer voxelizer)
        {
            m_Voxelizers.Add(voxelizer);
            m_VoxelsBufferNeedUpdate = true;
        }

        public static void UnregisterVoxelizer(IVoxelizer voxelizer)
        {
            m_Voxelizers.Remove(voxelizer);
            m_VoxelsBufferNeedUpdate = true;
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

        #region Compute Buffer Helpers
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