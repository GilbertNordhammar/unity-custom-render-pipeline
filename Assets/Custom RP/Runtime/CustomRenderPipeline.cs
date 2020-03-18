using UnityEngine;
using UnityEngine.Rendering;


namespace CustomRP
{
    public class CustomRenderPipeline : RenderPipeline
    {
        CameraRenderer _renderer = new CameraRenderer();
        bool _useDynamicBatching, _useGPUInstancing;
        ShadowSettings _shadowSettings;

        public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,
                                    ShadowSettings shadowSettings)
        {
            _shadowSettings = shadowSettings;
            _useDynamicBatching = useDynamicBatching;
            _useGPUInstancing = useGPUInstancing;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach(var camera in cameras)
            {
                _renderer.Render(context, camera, _useDynamicBatching, _useGPUInstancing, _shadowSettings);
            }
        }
    }
}