using System;
using System.Globalization;
using RiseOn.Utils.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Firebase.RemoteConfig.Editor {
    /// <summary>A parameter on two lines: its key, type, default and Live, then its description and group; its id is for enum fields only.</summary>
    [CustomPropertyDrawer(typeof(RemoteParameter))]
    internal sealed class RemoteParameterDrawer : PropertyDrawer {
        // Puts @ before a C# keyword, as the pack's source generator does.
        private static readonly Microsoft.CSharp.CSharpCodeProvider csharp = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect rect, SerializedProperty parameter, GUIContent label) {
            var line    = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;

            var key         = parameter.FindPropertyRelative("key");
            var type        = parameter.FindPropertyRelative("type");
            var value       = parameter.FindPropertyRelative("defaultValue");
            var description = parameter.FindPropertyRelative("description");
            var group       = parameter.FindPropertyRelative("group");
            var live        = parameter.FindPropertyRelative("live");

            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 70;

            var top    = new Rect(rect.x, rect.y, rect.width, line);
            var bottom = new Rect(rect.x, top.yMax + spacing, rect.width, line);

            var keyRect   = new Rect(top.x, top.y, top.width * 0.4f - 4, line);
            var typeRect  = new Rect(keyRect.xMax + 4, top.y, 70, line);
            var liveRect  = new Rect(top.xMax - 50, top.y, 50, line);
            var valueRect = new Rect(typeRect.xMax + 4, top.y, liveRect.x - typeRect.xMax - 8, line);

            EditorGUI.BeginChangeCheck();
            var newKey = EditorGUI.DelayedTextField(keyRect, new GUIContent("Key", $"RemoteKey.{csharp.CreateEscapedIdentifier(key.stringValue)} in code"), key.stringValue);
            if (EditorGUI.EndChangeCheck()) key.stringValue = newKey.Trim();

            // The pick comes after this draw, so it reads and writes the object on its own.
            var serialized = type.serializedObject;
            var typePath   = type.propertyPath;
            var valuePath  = value.propertyPath;
            ChoiceFieldUtils.Enum(typeRect, (RemoteValueType)type.enumValueIndex, picked => {
                var newType = (RemoteValueType)picked;
                serialized.Update();
                serialized.FindProperty(typePath).enumValueIndex = (int)newType;
                var newValue = serialized.FindProperty(valuePath);
                if (RemoteParameterRules.CheckValue(newType, newValue.stringValue) != null) newValue.stringValue = RemoteParameterRules.ZeroValue(newType);
                serialized.ApplyModifiedProperties();
            });

            EditorGUIUtility.labelWidth = 50;
            DrawDefault(valueRect, (RemoteValueType)type.enumValueIndex, value);
            EditorGUIUtility.labelWidth = 30;
            live.boolValue = EditorGUI.ToggleLeft(liveRect, new GUIContent("Live", "Takes values published while the game runs"), live.boolValue);

            EditorGUIUtility.labelWidth = 70;
            var descriptionRect = new Rect(bottom.x, bottom.y, bottom.width * 0.7f - 4, line);
            var groupRect       = new Rect(descriptionRect.xMax + 4, bottom.y, bottom.xMax - descriptionRect.xMax - 4, line);
            description.stringValue = EditorGUI.DelayedTextField(descriptionRect, "Description", description.stringValue);
            EditorGUIUtility.labelWidth = 40;
            group.stringValue = EditorGUI.DelayedTextField(groupRect, "Group", group.stringValue);

            EditorGUIUtility.labelWidth = labelWidth;
        }

        private static void DrawDefault(Rect rect, RemoteValueType type, SerializedProperty value) {
            var label = new GUIContent("Default", "The in-app default");

            // Writes only what the user changed, so drawing never rewrites a value pulled from the console.
            EditorGUI.BeginChangeCheck();
            string text;
            switch (type) {
                case RemoteValueType.Bool:
                    RemoteValue.ParseBool(value.stringValue, out var flag);
                    text = EditorGUI.Toggle(rect, label, flag) ? "true" : "false";
                    break;
                case RemoteValueType.Int:
                    RemoteValue.ParseInt(value.stringValue, out var number);
                    text = EditorGUI.DelayedIntField(rect, label, number).ToString(CultureInfo.InvariantCulture);
                    break;
                case RemoteValueType.Float:
                    RemoteValue.ParseFloat(value.stringValue, out var real);
                    text = EditorGUI.DelayedFloatField(rect, label, real).ToString("R", CultureInfo.InvariantCulture);
                    break;
                case RemoteValueType.String:
                case RemoteValueType.Json:
                    text = EditorGUI.DelayedTextField(rect, label, value.stringValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            if (EditorGUI.EndChangeCheck()) value.stringValue = text;
        }
    }
}