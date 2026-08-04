using System;
using UnityEditor;
using UnityEngine;

namespace Kronnect.Hub {

    static class SerializedParameterUtility {
        public static void SetParameter(SerializedObject component, string fieldName, object value) {
            SerializedProperty property = component.FindProperty(fieldName);
            if (property == null) {
                return;
            }
            SerializedProperty overrideProp = property.FindPropertyRelative("m_OverrideState");
            SerializedProperty valueProp = property.FindPropertyRelative("m_Value");
            if (overrideProp != null) {
                overrideProp.boolValue = true;
            }
            if (valueProp == null) return;
            SetSerializedValue(valueProp, value);
        }

        public static void SetScalar(SerializedObject so, string propertyName, int value) {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) {
                property.intValue = value;
            }
        }

        public static void SetFloat(SerializedObject so, string propertyName, float value) {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) {
                property.floatValue = value;
            }
        }

        public static void SetBool(SerializedObject so, string propertyName, bool value) {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) {
                property.boolValue = value;
            }
        }

        static void SetSerializedValue(SerializedProperty property, object value) {
            switch (value) {
                case bool boolValue:
                    property.boolValue = boolValue;
                    break;
                case int intValue:
                    property.intValue = intValue;
                    break;
                case float floatValue:
                    property.floatValue = floatValue;
                    break;
                case Enum enumValue:
                    property.intValue = Convert.ToInt32(enumValue);
                    break;
                case Vector2 vector2:
                    property.vector2Value = vector2;
                    break;
                case Vector3 vector3:
                    property.vector3Value = vector3;
                    break;
                case Vector4 vector4:
                    property.vector4Value = vector4;
                    break;
                case Color color:
                    property.colorValue = color;
                    break;
                default:
                    property.stringValue = value?.ToString() ?? string.Empty;
                    break;
            }
        }
    }

}

