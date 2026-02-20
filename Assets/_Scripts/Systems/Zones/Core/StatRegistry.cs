using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class StatRegistry
{
    private static List<string> cachedStatIdentifiers = null;
    
    public static List<string> GetAvailableStatIdentifiers()
    {
        if (cachedStatIdentifiers != null)
            return cachedStatIdentifiers;
        
        cachedStatIdentifiers = new List<string>();
        
#if UNITY_EDITOR
        var statTypes = TypeCache.GetTypesDerivedFrom<IPlayerStat>();
        
        foreach (Type type in statTypes)
        {
            if (type.IsAbstract || type.IsInterface)
                continue;
            
            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                continue;
            
            GameObject tempObj = new GameObject("TempStatDiscovery");
            tempObj.hideFlags = HideFlags.HideAndDontSave;
            
            try
            {
                MonoBehaviour component = tempObj.AddComponent(type) as MonoBehaviour;
                
                if (component is IPlayerStat stat && !string.IsNullOrEmpty(stat.StatIdentifier))
                {
                    if (!cachedStatIdentifiers.Contains(stat.StatIdentifier))
                    {
                        cachedStatIdentifiers.Add(stat.StatIdentifier);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempObj);
            }
        }
#endif
        
        if (cachedStatIdentifiers.Count == 0)
        {
            cachedStatIdentifiers.Add("Stability");
            cachedStatIdentifiers.Add("Radiation");
            cachedStatIdentifiers.Add("Health");
        }
        
        cachedStatIdentifiers.Sort();
        return cachedStatIdentifiers;
    }
    
    public static void RefreshCache()
    {
        cachedStatIdentifiers = null;
    }
    
    public static bool IsValidStatIdentifier(string identifier)
    {
        return GetAvailableStatIdentifiers().Contains(identifier);
    }
}
