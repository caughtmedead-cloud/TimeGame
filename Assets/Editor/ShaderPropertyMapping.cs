using UnityEngine;

[CreateAssetMenu(
    fileName = "ShaderPropertyMapping",
    menuName = "ProPixelizer/Shader Property Mapping"
)]
public class ShaderPropertyMapping : ScriptableObject
{
    [Header("Source Shader")]
    public Shader sourceShader;

    [Header("Source Properties")]
    public string albedoProperty = "_MainTex";
    public string normalProperty = "_BumpMap";
    public string emissionProperty = "_EmissionMap";

    [Header("Options")]
    public bool supportsEmission = true;
}
