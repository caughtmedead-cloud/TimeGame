using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Kronnect.Hub {

    static class TypeUtility {
        static readonly Dictionary<string, Type> Cache = new Dictionary<string, Type>(StringComparer.Ordinal);

        public static bool TryGetType(string fullName, out Type type) {
            type = null;
            if (string.IsNullOrEmpty(fullName)) {
                return false;
            }
            if (Cache.TryGetValue(fullName, out type)) {
                return type != null;
            }
            type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => GetTypeSafe(assembly, fullName))
                .FirstOrDefault(t => t != null);
            Cache[fullName] = type;
            return type != null;
        }

        static Type GetTypeSafe(Assembly assembly, string fullName) {
            try {
                return assembly.GetType(fullName);
            } catch (ReflectionTypeLoadException ex) {
                return ex.Types?.FirstOrDefault(t => t != null && t.FullName == fullName);
            }
        }
    }

}

