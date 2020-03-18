using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP
{
    [CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
    public class CustomRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField]
        bool _useDynamicBatching = true, _useGPUInstancing = true, _useSRPBatcher = true;

        [SerializeField]
        ShadowSettings _shadows = default;

        protected override RenderPipeline CreatePipeline()
        {
            return new CustomRenderPipeline(_useDynamicBatching, _useGPUInstancing, _useSRPBatcher, _shadows);
        }
    }
}