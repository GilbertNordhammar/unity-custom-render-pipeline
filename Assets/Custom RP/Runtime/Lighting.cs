using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP
{
    public class Lighting
    {

        const string _bufferName = "Lighting";
        CommandBuffer _buffer = new CommandBuffer
        {
            name = _bufferName
        };

        const int _maxDirLightCount = 4;
        Shadows _shadows = new Shadows();

        static readonly int
            _dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
            _dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
            _dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
            _dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

        static readonly Vector4[]
            _dirLightColors = new Vector4[_maxDirLightCount],
            _dirLightDirections = new Vector4[_maxDirLightCount],
            _dirLightShadowData = new Vector4[_maxDirLightCount];

        public void Setup(ScriptableRenderContext context, CullingResults cullingResults,
                          ShadowSettings shadowSettings)
        {
            _buffer.BeginSample(_bufferName);

            _shadows.Setup(context, cullingResults, shadowSettings);
            SetupLights(cullingResults);
            _shadows.Render();
            
            _buffer.EndSample(_bufferName);

            context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }

        void SetupLights(CullingResults cullingResults)
        {
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
            int dirLightCount = 0;
            for (int i = 0; i < visibleLights.Length; i++)
            {
                VisibleLight visibleLight = visibleLights[i];
                if (visibleLight.lightType == LightType.Directional)
                {
                    SetupDirectionalLight(i, ref visibleLight);
                    if (dirLightCount >= _maxDirLightCount)
                    {
                        break;
                    }
                }
            }

            _buffer.SetGlobalInt(_dirLightCountId, visibleLights.Length);
            _buffer.SetGlobalVectorArray(_dirLightColorsId, _dirLightColors);
            _buffer.SetGlobalVectorArray(_dirLightDirectionsId, _dirLightDirections);
            _buffer.SetGlobalVectorArray(_dirLightShadowDataId, _dirLightShadowData);
        }

        void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
        {
            _dirLightColors[index] = visibleLight.finalColor;
            _dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2); // negated forward vector
            _dirLightShadowData[index] = _shadows.ReserveDirectionalShadows(visibleLight.light, index);
        }

        public void Cleanup()
        {
            _shadows.Cleanup();
        }
    }
}
