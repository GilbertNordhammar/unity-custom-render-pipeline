using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP
{
    public partial class CameraRenderer
    {
        ScriptableRenderContext _context;
        Camera _camera;

        const string _bufferName = "Render Camera";
        readonly CommandBuffer _buffer = new CommandBuffer { name = _bufferName };

        Lighting _lighting = new Lighting();

        static readonly ShaderTagId _unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        static readonly ShaderTagId _litShaderTagId = new ShaderTagId("CustomLit");

        public void Render(ScriptableRenderContext context, Camera camera, 
                           bool useDynamicBatching, bool useGPUInstancing,
                           ShadowSettings shadowSettings)
        {
            _context = context;
            _camera = camera;

            PrepareBuffer();
            PrepareForSceneWindow();

            CullingResults cullingResults;
            if(!Cull(out cullingResults, shadowSettings.maxDistance))
            {
                return;
            }

            _buffer.BeginSample(_sampleName);
            ExecuteBuffer();
            _lighting.Setup(context, cullingResults, shadowSettings);
            _buffer.EndSample(_sampleName);
            Setup(); 
            DrawVisibleGeometry(ref cullingResults, useDynamicBatching, useGPUInstancing);
            DrawUnsupportedShaders(ref cullingResults);
            DrawGizmos();
            _lighting.Cleanup();
            Submit();
        }

        void DrawVisibleGeometry(ref CullingResults cullingResults, bool useDynamicBatching, bool useGPUInstancing)
        {
            var sortingSettings = new SortingSettings(_camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(_unlitShaderTagId, sortingSettings) {
                enableDynamicBatching = useDynamicBatching,
                enableInstancing = useGPUInstancing,
                perObjectData = PerObjectData.Lightmaps | PerObjectData.ShadowMask | 
                PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
                PerObjectData.LightProbeProxyVolume |
                PerObjectData.OcclusionProbeProxyVolume
            };
            drawingSettings.SetShaderPassName(1, _litShaderTagId);
            var filterSettings = new FilteringSettings(RenderQueueRange.opaque);
            
            _context.DrawRenderers(cullingResults, ref drawingSettings, ref filterSettings);

            _context.DrawSkybox(_camera);

            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filterSettings.renderQueueRange = RenderQueueRange.transparent;
            _context.DrawRenderers(cullingResults, ref drawingSettings, ref filterSettings);
        }

        void Submit()
        {
            _buffer.EndSample(_sampleName);
            ExecuteBuffer(); 
            _context.Submit();
        }

        void Setup()
        {
            _context.SetupCameraProperties(_camera);
            var flags = _camera.clearFlags;
            _buffer.ClearRenderTarget(clearDepth: flags <= CameraClearFlags.Depth, 
                                      clearColor: flags == CameraClearFlags.Color, 
                                      backgroundColor: flags == CameraClearFlags.Color ? _camera.backgroundColor.linear : Color.clear);
            _buffer.BeginSample(_sampleName);
            ExecuteBuffer();
        }

        void ExecuteBuffer()
        {
            _context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }

        bool Cull(out CullingResults cullingResults, float maxShadowDistance)
        {
            var successfulCull = false;
            if (_camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParams))
            {
                cullingParams.shadowDistance = Mathf.Min(maxShadowDistance, _camera.farClipPlane);
                cullingResults = _context.Cull(ref cullingParams);
                successfulCull = true;
            }
            else
            {
                cullingResults = new CullingResults();
            }
            return successfulCull;
        }
    }
}