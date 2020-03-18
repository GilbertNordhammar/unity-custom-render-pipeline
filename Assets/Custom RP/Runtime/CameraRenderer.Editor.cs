using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

namespace CustomRP
{
    public partial class CameraRenderer
    {
        partial void DrawGizmos();
        partial void DrawUnsupportedShaders(ref CullingResults cullingResults);
        partial void PrepareForSceneWindow();
        partial void PrepareBuffer();

#if UNITY_EDITOR

        string _sampleName { get; set; }

        static readonly ShaderTagId[] _legacyShaderTagIds = {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM")
        };

        static Material _errorMaterial;

        partial void PrepareBuffer()
        {
            Profiler.BeginSample("Editor Only");
            _buffer.name = _sampleName = _camera.name;
            Profiler.EndSample();
        }

        partial void PrepareForSceneWindow()
        {
            if (_camera.cameraType == CameraType.SceneView)
            {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(_camera);
            }
        }

        partial void DrawGizmos()
        {
            if (Handles.ShouldRenderGizmos())
            {
                _context.DrawGizmos(_camera, GizmoSubset.PreImageEffects);
                _context.DrawGizmos(_camera, GizmoSubset.PostImageEffects);
            }
        }

        partial void DrawUnsupportedShaders(ref CullingResults cullingResults)
        {
            if(_errorMaterial == null)
            {
                _errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            var drawingSettings = new DrawingSettings() { overrideMaterial = _errorMaterial };
            drawingSettings.sortingSettings = new SortingSettings(_camera);
            for (int i = 0; i < _legacyShaderTagIds.Length; i++)
            {
                drawingSettings.SetShaderPassName(i, _legacyShaderTagIds[i]);
            }

            var filterSettings = FilteringSettings.defaultValue;

            _context.DrawRenderers(cullingResults, ref drawingSettings, ref filterSettings);
        }
#else 
        const string _sampleName = _bufferName;
#endif
    }
}