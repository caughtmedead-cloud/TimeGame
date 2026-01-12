using UnityEngine;
using UnityEditor;

public class ProPixelizerVariantTextureAssigner : EditorWindow
{
    [Header("References")]
    [SerializeField] private Material pixelMaster;
    [SerializeField] private ShaderPropertyMapping mapping;

    // Pro Pixelizer destination property names
    private const string PP_ALBEDO = "_BaseMap";
    private const string PP_NORMAL = "_BumpMap";
    private const string PP_EMISSION = "_EmissionMap";

    [MenuItem("Tools/ProPixelizer/Assign Textures To Variant")]
    public static void Open()
    {
        GetWindow<ProPixelizerVariantTextureAssigner>(
            "Pixelizer Variant Assigner"
        );
    }

    private void OnGUI()
    {
        GUILayout.Label(
            "ProPixelizer Variant Texture Assigner",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Select SOURCE material first, then VARIANT material.\n" +
            "The variant must already inherit from Pixel_Master.",
            MessageType.Info
        );

        pixelMaster = (Material)EditorGUILayout.ObjectField(
            "Pixel Master Material",
            pixelMaster,
            typeof(Material),
            false
        );

        mapping = (ShaderPropertyMapping)EditorGUILayout.ObjectField(
            "Shader Mapping",
            mapping,
            typeof(ShaderPropertyMapping),
            false
        );

        GUILayout.Space(10);

        EditorGUI.BeginDisabledGroup(pixelMaster == null || mapping == null);

        if (GUILayout.Button("Assign Textures"))
        {
            AssignFromSelection();
        }

        EditorGUI.EndDisabledGroup();
    }

    private void AssignFromSelection()
    {
        Object[] selection = Selection.objects;

        if (selection.Length != 2 ||
            !(selection[0] is Material) ||
            !(selection[1] is Material))
        {
            EditorUtility.DisplayDialog(
                "Selection Error",
                "Select SOURCE material first, then VARIANT material.",
                "OK"
            );
            return;
        }

        Material source = selection[0] as Material;
        Material variant = selection[1] as Material;

        // Optional safety checks
        if (mapping.sourceShader &&
            source.shader != mapping.sourceShader)
        {
            Debug.LogWarning(
                $"Source material '{source.name}' does not use expected shader."
            );
        }

        if (pixelMaster &&
            variant.shader != pixelMaster.shader)
        {
            Debug.LogWarning(
                $"Variant '{variant.name}' does not use ProPixelizer shader."
            );
        }

        AssignTextures(source, variant);

        EditorUtility.SetDirty(variant);
        AssetDatabase.SaveAssets();
    }

    private void AssignTextures(Material source, Material variant)
    {
        CopyTexture(
            source,
            variant,
            mapping.albedoProperty,
            PP_ALBEDO
        );

        CopyTexture(
            source,
            variant,
            mapping.normalProperty,
            PP_NORMAL
        );

        if (mapping.supportsEmission)
        {
            CopyTexture(
                source,
                variant,
                mapping.emissionProperty,
                PP_EMISSION
            );

            if (variant.HasProperty(PP_EMISSION) &&
                variant.GetTexture(PP_EMISSION) != null)
            {
                EnableEmission(variant);
            }
            else
            {
                DisableEmission(variant);
            }
        }
    }

    private void CopyTexture(
        Material source,
        Material target,
        string sourceProp,
        string targetProp
    )
    {
        if (!source.HasProperty(sourceProp))
            return;

        if (!target.HasProperty(targetProp))
            return;

        Texture tex = source.GetTexture(sourceProp);
        if (!tex)
            return;

        target.SetTexture(targetProp, tex);
        target.SetTextureScale(
            targetProp,
            source.GetTextureScale(sourceProp)
        );
        target.SetTextureOffset(
            targetProp,
            source.GetTextureOffset(sourceProp)
        );
    }

    private void EnableEmission(Material mat)
    {
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags =
            MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    private void DisableEmission(Material mat)
    {
        mat.DisableKeyword("_EMISSION");
        mat.globalIlluminationFlags =
            MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    }
}
