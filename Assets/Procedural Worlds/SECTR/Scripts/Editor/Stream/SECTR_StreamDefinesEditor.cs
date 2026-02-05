using UnityEditor;
using UnityEditor.Build;

/// <summary>
/// Injects SECTR Audio Defines into project
/// </summary>
[InitializeOnLoad]
public class SECTR_StreamDefinesEditor : Editor
{
	static SECTR_StreamDefinesEditor()
	{
        // Make sure we inject SECTR_STREAM_PRESENT
#if UNITY_2021_2_OR_NEWER
        string currBuildSettings = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
#else
        string currBuildSettings = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup);
#endif
        if (!currBuildSettings.Contains("SECTR_STREAM_PRESENT"))
        {
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), currBuildSettings + ";SECTR_STREAM_PRESENT");
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, currBuildSettings + ";SECTR_STREAM_PRESENT");
#endif
        }
    }
}
