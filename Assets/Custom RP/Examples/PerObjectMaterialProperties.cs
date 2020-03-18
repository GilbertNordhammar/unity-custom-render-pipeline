using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    [SerializeField]
    Color _baseColor = Color.white;

    [SerializeField, Range(0f, 1f)]
    float _alphaCutoff = 0.5f, _metallic = 0f, _smoothness = 0.5f;

    [SerializeField, ColorUsage(false, true)]
    Color _emissionColor = Color.black;

    static int _baseColorId = Shader.PropertyToID("_BaseColor"),
               _alphaCutoffId = Shader.PropertyToID("_Cutoff"),
               _metallicId = Shader.PropertyToID("_Metallic"),
	           _smoothnessId = Shader.PropertyToID("_Smoothness"),
               _emissionColorId = Shader.PropertyToID("_EmissionColor");

    static MaterialPropertyBlock _block;

    private void Awake()
    {
        OnValidate();
    }

    void OnValidate()
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
        }
        _block.SetColor(_baseColorId, _baseColor);
        _block.SetFloat(_alphaCutoffId, _alphaCutoff);
        _block.SetFloat(_metallicId, _metallic);
        _block.SetFloat(_smoothnessId, _smoothness);
        _block.SetColor(_emissionColorId, _emissionColor);
        GetComponent<Renderer>().SetPropertyBlock(_block);
    }
}
