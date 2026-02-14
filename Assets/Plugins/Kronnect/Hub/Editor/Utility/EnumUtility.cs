using System;

namespace Kronnect.Hub {

    static class EnumUtility {
        public static object Parse(Type enumType, string name) {
            try {
                return Enum.Parse(enumType, name);
            } catch {
                return null;
            }
        }
    }

}

