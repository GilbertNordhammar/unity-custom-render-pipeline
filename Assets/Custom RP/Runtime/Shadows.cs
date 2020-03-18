using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP
{
    public class Shadows
    {
        const string _bufferName = "Shadows";
        CommandBuffer _buffer = new CommandBuffer
        {
            name = _bufferName
        };

        ScriptableRenderContext _context;
        CullingResults _cullingResults;
        ShadowSettings _settings;
        const int _maxShadowedDirectionalLightCount = 4, _maxCascades = 4;
        bool _useShadowMask;

        int _shadowedDirectionalLightCount;
        ShadowedDirectionalLight[] _shadowedDirectionalLights =
            new ShadowedDirectionalLight[_maxShadowedDirectionalLightCount];
        
        static int _dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
                   _dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
                   _cascadeCountId = Shader.PropertyToID("_CascadeCount"),
                   _cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
                   _cascadeDataId = Shader.PropertyToID("_CascadeData"),
                   _shadowAtlastSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
                   _shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

        static Matrix4x4[] _dirShadowMatrices = new Matrix4x4[_maxShadowedDirectionalLightCount * 
                                                              _maxCascades];
        static Vector4[] _cascadeCullingSpheres = new Vector4[_maxCascades],
                         _cascadeData = new Vector4[_maxCascades];

        static string[] _directionalFilterKeywords = {
            "_DIRECTIONAL_PCF3",
            "_DIRECTIONAL_PCF5",
            "_DIRECTIONAL_PCF7",
        };

        static string[] _cascadeBlendKeywords = {
            "_CASCADE_BLEND_SOFT",
            "_CASCADE_BLEND_DITHER"
        };

        static string[] _shadowMaskKeywords = {
            "_SHADOW_MASK_ALWAYS",
            "_SHADOW_MASK_DISTANCE"
        };

        struct ShadowedDirectionalLight
        {
            public int visibleLightIndex;
            public float slopeScaleBias;
            public float nearPlaneOffset;
        }
        
        public void Setup(
            ScriptableRenderContext context, CullingResults cullingResults,
            ShadowSettings settings
        )
        {
            _context = context;
            _cullingResults = cullingResults;
            _settings = settings;
            _shadowedDirectionalLightCount = 0;
            _useShadowMask = false;
        }

        public void Render()
        {
            if (_shadowedDirectionalLightCount > 0)
            {
                RenderDirectionalShadows();
            }
            _buffer.BeginSample(_bufferName);
            SetKeywords(_shadowMaskKeywords, 
                _useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask 
                ? 0 : 1 : -1);
            _buffer.EndSample(_bufferName);
            ExecuteBuffer();
        }

        void RenderDirectionalShadows()
        {
            int atlasSize = (int)_settings.directional.atlasSize;
            _buffer.GetTemporaryRT(_dirShadowAtlasId, width: atlasSize, height: atlasSize, 
                                   depthBuffer: 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            _buffer.SetRenderTarget(_dirShadowAtlasId,
                                    RenderBufferLoadAction.DontCare,
                                    RenderBufferStoreAction.Store);
            _buffer.ClearRenderTarget(true, false, Color.clear);
            _buffer.BeginSample(_bufferName);
            ExecuteBuffer();

            int tiles = _shadowedDirectionalLightCount * _settings.directional.cascadeCount;
            int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
            int tileSize = atlasSize / split;
            for (int i = 0; i < _shadowedDirectionalLightCount; i++)
            {
                RenderDirectionalShadows(i, split, tileSize);
            }

            _buffer.SetGlobalInt(_cascadeCountId, _settings.directional.cascadeCount);
            _buffer.SetGlobalVectorArray(
                _cascadeCullingSpheresId, _cascadeCullingSpheres
            );
            _buffer.SetGlobalVectorArray(_cascadeDataId, _cascadeData);
            _buffer.SetGlobalMatrixArray(_dirShadowMatricesId, _dirShadowMatrices);

            float f = 1f - _settings.directional.cascadeFade;
            _buffer.SetGlobalVector(
                _shadowDistanceFadeId,
                new Vector4(1f / _settings.maxDistance, 
                            1f / _settings.distanceFade, 
                            1f / (1f - f * f))
            );

            SetKeywords(
                _directionalFilterKeywords, (int)_settings.directional.filter - 1
            );
            SetKeywords(
                _cascadeBlendKeywords, (int)_settings.directional.cascadeBlend - 1
            );
            _buffer.SetGlobalVector(
                _shadowAtlastSizeId, new Vector4(atlasSize, 1f / atlasSize)
            );

            _buffer.EndSample(_bufferName);
            ExecuteBuffer();
        }

        void SetKeywords (string[] keywords, int enabledIndex)
        { 
            for (int i = 0; i < keywords.Length; i++)
            {
                if (i == enabledIndex)
                {
                    _buffer.EnableShaderKeyword(keywords[i]);
                }
                else
                {
                    _buffer.DisableShaderKeyword(keywords[i]);
                }
            }
        }

        void RenderDirectionalShadows(int index, int split, int tileSize)
        {
            ShadowedDirectionalLight light = _shadowedDirectionalLights[index];
            var shadowSettings = new ShadowDrawingSettings(_cullingResults, light.visibleLightIndex);
            int splitCount = _settings.directional.cascadeCount;
            Vector3 splitRatios = _settings.directional.CascadeRatios;
            float cullingFactor = Mathf.Max(0f, 0.8f - _settings.directional.cascadeFade);

            for (int i = 0; i < splitCount; i++)
            {
                _cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                    light.visibleLightIndex,
                    splitIndex: i,
                    splitCount,
                    splitRatios,
                    shadowResolution: tileSize,
                    light.nearPlaneOffset,
                    out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, 
                    out ShadowSplitData splitData
                );

                splitData.shadowCascadeBlendCullingFactor = cullingFactor;
                _settings.SplitData = splitData;
                if(index == 0)
                {
                    SetCascadeData(i, splitData.cullingSphere, tileSize);
                }

                int tileIndex = index * splitCount + i;
                var tileOffset = GetTileViewportOffset(tileIndex, split);
                SetTileViewport(tileOffset, tileSize);
                _dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(worldToLight: projectionMatrix * viewMatrix,
                                                                     tileOffset, split);
                _buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

                _buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
                ExecuteBuffer();
                _context.DrawShadows(ref shadowSettings);
                _buffer.SetGlobalDepthBias(0f, 0f);
            }
        }

        void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
        {
            float texelSize = 2f * cullingSphere.w / tileSize;
            float filterSize = texelSize * ((float)_settings.directional.filter + 1f);

            _cascadeData[index] = new Vector4(
                1f / (cullingSphere.w * cullingSphere.w), // Det är 1f / cullingSphere.w i tutorialen, men jag tror det är en typo?
                filterSize * 1.4142136f //offseted along diagonal
            );
            cullingSphere.w -= filterSize;
            cullingSphere.w *= cullingSphere.w;
            _cascadeCullingSpheres[index] = cullingSphere;
        }

        Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 worldToLight, Vector2 offset, int split)
        {
            Matrix4x4 m = worldToLight;
            if (SystemInfo.usesReversedZBuffer)
            {
                m.m20 = -m.m20;
                m.m21 = -m.m21;
                m.m22 = -m.m22;
                m.m23 = -m.m23;
            }

            float scale = 1f / split;
            m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
            m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
            m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
            m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
            m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
            m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
            m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
            m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
            m.m20 = 0.5f * (m.m20 + m.m30);
            m.m21 = 0.5f * (m.m21 + m.m31);
            m.m22 = 0.5f * (m.m22 + m.m32);
            m.m23 = 0.5f * (m.m23 + m.m33);
            return m;
        }

        void SetTileViewport(Vector2 tileOffset, float tileSize)
        {
            _buffer.SetViewport(
                new Rect(tileOffset.x * tileSize, tileOffset.y * tileSize, tileSize, tileSize)
            );
        }

        Vector2 GetTileViewportOffset(int index, int split)
        {
            return new Vector2(index % split, index / split);
        }

        public void Cleanup()
        {
            if (_shadowedDirectionalLightCount > 0)
            {
                _buffer.ReleaseTemporaryRT(_dirShadowAtlasId);
                ExecuteBuffer();
            }
        }

        void ExecuteBuffer()
        {
            _context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }

        public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
        {
            if (_shadowedDirectionalLightCount < _maxShadowedDirectionalLightCount &&
                light.shadows != LightShadows.None && 
                light.shadowStrength > 0f
                )
            {
                float maskChannel = -1;
                LightBakingOutput lightBaking = light.bakingOutput;
                if (
                    lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                    lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
                )
                {
                    _useShadowMask = true;
                    maskChannel = lightBaking.occlusionMaskChannel;
                }

                if (!_cullingResults.GetShadowCasterBounds(
                    visibleLightIndex, out Bounds b
                ))
                {
                    return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
                }

                _shadowedDirectionalLights[_shadowedDirectionalLightCount] =
                    new ShadowedDirectionalLight
                    {
                        visibleLightIndex = visibleLightIndex,
                        slopeScaleBias = light.shadowBias,
                        nearPlaneOffset = light.shadowNearPlane
                    };


                return new Vector4(
                    light.shadowStrength,
                    _settings.directional.cascadeCount * _shadowedDirectionalLightCount++,
                    light.shadowNormalBias,
                    maskChannel
                );
            }

            return new Vector4(0f, 0f, 0f, -1f);
        }
    }
}