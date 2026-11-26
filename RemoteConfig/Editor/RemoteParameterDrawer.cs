using System;
using System.Globalization;
using RiseOn.Utils.Editor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Firebase.RemoteConfig.Editor {
    /// <summary>
    /// A parameter of the Parameters list as a header line, the key in the label column and the type in the value column, whose arrow opens the rest
    /// under it: the in-app default, Live, the description and the group, each a row whose value keeps to the type's column.<br/>
    /// Written through the value entries, with undo; its id is for enum fields only.
    /// </summary>
    internal sealed class RemoteParameterDrawer : OdinValueDrawer<RemoteParameter> {
        // Puts @ before a C# keyword, as the pack's source generator does.
        private static readonly Microsoft.CSharp.CSharpCodeProvider csharp = new();

        // The foldout's arrow, as the IAP page's products leave for it.
        private const float Arrow = 14;

        private static readonly GUIContent defaultLabel     = new("Default", "The in-app default, used until the first fetch activates, when a fetch fails, and for keys the console does not have.");
        private static readonly GUIContent liveLabel        = new("Live", "Takes a value published while the game runs; otherwise the value stays the one of launch until the next launch.");
        private static readonly GUIContent descriptionLabel = new("Description", "Shown on the key in code, and in the Firebase console.");
        private static readonly GUIContent groupLabel       = new("Group", "The parameter group in the Firebase console; empty for none.");

        protected override void DrawPropertyLayout(GUIContent label) {
            var key   = Property.Children["key"];
            var type  = Property.Children["type"];
            var value = Property.Children["defaultValue"];

            // The key as the IAP page's product id: as wide as its text, at least as wide as most keys need so that the list's keys line up, and never closer
            // than the inset to the value column. Placed by the item rather than the layout, which moves it by a margin once the item opens.
            const float least = 120;
            var line      = SectionRows.MarkFirstRow(EditorGUILayout.GetControlRect());
            var valueRect = Rect.MinMaxRect(line.x + EditorGUIUtility.labelWidth + Section.LabelGap, line.y, line.xMax, line.yMax);
            var text      = (key.ValueEntry.WeakSmartValue as string ?? "").Trim();
            var arrowRect = new Rect(line.x, line.y, Arrow, line.height);
            var keyWidth  = Mathf.Max(0, Mathf.Min(Mathf.Max(EditorStyles.textField.CalcSize(new GUIContent(text)).x + Section.Inset, least), valueRect.x - Section.Inset - arrowRect.xMax));
            var keyRect   = new Rect(arrowRect.xMax, line.y, keyWidth, line.height);

            var open = Property.State.Expanded;
            if (EditorGUI.Foldout(arrowRect, open, GUIContent.none, true) != open) Property.State.Expanded = !open;

            // The label column's place past the key folds too, as a title does.
            var rest = Rect.MinMaxRect(keyRect.xMax, line.y, valueRect.x, line.yMax);
            var e    = Event.current;
            if (e.type is EventType.MouseDown && e.button == 0 && rest.Contains(e.mousePosition)) {
                Property.State.Expanded = !open;
                e.Use();
            }

            // Delayed: the list is read once a typed key is done.
            EditorGUI.BeginChangeCheck();
            var newKey = EditorGUI.DelayedTextField(keyRect, text);
            if (EditorGUI.EndChangeCheck()) key.ValueEntry.WeakSmartValue = newKey.Trim();
            GUI.Label(keyRect, new GUIContent("", $"The key in the Firebase console: a letter or underscore, then letters, digits or underscores; RemoteKey.{csharp.CreateEscapedIdentifier(text)} in code"));

            // A default the new type cannot read goes back to that type's zero.
            ChoiceFieldUtils.Enum(valueRect, (RemoteValueType) type.ValueEntry.WeakSmartValue, picked => Property.Tree.DelayActionUntilRepaint(() => {
                var newType = (RemoteValueType) picked;
                type.ValueEntry.WeakSmartValue = newType;
                if (RemoteParameterRules.CheckValue(newType, value.ValueEntry.WeakSmartValue as string ?? "") != null) value.ValueEntry.WeakSmartValue = RemoteParameterRules.ZeroValue(newType);
            }));

            // Odin's fade, as the page's sections open. In by the arrow alone: the drag handle already moves the whole row in.
            if (SirenixEditorGUI.BeginFadeGroup(Property, open)) {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Arrow);
                GUILayout.BeginVertical();
                GUIHelper.PushLabelWidth(EditorGUIUtility.labelWidth - Arrow);
                Default(value, (RemoteValueType) type.ValueEntry.WeakSmartValue);
                Live(Property.Children["live"]);
                Text(Property.Children["description"], descriptionLabel);
                Text(Property.Children["group"], groupLabel);
                GUIHelper.PopLabelWidth();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            SirenixEditorGUI.EndFadeGroup();
        }

        // Written only when changed here, so drawing never rewrites a value pulled from the console.
        private static void Default(InspectorProperty value, RemoteValueType type) {
            var rect    = Line(defaultLabel);
            var current = value.ValueEntry.WeakSmartValue as string ?? "";
            EditorGUI.BeginChangeCheck();
            string text;
            switch (type) {
                case RemoteValueType.Bool:
                    RemoteValue.ParseBool(current, out var flag);
                    text = EditorGUI.Toggle(rect, flag) ? "true" : "false";
                    break;
                case RemoteValueType.Int:
                    RemoteValue.ParseInt(current, out var number);
                    text = EditorGUI.DelayedIntField(rect, number).ToString(CultureInfo.InvariantCulture);
                    break;
                case RemoteValueType.Float:
                    RemoteValue.ParseFloat(current, out var real);
                    text = EditorGUI.DelayedFloatField(rect, real).ToString("R", CultureInfo.InvariantCulture);
                    break;
                case RemoteValueType.String:
                case RemoteValueType.Json:
                    text = EditorGUI.DelayedTextField(rect, current);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            if (EditorGUI.EndChangeCheck()) value.ValueEntry.WeakSmartValue = text;
        }

        private static void Live(InspectorProperty live) {
            var rect = Line(liveLabel);
            EditorGUI.BeginChangeCheck();
            var on = EditorGUI.Toggle(rect, live.ValueEntry.WeakSmartValue is true);
            if (EditorGUI.EndChangeCheck()) live.ValueEntry.WeakSmartValue = on;
        }

        // Delayed, as the key.
        private static void Text(InspectorProperty field, GUIContent label) {
            var rect = Line(label);
            EditorGUI.BeginChangeCheck();
            var text = EditorGUI.DelayedTextField(rect, field.ValueEntry.WeakSmartValue as string ?? "");
            if (EditorGUI.EndChangeCheck()) field.ValueEntry.WeakSmartValue = text;
        }

        // A row: its label, and its value where a row's own label would end, in line with the page's values.
        private static Rect Line(GUIContent label) {
            var line   = EditorGUILayout.GetControlRect();
            var valueX = line.x + EditorGUIUtility.labelWidth + Section.LabelGap;
            GUI.Label(Rect.MinMaxRect(line.x, line.y, valueX, line.yMax), label, Section.CutLabel);
            return Rect.MinMaxRect(valueX, line.y, line.xMax, line.yMax);
        }
    }
}