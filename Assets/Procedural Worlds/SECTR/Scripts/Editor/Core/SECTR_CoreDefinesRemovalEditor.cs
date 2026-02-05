using UnityEditor;
using UnityEditor.Build;

// Automates removal of SECTR Audio defines
public class SECTR_CoreDefinesRemovalEditor : UnityEditor.AssetModificationProcessor
{
	public static AssetDeleteResult OnWillDeleteAsset(string AssetPath, RemoveAssetOptions rao)
	{
		// Assuming that if this file is being removed, than the whole module is being removed
		if (AssetPath.EndsWith("SECTR") || AssetPath.EndsWith("SECTR/Scripts/Core"))
		{
#if UNITY_2021_2_OR_NEWER
            string symbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
#else
        string symbols = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup);
#endif
            if (symbols.Contains("SECTR_CORE_PRESENT"))
			{
				symbols = symbols.Replace("SECTR_CORE_PRESENT;", "");
				symbols = symbols.Replace("SECTR_CORE_PRESENT", "");
#if UNITY_2021_2_OR_NEWER
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), symbols);
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
#endif
            }
        }
		return AssetDeleteResult.DidNotDelete;
	}
}
