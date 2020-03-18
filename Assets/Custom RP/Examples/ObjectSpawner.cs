using UnityEngine;
using UnityEngine.Rendering;

public class ObjectSpawner : MonoBehaviour
{
    [SerializeField]
    Mesh _mesh = default;

    [SerializeField]
    Material _material = default;

    [SerializeField]
    LightProbeProxyVolume _lightProbeVolume = null;

    static int _baseColorId = Shader.PropertyToID("_BaseColor"),
               _metallicId = Shader.PropertyToID("_Metallic"),
               _smoothnessId = Shader.PropertyToID("_Smoothness");

    Matrix4x4[] _matrices = new Matrix4x4[1023];
    Vector4[] _baseColors = new Vector4[1023];
    float[] _metallic = new float[1023], _smoothness = new float[1023];

    MaterialPropertyBlock _block;

    void Awake()
    {
        for (int i = 0; i < _matrices.Length; i++)
        {
            _matrices[i] = Matrix4x4.TRS(
                pos: gameObject.transform.position + Random.insideUnitSphere * 10f,
                q: Quaternion.Euler(
                    Random.value * 360f, Random.value * 360f, Random.value * 360f
                ),
                s: Vector3.one * Random.Range(0.5f, 1.5f)
            );
            _baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1f));
            _metallic[i] = Random.value < 0.25f ? 1f : 0f;
            _smoothness[i] = Random.Range(0.05f, 0.95f);
        }
    }

    void Update()
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
            _block.SetVectorArray(_baseColorId, _baseColors);
            _block.SetFloatArray(_metallicId, _metallic);
            _block.SetFloatArray(_smoothnessId, _smoothness);


            if (!_lightProbeVolume)
            {
                var positions = new Vector3[1023];
                for (int i = 0; i < _matrices.Length; i++)
                {
                    positions[i] = _matrices[i].GetColumn(3);
                }

                var lightProbes = new SphericalHarmonicsL2[1023];
                var occlusionProbes = new Vector4[1023];
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
                    positions, lightProbes, occlusionProbes
                );
                _block.CopySHCoefficientArraysFrom(lightProbes);
                _block.CopyProbeOcclusionArrayFrom(occlusionProbes);
            }
        }

        Graphics.DrawMeshInstanced(
            _mesh, 
            submeshIndex: 0, 
            _material, 
            _matrices, 
            count: 1023, 
            _block,
            ShadowCastingMode.On, 
            receiveShadows: true, 
            layer: 0, 
            camera: null,
            _lightProbeVolume ? LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided,
            _lightProbeVolume
        );
    }
}
