using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP
{
    public class CustomShaderGUI : ShaderGUI
    {
        MaterialEditor _editor;
        Object[] _materials;
        MaterialProperty[] _properties;

        bool _showPresets;

        public override void OnGUI(
            MaterialEditor materialEditor, MaterialProperty[] properties
        )
        {
            EditorGUI.BeginChangeCheck();

            base.OnGUI(materialEditor, properties);
            _editor = materialEditor;
            _materials = materialEditor.targets;
            _properties = properties;

            BakedEmission();

            EditorGUILayout.Space();
            _showPresets = EditorGUILayout.Foldout(_showPresets, "Presets", true);
            if (_showPresets)
            {
                OpaquePreset();
                ClipPreset();
                FadePreset();
                TransparentPreset();
            }

            if (EditorGUI.EndChangeCheck())
            {
                SetShadowCasterPass();
                CopyLightMappingProperties();
            }
        }

        void CopyLightMappingProperties()
        {
            MaterialProperty mainTex = FindProperty("_MainTex", _properties, false);
            MaterialProperty baseMap = FindProperty("_BaseMap", _properties, false);
            if (mainTex != null && baseMap != null)
            {
                mainTex.textureValue = baseMap.textureValue;
                mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
            }
            MaterialProperty color = FindProperty("_Color", _properties, false);
            MaterialProperty baseColor =
                FindProperty("_BaseColor", _properties, false);
            if (color != null && baseColor != null)
            {
                color.colorValue = baseColor.colorValue;
            }
        }

        void BakedEmission()
        {
            EditorGUI.BeginChangeCheck();
            _editor.LightmapEmissionProperty();
            if (EditorGUI.EndChangeCheck())
            {
                foreach (Material m in _editor.targets)
                {
                    m.globalIlluminationFlags &=
                        ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
            }
        }

        bool SetProperty(string name, float value)
        {
            MaterialProperty property = FindProperty(name, _properties, false);
            if (property != null)
            {
                property.floatValue = value;
                return true;
            }
            return false;
        }

        void SetProperty(string name, string keyword, bool value)
        {
            if (SetProperty(name, value ? 1f : 0f))
            {
                SetKeyword(keyword, value);
            }
        }

        void SetKeyword(string keyword, bool enabled)
        {
            if (enabled)
            {
                foreach (Material m in _materials)
                {
                    m.EnableKeyword(keyword);
                }
            }
            else
            {
                foreach (Material m in _materials)
                {
                    m.DisableKeyword(keyword);
                }
            }
        }

        void SetShadowCasterPass()
        {
            MaterialProperty shadows = FindProperty("_Shadows", _properties, false);
            if (shadows == null || shadows.hasMixedValue)
            {
                return;
            }
            bool enabled = shadows.floatValue < (float)ShadowMode.Off;
            foreach (Material m in _materials)
            {
                m.SetShaderPassEnabled("ShadowCaster", enabled);
            }
        }

        void OpaquePreset()
        {
            if (PresetButton("Opaque"))
            {
                Clipping = false;
                PremultiplyAlpha = false;
                SrcBlend = BlendMode.One;
                DstBlend = BlendMode.Zero;
                ZWrite = true;
                RenderQueue = RenderQueue.Geometry;
                Shadows = ShadowMode.On;
            }
        }

        void ClipPreset()
        {
            if (PresetButton("Clip"))
            {
                Clipping = true;
                PremultiplyAlpha = false;
                SrcBlend = BlendMode.One;
                DstBlend = BlendMode.Zero;
                ZWrite = true;
                RenderQueue = RenderQueue.AlphaTest;
                Shadows = ShadowMode.Clip;
            }
        }

        void FadePreset()
        {
            if (PresetButton("Fade"))
            {
                Clipping = false;
                PremultiplyAlpha = false;
                SrcBlend = BlendMode.SrcAlpha;
                DstBlend = BlendMode.OneMinusSrcAlpha;
                ZWrite = false;
                RenderQueue = RenderQueue.Transparent;
                Shadows = ShadowMode.Dither; 
            }
        }

        void TransparentPreset()
        {
            if (HasPremultiplyAlpha && PresetButton("Transparent"))
            {
                Clipping = false;
                PremultiplyAlpha = true;
                SrcBlend = BlendMode.One;
                DstBlend = BlendMode.OneMinusSrcAlpha;
                ZWrite = false;
                RenderQueue = RenderQueue.Transparent;
                Shadows = ShadowMode.Dither;
            }
        }

        bool PresetButton(string name)
        {
            if (GUILayout.Button(name))
            {
                _editor.RegisterPropertyChangeUndo(name);
                return true;
            }
            return false;
        }

        bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");

        bool HasProperty(string name) => FindProperty(name, _properties, false) != null;

        bool Clipping
        {
            set => SetProperty("_Clipping", "_CLIPPING", value);
        }

        bool PremultiplyAlpha
        {
            set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
        }

        BlendMode SrcBlend
        {
            set => SetProperty("_SrcBlend", (float)value);
        }

        BlendMode DstBlend
        {
            set => SetProperty("_DstBlend", (float)value);
        }

        bool ZWrite
        {
            set => SetProperty("_ZWrite", value ? 1f : 0f);
        }

        RenderQueue RenderQueue
        {
            set
            {
                foreach (Material m in _materials)
                {
                    m.renderQueue = (int)value;
                }
            }
        }

        ShadowMode Shadows
        {
            set
            {
                if (SetProperty("_Shadows", (float)value))
                {
                    SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
                    SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
                }
            }
        }

        enum ShadowMode
        {
            On, Clip, Dither, Off
        }

    }
}
