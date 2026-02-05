using UnityEditor;
using UnityEditor.Build;

/// <summary>
/// Injects SECTR Audio Defines into project
/// </summary>
[InitializeOnLoad]
public class SECTR_AudioDefinesEditor : Editor
{
	static SECTR_AudioDefinesEditor()
	{
        // Make sure we inject SECTR_AUDIO_PRESENT
#if UNITY_2021_2_OR_NEWER
        string currBuildSettings = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
#else
        string currBuildSettings = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup);
#endif
		if (!currBuildSettings.Contains("SECTR_AUDIO_PRESENT"))
		{
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), currBuildSettings + ";SECTR_AUDIO_PRESENT");
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, currBuildSettings + ";SECTR_AUDIO_PRESENT");
#endif
		}
	}
}
