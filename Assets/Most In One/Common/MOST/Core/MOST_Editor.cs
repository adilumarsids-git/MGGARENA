using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEditor.Build;

namespace Solo.MOST_IN_ONE
{
    #region Enums
    public enum MOSTEdit { None, RuntimeOnly }
    #endregion

    #region HAS_INPUT_SYSTEM_PACKAGE
#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class InputSystemDefineSync
    {
        const string PackageName = "com.unity.inputsystem";
        const string Define = "HAS_INPUT_SYSTEM_PACKAGE";

        static ListRequest _listRequest;
        static bool _isPolling;

        static InputSystemDefineSync()
        {
            EditorApplication.delayCall += Refresh;
            AssemblyReloadEvents.afterAssemblyReload += Refresh;
        }

        [MenuItem("Tools/MOST/Refresh Input System Define")]
        public static void Refresh()
        {
            if (_isPolling) return;

            _isPolling = true;
            _listRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_listRequest == null) { StopPolling(); return; }
            if (!_listRequest.IsCompleted) return;

            try
            {
                bool installed = false;

                if (_listRequest.Status == StatusCode.Success && _listRequest.Result != null)
                {
                    installed = _listRequest.Result.Any(p => string.Equals(p.name, PackageName, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    StopPolling();
                    return;
                }

                ApplyDefineToAllBuildTargetGroups(installed);
            }
            finally
            {
                StopPolling();
            }
        }

        static void StopPolling()
        {
            EditorApplication.update -= Poll;
            _listRequest = null;
            _isPolling = false;
        }

        static void ApplyDefineToAllBuildTargetGroups(bool shouldHaveDefine)
        {
            bool anyChanged = false;

            foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (group == BuildTargetGroup.Unknown) continue;

                if (!IsValidBuildTargetGroup(group)) continue;

                var target = NamedBuildTarget.FromBuildTargetGroup(group);
                var defines = PlayerSettings.GetScriptingDefineSymbols(target);
                var set = defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(s => s.Trim())
                                 .Where(s => s.Length > 0)
                                 .ToList();

                bool has = set.Contains(Define);

                if (shouldHaveDefine && !has)
                {
                    set.Add(Define);
                    anyChanged = true;
                }
                else if (!shouldHaveDefine && has)
                {
                    set.RemoveAll(s => s == Define);
                    anyChanged = true;
                }

                if (anyChanged)
                {
                    var joined = string.Join(";", set.Distinct());
                    PlayerSettings.SetScriptingDefineSymbols(target, joined);
                }
            }
            if (anyChanged) AssetDatabase.Refresh();
        }

        static bool IsValidBuildTargetGroup(BuildTargetGroup group)
        {
            try
            {
                var target = NamedBuildTarget.FromBuildTargetGroup(group);
                PlayerSettings.GetScriptingDefineSymbols(target);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
#endif
    #endregion

    #region MOSTRange Property
    [Serializable]
    public struct MOSTRange
    {
        [SerializeField] float min;
        [SerializeField] float max;

        public float Min { readonly get { return min > max ? max : min; } set { if (value > max) { min = max; max = value; } else min = value; } }
        public float Max { readonly get { return min < max ? max : min; } set { if (value < min) { max = min; min = value; } else max = value; } }

        public MOSTRange(float min, float max) { this.min = min > max ? max : min; this.max = min < max ? max : min; }

        public readonly float GetRandomValue(){ return UnityEngine.Random.Range(min, max); }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MOSTRange))]
    public class MOSTRangeDrawer : PropertyDrawer
    {
        const float k_SubLabelWidth = 28f;
        const float k_FieldSpacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");
            if (minProp == null || maxProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Use with { float Min, float Max }");
                EditorGUI.EndProperty();
                return;
            }

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            float half = (position.width - k_FieldSpacing) * 0.5f;
            Rect minFieldRect = new(position.x, position.y, half, position.height);
            Rect maxFieldRect = new(position.x + half + k_FieldSpacing, position.y, half, position.height);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            bool prevWide = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            EditorGUIUtility.labelWidth = k_SubLabelWidth;

            float minVal = minProp.floatValue;
            float maxVal = maxProp.floatValue;

            EditorGUI.BeginChangeCheck();

            minVal = EditorGUI.FloatField(minFieldRect, new GUIContent("Min"), minVal);
            maxVal = EditorGUI.FloatField(maxFieldRect, new GUIContent("Max"), maxVal);

            if (EditorGUI.EndChangeCheck())
            {
                if (minVal > maxVal)
                {
                    if (minProp.floatValue != minVal) maxVal = minVal;
                    else minVal = maxVal;
                }
                minProp.floatValue = minVal;
                maxProp.floatValue = maxVal;
                property.serializedObject.ApplyModifiedProperties();
            }
            EditorGUIUtility.labelWidth = prevLabelWidth;
            EditorGUIUtility.wideMode = prevWide;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;
    }
#endif
#endregion

    #region MinMaxSlider Attribute
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class MinMaxSliderAttribute : PropertyAttribute
    {
        public enum Source { Const, Ref }

        public readonly Source MinSource;
        public readonly Source MaxSource;

        public readonly float MinConst;
        public readonly float MaxConst;

        public readonly string MinRef;
        public readonly string MaxRef;

        public MinMaxSliderAttribute(float minConst, float maxConst)
        { MinSource = Source.Const; MinConst = minConst; MaxSource = Source.Const; MaxConst = maxConst; }

        public MinMaxSliderAttribute(string minRef, string maxRef)
        { MinSource = Source.Ref; MinRef = minRef; MaxSource = Source.Ref; MaxRef = maxRef; }

        public MinMaxSliderAttribute(string minRef, float maxConst)
        { MinSource = Source.Ref; MinRef = minRef; MaxSource = Source.Const; MaxConst = maxConst; }

        public MinMaxSliderAttribute(float minConst, string maxRef)
        { MinSource = Source.Const; MinConst = minConst; MaxSource = Source.Ref; MaxRef = maxRef; }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
    public class MinMaxSliderDrawer : PropertyDrawer
    {
        const float FieldWidth = 50f;
        const float Spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");
            if (minProp == null || maxProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Use with MostRange { float Min, float Max }");
                return;
            }

            var attr = (MinMaxSliderAttribute)attribute;
            if (!TryResolveLimit(property, attr.MinSource, attr.MinConst, attr.MinRef, out float hardMin) ||
                !TryResolveLimit(property, attr.MaxSource, attr.MaxConst, attr.MaxRef, out float hardMax))
            {
                EditorGUI.HelpBox(position, "MinMaxSlider: couldn't resolve one/both limits.", MessageType.Error);
                return;
            }
            if (hardMin > hardMax) (hardMin, hardMax) = (hardMax, hardMin); 

            float curMin = minProp.floatValue;
            float curMax = maxProp.floatValue;

            float clampedMin = Mathf.Clamp(curMin, hardMin, hardMax);
            float clampedMax = Mathf.Clamp(curMax, hardMin, hardMax);
            if (clampedMin > clampedMax) clampedMin = clampedMax;

            if (!Mathf.Approximately(curMin, clampedMin) || !Mathf.Approximately(curMax, clampedMax))
            {
                var so = property.serializedObject;
                foreach (var t in so.targetObjects) Undo.RecordObject(t, $"Clamp {label.text}");
                minProp.floatValue = clampedMin;
                maxProp.floatValue = clampedMax;
                so.ApplyModifiedProperties();
                foreach (var t in so.targetObjects) EditorUtility.SetDirty(t);
                curMin = clampedMin;
                curMax = clampedMax;
            }

            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            Rect minRect = new(position.x, position.y, FieldWidth, position.height);
            Rect maxRect = new(position.xMax - FieldWidth, position.y, FieldWidth, position.height);
            Rect sliderRect = new(minRect.xMax + Spacing, position.y,
                                  Mathf.Max(0, maxRect.xMin - (minRect.xMax + Spacing)), position.height);

            float minValue = curMin;
            float maxValue = curMax;

            EditorGUI.BeginChangeCheck();

            minValue = EditorGUI.FloatField(minRect, minValue);
            EditorGUI.MinMaxSlider(sliderRect, ref minValue, ref maxValue, hardMin, hardMax);
            maxValue = EditorGUI.FloatField(maxRect, maxValue);

            if (EditorGUI.EndChangeCheck())
            {
                minValue = Mathf.Clamp(minValue, hardMin, hardMax);
                maxValue = Mathf.Clamp(maxValue, hardMin, hardMax);
                if (minValue > maxValue) minValue = maxValue;

                var so = property.serializedObject;
                foreach (var t in so.targetObjects) Undo.RecordObject(t, $"Change {label.text}");
                minProp.floatValue = minValue;
                maxProp.floatValue = maxValue;
                so.ApplyModifiedProperties();
                foreach (var t in so.targetObjects) EditorUtility.SetDirty(t);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;

        static bool TryResolveLimit(SerializedProperty owner, MinMaxSliderAttribute.Source kind,
                                    float constVal, string refPath, out float value)
        {
            if (kind == MinMaxSliderAttribute.Source.Const)
            { value = constVal; return true; }

            var p = owner.serializedObject.FindProperty(BuildSiblingPath(owner, refPath));
            if (p == null) { value = 0f; return false; }

            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: value = p.floatValue; return true;
                case SerializedPropertyType.Integer: value = p.intValue; return true;
                default: value = 0f; return false;
            }
        }

        static string BuildSiblingPath(SerializedProperty property, string rel)
        {
            int i = property.propertyPath.LastIndexOf('.');
            var parent = i >= 0 ? property.propertyPath[..i] : string.Empty;
            return string.IsNullOrEmpty(parent) ? rel : $"{parent}.{rel}";
        }
    }
#endif
#endregion

    #region MOSTRangeInt Property
    [Serializable]
    public struct MOSTRangeInt
    {
        [SerializeField] int min;
        [SerializeField] int max;

        public int Min { readonly get { return min > max ? max : min; } set { if (value > max) { min = max; max = value; } else min = value; } }
        public int Max { readonly get { return min < max ? max : min; } set { if (value < min) { max = min; min = value; } else max = value; } }

        public MOSTRangeInt(int min, int max) { this.min = min > max ? max : min; this.max = min < max ? max : min; }

        public readonly int GetRandomValue() { return UnityEngine.Random.Range(min, max); }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MOSTRangeInt))]
    public class MOSTRangeIntDrawer : PropertyDrawer
    {
        const float k_SubLabelWidth = 28f;
        const float k_FieldSpacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");
            if (minProp == null || maxProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Use with { int Min, int Max }");
                EditorGUI.EndProperty();
                return;
            }

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            float half = (position.width - k_FieldSpacing) * 0.5f;
            Rect minFieldRect = new(position.x, position.y, half, position.height);
            Rect maxFieldRect = new(position.x + half + k_FieldSpacing, position.y, half, position.height);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            bool prevWide = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            EditorGUIUtility.labelWidth = k_SubLabelWidth;

            int minVal = minProp.intValue;
            int maxVal = maxProp.intValue;

            EditorGUI.BeginChangeCheck();

            minVal = EditorGUI.IntField(minFieldRect, new GUIContent("Min"), minVal);
            maxVal = EditorGUI.IntField(maxFieldRect, new GUIContent("Max"), maxVal);

            if (EditorGUI.EndChangeCheck())
            {
                if (minVal > maxVal)
                {
                    if (minProp.intValue != minVal) maxVal = minVal;
                    else minVal = maxVal;
                }
                minProp.intValue = minVal;
                maxProp.intValue = maxVal;
                property.serializedObject.ApplyModifiedProperties();
            }
            EditorGUIUtility.labelWidth = prevLabelWidth;
            EditorGUIUtility.wideMode = prevWide;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;
    }
#endif
#endregion

    #region MinMaxSliderInt Attribute
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class MinMaxSliderIntAttribute : PropertyAttribute
    {
        public enum SourceInt { Const, Ref }

        public readonly SourceInt MinSource;
        public readonly SourceInt MaxSource;

        public readonly int MinConst;
        public readonly int MaxConst;

        public readonly string MinRef;
        public readonly string MaxRef;

        public MinMaxSliderIntAttribute(int minConst, int maxConst)
        { MinSource = SourceInt.Const; MinConst = minConst; MaxSource = SourceInt.Const; MaxConst = maxConst; }

        public MinMaxSliderIntAttribute(string minRef, string maxRef)
        { MinSource = SourceInt.Ref; MinRef = minRef; MaxSource = SourceInt.Ref; MaxRef = maxRef; }

        public MinMaxSliderIntAttribute(string minRef, int maxConst)
        { MinSource = SourceInt.Ref; MinRef = minRef; MaxSource = SourceInt.Const; MaxConst = maxConst; }

        public MinMaxSliderIntAttribute(int minConst, string maxRef)
        { MinSource = SourceInt.Const; MinConst = minConst; MaxSource = SourceInt.Ref; MaxRef = maxRef; }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MinMaxSliderIntAttribute))]
    public class MinMaxSliderIntDrawer : PropertyDrawer
    {
        const float FieldWidth = 50f;
        const float Spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");
            if (minProp == null || maxProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Use with MostRangeInt { int Min, int Max }");
                return;
            }

            var attr = (MinMaxSliderIntAttribute)attribute;
            if (!TryResolveLimit(property, attr.MinSource, attr.MinConst, attr.MinRef, out float hardMin) ||
                !TryResolveLimit(property, attr.MaxSource, attr.MaxConst, attr.MaxRef, out float hardMax))
            {
                EditorGUI.HelpBox(position, "MinMaxSlider: couldn't resolve one/both limits.", MessageType.Error);
                return;
            }
            if (hardMin > hardMax) (hardMin, hardMax) = (hardMax, hardMin);

            int curMin = minProp.intValue;
            int curMax = maxProp.intValue;

            int clampedMin = (int)Mathf.Clamp(curMin, hardMin, hardMax);
            int clampedMax = (int)Mathf.Clamp(curMax, hardMin, hardMax);
            if (clampedMin > clampedMax) clampedMin = clampedMax;

            if (!Mathf.Approximately(curMin, clampedMin) || !Mathf.Approximately(curMax, clampedMax))
            {
                var so = property.serializedObject;
                foreach (var t in so.targetObjects) Undo.RecordObject(t, $"Clamp {label.text}");
                minProp.intValue = clampedMin;
                maxProp.intValue = clampedMax;
                so.ApplyModifiedProperties();
                foreach (var t in so.targetObjects) EditorUtility.SetDirty(t);
                curMin = clampedMin;
                curMax = clampedMax;
            }

            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            Rect minRect = new(position.x, position.y, FieldWidth, position.height);
            Rect maxRect = new(position.xMax - FieldWidth, position.y, FieldWidth, position.height);
            Rect sliderRect = new(minRect.xMax + Spacing, position.y,
                                  Mathf.Max(0, maxRect.xMin - (minRect.xMax + Spacing)), position.height);

            float minValue = curMin;
            float maxValue = curMax;

            EditorGUI.BeginChangeCheck();

            minValue = EditorGUI.IntField(minRect, (int)minValue);
            EditorGUI.MinMaxSlider(sliderRect, ref minValue, ref maxValue, hardMin, hardMax);
            maxValue = EditorGUI.IntField(maxRect, (int)maxValue);

            if (EditorGUI.EndChangeCheck())
            {
                minValue = Mathf.Clamp(minValue, hardMin, hardMax);
                maxValue = Mathf.Clamp(maxValue, hardMin, hardMax);
                if (minValue > maxValue) minValue = maxValue;

                var so = property.serializedObject;
                foreach (var t in so.targetObjects) Undo.RecordObject(t, $"Change {label.text}");
                minProp.intValue = (int)minValue;
                maxProp.intValue = (int)maxValue;
                so.ApplyModifiedProperties();
                foreach (var t in so.targetObjects) EditorUtility.SetDirty(t);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;

        static bool TryResolveLimit(SerializedProperty owner, MinMaxSliderIntAttribute.SourceInt kind,
                                    float constVal, string refPath, out float value)
        {
            if (kind == MinMaxSliderIntAttribute.SourceInt.Const)
            { value = constVal; return true; }

            var p = owner.serializedObject.FindProperty(BuildSiblingPath(owner, refPath));
            if (p == null) { value = 0f; return false; }

            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: value = p.intValue; return true;
                case SerializedPropertyType.Integer: value = p.intValue; return true;
                default: value = 0f; return false;
            }
        }

        static string BuildSiblingPath(SerializedProperty property, string rel)
        {
            int i = property.propertyPath.LastIndexOf('.');
            var parent = i >= 0 ? property.propertyPath[..i] : string.Empty;
            return string.IsNullOrEmpty(parent) ? rel : $"{parent}.{rel}";
        }
    }
#endif
#endregion

    #region BigHeader Attribute
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class BigHeaderAttribute : PropertyAttribute
    {
        public string Text { get; }
        public Color Color { get; }
        [Min(1f)] public int FontSize;

        public BigHeaderAttribute(string text)
        {
            Text = text; Color = new Color32(252, 191, 1, 255); FontSize = 15;
        }

        public BigHeaderAttribute(string text, float r, float g, float b)
        {
            Text = text; Color = new Color(r, g, b, 1f); FontSize = 15;
        }

        public BigHeaderAttribute(string text, byte r, byte g, byte b)
        {
            Text = text; Color = new Color32(r, g, b, 255); FontSize = 15;
        }

        public BigHeaderAttribute(string text, int fontSize)
        {
            Text = text; Color = new Color32(252, 191, 1, 255); FontSize = Mathf.Max(1, fontSize);
        }

        public BigHeaderAttribute(string text, float r, float g, float b, int fontSize)
        {
            Text = text; Color = new Color(r, g, b, 1f); FontSize = Mathf.Max(1, fontSize);
        }

        public BigHeaderAttribute(string text, byte r, byte g, byte b, int fontSize)
        {
            Text = text; Color = new Color32(r, g, b, 255); FontSize = Mathf.Max(1, fontSize);
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(BigHeaderAttribute))]
    public class BigHeaderPropertyDrawer : DecoratorDrawer
    {
        public override void OnGUI(Rect position)
        {
            BigHeaderAttribute attributeHandle = (BigHeaderAttribute)attribute;

            position.yMin += EditorGUIUtility.singleLineHeight * 0.5f;
            position = EditorGUI.IndentedRect(position);

            GUIStyle headerTextStyle = new()
            {
                fontSize = attributeHandle.FontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = attributeHandle.Color }
            };

            GUI.Label(position, attributeHandle.Text, headerTextStyle);
            EditorGUI.DrawRect(new Rect(position.xMin, position.yMin, position.width, 1), attributeHandle.Color);
        }

        public override float GetHeight()
        {
            return EditorGUIUtility.singleLineHeight * 2f;
        }
    }
#endif
#endregion

    #region Line Attribute
    public class LineAttribute : PropertyAttribute
    {
        public readonly float thickness;
        public readonly float width;
        public readonly Color color;
        public readonly float spacing;

        public LineAttribute(float thickness = 1f, float width = 1f, float r = 0.34f, float g = 0.34f, float b = 0.34f, float a = 1f, float spacing = 7f)
        {
            this.thickness = thickness;
            this.width = Mathf.Clamp01(width);
            this.color = new Color(r, g, b, a);
            this.spacing = spacing;
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(LineAttribute))]
    public class LineDrawer : DecoratorDrawer
    {
        public override float GetHeight()
        {
            LineAttribute lineAttribute = (LineAttribute)attribute;
            return lineAttribute.spacing * 2 + lineAttribute.thickness;
        }

        public override void OnGUI(Rect position)
        {
            LineAttribute lineAttribute = (LineAttribute)attribute;

            // Calculate centered position
            float width = position.width * lineAttribute.width;
            float xOffset = (position.width - width) * 0.5f;

            // Spacing above the line
            position.y += lineAttribute.spacing;
            position.height = lineAttribute.thickness;

            // Draw the line
            Rect lineRect = new Rect(
                position.x + xOffset,
                position.y,
                width,
                lineAttribute.thickness
            );

            EditorGUI.DrawRect(lineRect, lineAttribute.color);
        }
    }
#endif
#endregion

    #region ReadOnly Attribute
    public class ReadOnlyAttribute : PropertyAttribute { }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
#endif
#endregion

    #region ReadOnlyIf Attribute
    public class ReadOnlyIfAttribute : PropertyAttribute
    {
        public string ConditionField { get; }
        public bool ReadOnlyWhen { get; }
        public object CompareValue { get; }

        public string ConditionFieldSec { get; }
        public bool ReadOnlyWhenSec { get; }
        public object CompareValueSec { get; }

        // Boolean condition
        public ReadOnlyIfAttribute(string boolFieldName, bool readOnlyWhen = true)
        {
            ConditionField = boolFieldName;
            ReadOnlyWhen = readOnlyWhen;
            CompareValue = null;
        }

        // Enum condition
        public ReadOnlyIfAttribute(string enumFieldName, object enumValue, bool readOnlyWhen = true)
        {
            ConditionField = enumFieldName;
            ReadOnlyWhen = readOnlyWhen;
            CompareValue = enumValue;
        }

        public ReadOnlyIfAttribute(string enumFieldName, object enumValue1, object enumValue2)
        {
            ConditionField = enumFieldName;
            ReadOnlyWhen = true;
            CompareValue = enumValue1;
            CompareValueSec = enumValue2;
        }

        public ReadOnlyIfAttribute(string enumFieldName1, object enumValue1, bool ReadOnlyWhen1, string enumFieldName2, object enumValue2, bool ReadOnlyWhen2)
        {
            ConditionField = enumFieldName1;
            ReadOnlyWhen = ReadOnlyWhen1;
            CompareValue = enumValue1;

            ConditionFieldSec = enumFieldName2;
            ReadOnlyWhenSec = ReadOnlyWhen2;
            CompareValueSec = enumValue2;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ReadOnlyIfAttribute))]
    public class ReadOnlyIfDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (attribute is not ReadOnlyIfAttribute readOnlyIf) return;

            bool shouldBeReadOnly = EvaluateCondition(property, readOnlyIf);
            bool wasEnabled = GUI.enabled;

            GUI.enabled = !shouldBeReadOnly;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = wasEnabled;
        }

        bool EvaluateCondition(SerializedProperty property, ReadOnlyIfAttribute readOnlyIf)
        {
            // For non-MonoBehaviour classes, we need to handle nested property paths
            SerializedProperty conditionProperty = FindConditionProperty(property, readOnlyIf.ConditionField);
            if (conditionProperty == null)
            {
                Debug.LogWarning($"ReadOnlyIf: Condition field '{readOnlyIf.ConditionField}' not found in property path: {property.propertyPath}");
                return false;
            }

            if (readOnlyIf.CompareValue == null) // Boolean condition
            {
                return conditionProperty.boolValue == readOnlyIf.ReadOnlyWhen;
            }
            else if (readOnlyIf.CompareValueSec == null) // Enum condition
            {
                try
                {
                    return conditionProperty.enumValueIndex == (int)readOnlyIf.CompareValue == readOnlyIf.ReadOnlyWhen;
                }
                catch (Exception e)
                {
                    Debug.LogError($"ReadOnlyIf: Invalid enum comparison for field {readOnlyIf.ConditionField}: {e.Message}");
                    return false;
                }
            }
            else
            {
                try
                {
                    if (readOnlyIf.ConditionFieldSec == null || readOnlyIf.ConditionFieldSec == string.Empty)
                        return (conditionProperty.enumValueIndex == (int)readOnlyIf.CompareValue ||
                            conditionProperty.enumValueIndex == (int)readOnlyIf.CompareValueSec) == readOnlyIf.ReadOnlyWhen;
                    else
                    {
                        SerializedProperty conditionPropertySec = FindConditionProperty(property, readOnlyIf.ConditionFieldSec);
                        return (conditionProperty.enumValueIndex == (int)readOnlyIf.CompareValue == readOnlyIf.ReadOnlyWhen) &&
                        (conditionPropertySec.enumValueIndex == (int)readOnlyIf.CompareValueSec == readOnlyIf.ReadOnlyWhenSec);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"ReadOnlyIf: Invalid enum comparison for field {readOnlyIf.ConditionField}: {e.Message}");
                    return false;
                }
            }
        }

        SerializedProperty FindConditionProperty(SerializedProperty property, string conditionFieldName)
        {
            // First try direct finding
            SerializedProperty conditionProperty = property.serializedObject.FindProperty(conditionFieldName);
            if (conditionProperty != null) return conditionProperty;

            // For nested properties (like in arrays), try to find relative to current property
            string basePath = GetBasePropertyPath(property.propertyPath);
            if (!string.IsNullOrEmpty(basePath))
            {
                string fullConditionPath = $"{basePath}.{conditionFieldName}";
                conditionProperty = property.serializedObject.FindProperty(fullConditionPath);
            }

            return conditionProperty;
        }

        string GetBasePropertyPath(string propertyPath)
        {
            // Extract base path from nested property paths
            // Example: "effects.Array.data[0].someField" -> "effects.Array.data[0]"
            int lastDotIndex = propertyPath.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                return propertyPath.Substring(0, lastDotIndex);
            }

            return string.Empty;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
#endif
#endregion

    #region HelpBox Attribute
    public enum HelpBoxKind { Info, Warning, Error, None }
        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
        public class HelpBoxAttribute : PropertyAttribute
        {
            public readonly string Message;
            public readonly HelpBoxKind Kind;
            public readonly float Top;
            public readonly float Bottom;
            public readonly bool RichText;

            public HelpBoxAttribute(string message, HelpBoxKind kind = HelpBoxKind.Info, float top = 4f, float bottom = 4f, bool richText = false)
            {
                Message = message ?? string.Empty;
                Kind = kind;
                Top = Mathf.Max(0f, top);
                Bottom = Mathf.Max(0f, bottom);
                RichText = richText;
            }
        }
#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(HelpBoxAttribute))]
        public class HelpBoxDecoratorDrawer : DecoratorDrawer
        {
            HelpBoxAttribute A => (HelpBoxAttribute)attribute;

            static GUIStyle _rtHelpBoxStyle;
            static GUIStyle RtHelpBoxStyle
            {
                get
                {
                _rtHelpBoxStyle ??= new GUIStyle(EditorStyles.helpBox)
                        {
                            richText = true,
                            wordWrap = true,
                            alignment = TextAnchor.MiddleLeft,
                        };
                    return _rtHelpBoxStyle;
                }
            }

            public override float GetHeight()
            {
                if (string.IsNullOrEmpty(A.Message))
                    return 0f;
                float viewWidth = EditorGUIUtility.currentViewWidth;
                float indentPx = EditorGUI.indentLevel * 15f; // Unity's indent size approximation
                float contentWidth = Mathf.Max(0f, viewWidth - indentPx - 32f); // 32 ~ padding + scrollbar buffer

                var style = A.RichText ? RtHelpBoxStyle : EditorStyles.helpBox;
                float textHeight = style.CalcHeight(new GUIContent(A.Message), contentWidth);
                return A.Top + textHeight + A.Bottom;
            }

            public override void OnGUI(Rect position)
            {
                if (string.IsNullOrEmpty(A.Message))
                    return;

                position = EditorGUI.IndentedRect(position);
                var drawRect = new Rect(position.x, position.y + A.Top, position.width, position.height - (A.Top + A.Bottom));
                MessageType mt = MessageType.Info;
                switch (A.Kind)
                {
                    case HelpBoxKind.Warning: mt = MessageType.Warning; break;
                    case HelpBoxKind.Error: mt = MessageType.Error; break;
                    case HelpBoxKind.None: mt = MessageType.None; break;
                }

                if (A.RichText) GUI.Label(drawRect, new GUIContent(A.Message), RtHelpBoxStyle);
                else EditorGUI.HelpBox(drawRect, A.Message, mt);
            }
        }
#endif
    #endregion

    #region GUIColor Attribute
    public class GUIColorAttribute : PropertyAttribute
    {
        public Color Color { get; }
        public bool ApplyToChildren { get; }

        // Constructor using Color
        public GUIColorAttribute(float r, float g, float b, float a = 1f, bool applyToChildren = false)
        {
            Color = new Color(r, g, b, a);
            ApplyToChildren = applyToChildren;
        }

        // Constructor using predefined color names
        public GUIColorAttribute(string colorName, bool applyToChildren = false)
        {
            ApplyToChildren = applyToChildren;
            Color = GetColorByName(colorName);
        }

        static Color GetColorByName(string name)
        {
            return name.ToLower() switch
            {
                "red" => Color.red,
                "green" => Color.green,
                "blue" => Color.blue,
                "yellow" => Color.yellow,
                "cyan" => Color.cyan,
                "magenta" => Color.magenta,
                "white" => Color.white,
                "black" => Color.black,
                "gray" or "grey" => Color.grey,
                "clear" => Color.clear,
                _ => Color.white
            };
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(GUIColorAttribute))]
    public class GUIColorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var colorAttribute = (GUIColorAttribute)attribute;

            // Save original colors
            Color originalColor = GUI.color;
            Color originalContentColor = GUI.contentColor;
            Color originalBackgroundColor = GUI.backgroundColor;

            // Apply the color
            GUI.color = colorAttribute.Color;
            GUI.contentColor = colorAttribute.Color;
            GUI.backgroundColor = colorAttribute.Color;

            // Draw the property
            if (colorAttribute.ApplyToChildren)
            {
                // For complex properties, we need to handle children differently
                EditorGUI.PropertyField(position, property, label, true);
            }
            else
            {
                // For simple properties, draw just this field
                EditorGUI.PropertyField(position, property, label);
            }

            // Restore original colors
            GUI.color = originalColor;
            GUI.contentColor = originalContentColor;
            GUI.backgroundColor = originalBackgroundColor;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (((GUIColorAttribute)attribute).ApplyToChildren) return EditorGUI.GetPropertyHeight(property, label, true);
            else return EditorGUI.GetPropertyHeight(property, label);
        }
    }
#endif
#endregion

    #region Required Attribute
    public class RequiredAttribute : PropertyAttribute
    {
        public string errorMessage = "(Require)";

        public RequiredAttribute() { }

        public RequiredAttribute(string errorMessage)
        {
            this.errorMessage = errorMessage;
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(RequiredAttribute))]
    public class RequiredDrawer : PropertyDrawer
    {
        static Texture2D _errorIcon;
        static Texture2D ErrorIcon => _errorIcon = _errorIcon != null ? _errorIcon : EditorGUIUtility.IconContent("console.erroricon").image as Texture2D;

        static GUIStyle _errorStyle;
        static GUIStyle ErrorStyle => _errorStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(1f, 0.3f, 0.3f, 0.8f) },
            fontStyle = FontStyle.Bold
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // First, draw the property exactly as Unity would without any modifications
            EditorGUI.PropertyField(position, property, label);

            // Then, if it's an object reference and empty, overlay the error indicators
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue == null)
            {
                RequiredAttribute requiredAttr = (RequiredAttribute)attribute;

                // Calculate the field area (without affecting layout)
                Rect fieldArea = new (
                    position.x + EditorGUIUtility.labelWidth,
                    position.y,
                    position.width - EditorGUIUtility.labelWidth,
                    position.height
                );

                // Draw error icon (to the left of the field)
                Rect iconRect = new (
                    fieldArea.x - 18f,
                    fieldArea.y + (fieldArea.height - 16f) * 0.5f,
                    16f, 16f
                );

                if (Event.current.type == EventType.Repaint)
                {
                    GUI.DrawTexture(iconRect, ErrorIcon, ScaleMode.ScaleToFit);
                }

                string defaultText = GetDefaultNoneText(property);
                float defaultTextWidth = EditorStyles.label.CalcSize(new GUIContent(defaultText)).x;

                // Draw required text (inside the field, after default text)
                Rect textRect = new (
                    fieldArea.x + defaultTextWidth + 6f,
                    fieldArea.y, 80f,
                    fieldArea.height
                );

                GUI.Label(textRect, requiredAttr.errorMessage, ErrorStyle);
            }
        }

        string GetDefaultNoneText(SerializedProperty property)
        {
            string typeName = "Object";

            if (property.type.Contains("GameObject")) typeName = "Game Object";
            else if (property.type.Contains("Transform")) typeName = "Transform";
            else if (property.type.Contains("MonoBehaviour")) typeName = "Mono Behaviour";
            else if (property.type.Contains("Component")) typeName = "Component";
            else if (property.type.Contains("AudioClip")) typeName = "Audio Clip";
            else if (property.type.Contains("Material")) typeName = "Material";
            else if (property.type.Contains("Texture")) typeName = "Texture";
            else
            {
                // Extract type from PPtr<$Type> format
                int start = property.type.IndexOf("PPtr<$") + 6;
                int end = property.type.IndexOf(">");
                if (start > 5 && end > start)
                {
                    typeName = property.type[start..end];
                    // Clean up common Unity type names
                    typeName = typeName.Replace("UnityEngine.", "");
                }
            }

            return "None (" + typeName + ")";
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Return the exact same height as the default property field
            return EditorGUI.GetPropertyHeight(property, label);
        }
    }
#endif
    #endregion

    #region Inner Hint
    public class InnerHintAttribute : PropertyAttribute
    {
        public string customHint;

        public InnerHintAttribute(string customHint)
        {
            this.customHint = customHint;
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(InnerHintAttribute))]
    public class InnerHintDrawer : PropertyDrawer
    {
        static GUIStyle _hintStyle;
        static GUIStyle HintStyle
        {
            get
            {
                _hintStyle ??= new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.6f, 0.6f, 0.6f, 0.9f) },
                    padding = new RectOffset(2, 2, 0, 0)
                };
                return _hintStyle;
            }
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // First, draw the property exactly as Unity would without any modifications
            EditorGUI.PropertyField(position, property, label);

            // Then, if it's an object reference and empty, overlay the error indicators
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue == null)
            {
                InnerHintAttribute innerHint = (InnerHintAttribute)attribute;

                // Calculate the field area (without affecting layout)
                Rect fieldArea = new(
                    position.x + EditorGUIUtility.labelWidth,
                    position.y,
                    position.width - EditorGUIUtility.labelWidth,
                    position.height
                );

                string defaultText = GetDefaultNoneText(property);
                float defaultTextWidth = EditorStyles.label.CalcSize(new GUIContent(defaultText)).x;

                // Draw required text (inside the field, after default text)
                Rect textRect = new(
                    fieldArea.x + defaultTextWidth + 6f,
                    fieldArea.y, 80f,
                    fieldArea.height
                );

                GUI.Label(textRect, innerHint.customHint, HintStyle);
            }
        }

        string GetDefaultNoneText(SerializedProperty property)
        {
            string typeName = "Object";

            if (property.type.Contains("GameObject")) typeName = "Game Object";
            else if (property.type.Contains("Transform")) typeName = "Transform";
            else if (property.type.Contains("MonoBehaviour")) typeName = "Mono Behaviour";
            else if (property.type.Contains("Component")) typeName = "Component";
            else if (property.type.Contains("AudioClip")) typeName = "Audio Clip";
            else if (property.type.Contains("Material")) typeName = "Material";
            else if (property.type.Contains("Texture")) typeName = "Texture";
            else
            {
                // Extract type from PPtr<$Type> format
                int start = property.type.IndexOf("PPtr<$") + 6;
                int end = property.type.IndexOf(">");
                if (start > 5 && end > start)
                {
                    typeName = property.type[start..end];
                    // Clean up common Unity type names
                    typeName = typeName.Replace("UnityEngine.", "");
                }
            }

            return "None (" + typeName + ")";
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Return the exact same height as the default property field
            return EditorGUI.GetPropertyHeight(property, label);
        }
    }
#endif
    #endregion

    #region Group Attribute
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class GroupAttribute : PropertyAttribute
    {
        public readonly string Name;
        public GroupAttribute(string name) { Name = name; }
    }

    /// Groups any fields marked with [Group("Name")] under a single foldout header per scope.
    /// Scope = the parent path of the field (so the same group name inside different structs/arrays is isolated).
    /// </summary>
#if UNITY_EDITOR
/// <summary>
/// Groups any fields marked with [Group("Name")] under a single foldout header per scope.
/// Scope = the parent path of the field (so the same group name inside different structs/arrays is isolated).
/// </summary>
[CustomPropertyDrawer(typeof(GroupAttribute))]
    public class GroupDrawer : PropertyDrawer
    {
        // Foldout state per (object/scope/group)
        static readonly Dictionary<string, bool> Foldout = new();

        // Cache of "first field path" per (object/scope/group) to know who draws the header
        static readonly Dictionary<string, string> FirstPath = new();

        // Reflection flags
        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        const float VSP = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var groupName = ((GroupAttribute)attribute).Name;
            var key = GetGroupKey(property, groupName);
            var isHeader = IsHeader(property, groupName, key);

            if (!Foldout.ContainsKey(key)) Foldout[key] = true;

            // Draw header once per group/scope
            if (isHeader)
            {
                var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.DrawRect(headerRect, new Color(0.18f, 0.18f, 0.18f, 0.85f));
                Foldout[key] = EditorGUI.Foldout(headerRect, Foldout[key], groupName, true);
                position.y += headerRect.height + EditorGUIUtility.standardVerticalSpacing;
            }

            if (!Foldout[key]) return;

            var fieldLabel = new GUIContent(property.displayName, label?.tooltip);

            using (new EditorGUI.IndentLevelScope(1))
            {
                EditorGUI.BeginProperty(position, fieldLabel, property);
                EditorGUI.PropertyField(position, property, fieldLabel, true);
                EditorGUI.EndProperty();
            }
        }


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var groupName = ((GroupAttribute)attribute).Name;
            var key = GetGroupKey(property, groupName);
            var isHeader = IsHeader(property, groupName, key);

            // Header height (only once per group)
            float h = 0f;
            if (isHeader)
                h += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // If folded: only header shows, members collapse to 0
            if (Foldout.ContainsKey(key) && !Foldout[key])
                return isHeader ? h : 0f;

            // Expanded: header (if first) + property height
            h += EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
            return h + VSP;
        }

        // ---------- helpers ----------

        static string GetGroupKey(SerializedProperty p, string groupName)
        {
            // Key combines: target instance id(s) + scope(base path) + group name.
            // For multi-object editing, aggregate IDs to keep states separate per selection set.
            int[] ids = p.serializedObject.targetObjects.Select(o => o.GetInstanceID()).OrderBy(x => x).ToArray();
            string idPart = string.Join(",", ids);
            string scope = GetScopePath(p);
            return $"{idPart}|{scope}|{groupName}";
        }

        static string GetScopePath(SerializedProperty p)
        {
            // Scope = parent path of this field (without the last segment)
            var path = p.propertyPath;
            int lastDot = path.LastIndexOf('.');
            return lastDot > 0 ? path[..lastDot] : string.Empty;
        }

        bool IsHeader(SerializedProperty property, string groupName, string key)
        {
            // Cache and compare the first path for this group in this scope
            if (!FirstPath.TryGetValue(key, out string firstPath) || string.IsNullOrEmpty(firstPath))
            {
                firstPath = FindFirstPathInGroup(property, groupName);
                FirstPath[key] = firstPath ?? property.propertyPath; // fallback to self to remain stable
            }
            return property.propertyPath == FirstPath[key];
        }

        string FindFirstPathInGroup(SerializedProperty property, string groupName)
        {
            // Iterate all visible properties and return the first in the same scope that also has [Group(groupName)]
            string scope = GetScopePath(property);
            var it = property.serializedObject.GetIterator();
            bool enterChildren = true;

            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;

                // Same scope?
                if (GetScopePath(it) != scope) continue;

                // Has GroupAttribute with same name?
                if (HasGroupAttribute(it, groupName))
                    return it.propertyPath;

                // Stop if we left scope block (cheap heuristic: scope prefix no longer matches)
                // Not strictly necessary, but can cut iterations in large inspectors.
            }
            return null;
        }

        bool HasGroupAttribute(SerializedProperty p, string groupName)
        {
            // Reflect the field to read attributes
            var fi = GetFieldInfo(p, out _);
            if (fi == null) return false;
            var attrs = fi.GetCustomAttributes(typeof(GroupAttribute), false);
            return attrs.Length > 0 && ((GroupAttribute)attrs[0]).Name == groupName;
        }

        static FieldInfo GetFieldInfo(SerializedProperty prop, out Type fieldType)
        {
            fieldType = null;
            var obj = prop.serializedObject.targetObject;
            if (!obj) return null;

            var t = obj.GetType();
            string path = prop.propertyPath.Replace(".Array.data[", "[");
            var segments = path.Split('.');
            FieldInfo fi = null;
            Type parent = t;

            foreach (var seg in segments)
            {
                if (seg.Contains("["))
                {
                    string name = seg[..seg.IndexOf('[')];
                    fi = parent.GetField(name, BF);
                    if (fi == null) return null;
                    parent = GetElementType(fi.FieldType) ?? fi.FieldType;
                }
                else
                {
                    fi = parent.GetField(seg, BF);
                    if (fi == null) return null;
                    parent = fi.FieldType;
                }
            }
            fieldType = parent;
            return fi;
        }

        static Type GetElementType(Type t)
        {
            if (t.IsArray) return t.GetElementType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                return t.GetGenericArguments()[0];
            return null;
        }
    }
#endif
    #endregion

    #region HideScriptField
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class HideScriptFieldAttribute : Attribute { }
#if UNITY_EDITOR
[CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class HideScriptFieldEditor_MB : HideScriptFieldEditorBase { }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true)]
    public class HideScriptFieldEditor_SO : HideScriptFieldEditorBase { }

    public abstract class HideScriptFieldEditorBase : Editor
    {
        public override void OnInspectorGUI()
        {
            // Hide only if ALL selected targets have the attribute
            bool hideScript = targets.All(t => t.GetType()
                                                .GetCustomAttributes(typeof(HideScriptFieldAttribute), true)
                                                .Any());

            // If nothing to hide, just use default inspector
            if (!hideScript)
            {
                base.OnInspectorGUI();
                return;
            }

            serializedObject.Update();
            var prop = serializedObject.GetIterator();
            bool enterChildren = true;

            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                // Skip the script reference on both MonoBehaviours and ScriptableObjects
                if (prop.propertyPath == "m_Script") continue;

                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
    #endregion

    #region Vec3AnimationCurve
    [Serializable]
    public struct Vector3AnimationCurves
    {
        public bool enableX;
        public bool enableY;
        public bool enableZ;

        public AnimationCurve x;
        public AnimationCurve y;
        public AnimationCurve z;

        public Vector3AnimationCurves(AnimationCurve xCurve, AnimationCurve yCurve, AnimationCurve zCurve)
        {
            x = xCurve;
            y = yCurve;
            z = zCurve;
            enableX = enableY = enableZ = true;
        }

        public readonly Vector3 Evaluate(float time)
        {
            return new Vector3(
                enableX && x != null ? x.Evaluate(time) : 0,
                enableY && y != null ? y.Evaluate(time) : 0,
                enableZ && z != null ? z.Evaluate(time) : 0
            );
        }

        public readonly float GetDuration()
        {
            float duration = 0f;

            if (enableX && x != null && x.length > 0)
                duration = Mathf.Max(duration, x.keys[x.length - 1].time);

            if (enableY && y != null && y.length > 0)
                duration = Mathf.Max(duration, y.keys[y.length - 1].time);

            if (enableZ && z != null && z.length > 0)
                duration = Mathf.Max(duration, z.keys[z.length - 1].time);

            return duration;
        }

        public readonly bool IsEnabled()
        {
            return enableX || enableY || enableZ;
        }

        public void Reset()
        {
            x = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0));
            y = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0));
            z = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0));
            enableX = enableY = enableZ = true;
        }
    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(Vector3AnimationCurves))]
    public class Vector3AnimationCurvesDrawer : PropertyDrawer
    {
         const float SPACING = 5f;
         const float CURVE_HEIGHT = 50f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 4 + CURVE_HEIGHT + SPACING * 4;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            Rect labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label);

            // Get properties
            SerializedProperty enableX = property.FindPropertyRelative("enableX");
            SerializedProperty enableY = property.FindPropertyRelative("enableY");
            SerializedProperty enableZ = property.FindPropertyRelative("enableZ");
            SerializedProperty xCurve = property.FindPropertyRelative("x");
            SerializedProperty yCurve = property.FindPropertyRelative("y");
            SerializedProperty zCurve = property.FindPropertyRelative("z");

            float yOffset = EditorGUIUtility.singleLineHeight + SPACING;

            // Draw toggle buttons row
            Rect toggleRowRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
            DrawToggleRow(toggleRowRect, enableX, enableY, enableZ);

            yOffset += EditorGUIUtility.singleLineHeight + SPACING;

            // Draw curve fields
            Rect curvesRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight * 2);
            DrawCurveFields(curvesRect, enableX, enableY, enableZ, xCurve, yCurve, zCurve);

            yOffset += EditorGUIUtility.singleLineHeight * 2 + SPACING;

            // Draw preview
            Rect previewRect = new Rect(position.x, position.y + yOffset, position.width, CURVE_HEIGHT);
            DrawPreview(previewRect, enableX, enableY, enableZ, xCurve, yCurve, zCurve);

            EditorGUI.EndProperty();
        }

        void DrawToggleRow(Rect rect, SerializedProperty enableX, SerializedProperty enableY, SerializedProperty enableZ)
        {
            float toggleWidth = rect.width / 3f;

            Rect xRect = new (rect.x, rect.y, toggleWidth, rect.height);
            Rect yRect = new (rect.x + toggleWidth, rect.y, toggleWidth, rect.height);
            Rect zRect = new (rect.x + toggleWidth * 2, rect.y, toggleWidth, rect.height);

            enableX.boolValue = GUI.Toggle(xRect, enableX.boolValue, "X", "Button");
            enableY.boolValue = GUI.Toggle(yRect, enableY.boolValue, "Y", "Button");
            enableZ.boolValue = GUI.Toggle(zRect, enableZ.boolValue, "Z", "Button");
        }

        void DrawCurveFields(Rect rect, SerializedProperty enableX, SerializedProperty enableY, SerializedProperty enableZ,
                                    SerializedProperty xCurve, SerializedProperty yCurve, SerializedProperty zCurve)
        {
            float curveWidth = rect.width / 3f;

            GUI.enabled = enableX.boolValue;
            Rect xRect = new (rect.x, rect.y, curveWidth, rect.height);
            EditorGUI.PropertyField(xRect, xCurve, GUIContent.none);

            GUI.enabled = enableY.boolValue;
            Rect yRect = new (rect.x + curveWidth, rect.y, curveWidth, rect.height);
            EditorGUI.PropertyField(yRect, yCurve, GUIContent.none);

            GUI.enabled = enableZ.boolValue;
            Rect zRect = new (rect.x + curveWidth * 2, rect.y, curveWidth, rect.height);
            EditorGUI.PropertyField(zRect, zCurve, GUIContent.none);

            GUI.enabled = true;
        }

        void DrawPreview(Rect rect, SerializedProperty enableX, SerializedProperty enableY, SerializedProperty enableZ,
                               SerializedProperty xCurve, SerializedProperty yCurve, SerializedProperty zCurve)
        {
            // Draw background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.3f));

            Handles.BeginGUI();

            // Draw each enabled curve
            if (enableX.boolValue && xCurve.animationCurveValue != null && xCurve.animationCurveValue.length > 0)
            {
                Handles.color = Color.red;
                DrawNormalizedCurve(rect, xCurve.animationCurveValue);
            }

            if (enableY.boolValue && yCurve.animationCurveValue != null && yCurve.animationCurveValue.length > 0)
            {
                Handles.color = Color.green;
                DrawNormalizedCurve(rect, yCurve.animationCurveValue);
            }

            if (enableZ.boolValue && zCurve.animationCurveValue != null && zCurve.animationCurveValue.length > 0)
            {
                Handles.color = Color.blue;
                DrawNormalizedCurve(rect, zCurve.animationCurveValue);
            }

            Handles.EndGUI();

            // Draw border
            Handles.BeginGUI();
            Handles.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            Handles.DrawPolyLine(
                new Vector3(rect.x, rect.y, 0),
                new Vector3(rect.x + rect.width, rect.y, 0),
                new Vector3(rect.x + rect.width, rect.y + rect.height, 0),
                new Vector3(rect.x, rect.y + rect.height, 0),
                new Vector3(rect.x, rect.y, 0)
            );
            Handles.EndGUI();
        }

        void DrawNormalizedCurve(Rect rect, AnimationCurve curve)
        {
            int segments = Mathf.Min(50, (int)rect.width);
            Vector3[] points = new Vector3[segments];

            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)(segments - 1);
                float value = curve.Evaluate(t);

                // Normalize value for display
                float min = 0f, max = 1f;
                foreach (Keyframe key in curve.keys)
                {
                    min = Mathf.Min(min, key.value);
                    max = Mathf.Max(max, key.value);
                }

                float normalizedValue = Mathf.InverseLerp(min, max, value);

                points[i] = new Vector3(
                    rect.x + t * rect.width,
                    rect.y + rect.height - normalizedValue * rect.height,
                    0
                );
            }

            Handles.DrawAAPolyLine(2f, points);
        }
    }
#endif
    #endregion

    #region HideIf Attribute
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class HideIfAnyAttribute : PropertyAttribute
    {
        public readonly object[] Triplets; // (field, value, hideWhen) x N
        public MOSTEdit EditState;
        public HideIfAnyAttribute(params object[] triplets) => Triplets = triplets;
        public HideIfAnyAttribute(MOSTEdit edi) => EditState = edi;
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class HideIfAllAttribute : PropertyAttribute
    {
        public readonly object[] Triplets; // (field, value, hideWhen) x N
        public MOSTEdit EditState;
        public HideIfAllAttribute(params object[] triplets) => Triplets = triplets;
        public HideIfAllAttribute(MOSTEdit edi) => EditState = edi;
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(HideIfAnyAttribute), true)]
    [CustomPropertyDrawer(typeof(HideIfAllAttribute), true)]
    public class HideIfMultiDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ShouldHide(property))
                return;

            var fieldLabel = new GUIContent(property.displayName, label?.tooltip);
            EditorGUI.BeginProperty(position, fieldLabel, property);
            EditorGUI.PropertyField(position, property, fieldLabel, true);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ShouldHide(property)
                ? 0f
                : EditorGUI.GetPropertyHeight(property, new GUIContent(property.displayName, label?.tooltip), true);
        }

        bool ShouldHide(SerializedProperty property)
        {
            //// Extract rules
            //MOSTEdit ed = attribute switch
            //{
            //    HideIfAnyAttribute a => a.EditState,
            //    HideIfAllAttribute a => a.EditState,
            //    _ => MOSTEdit.None
            //};
            //if (ed == MOSTEdit.RuntimeOnly && EditorApplication.isPlaying) return true;

            object[] triplets = attribute switch
            {
                HideIfAnyAttribute a => a.Triplets,
                HideIfAllAttribute a => a.Triplets,
                _ => Array.Empty<object>()
            };

            if (triplets == null || triplets.Length == 0) return false;
            if (triplets.Length % 3 != 0)
            {
                Debug.LogError($"[{attribute.GetType().Name}] Expected triplets: (field, value, hideWhen)*N");
                return false;
            }

            bool isAll = attribute is HideIfAllAttribute;
            bool anyHit = false;

            for (int i = 0; i < triplets.Length; i += 3)
            {
                string fieldName = triplets[i] as string;
                object compare = triplets[i + 1];
                bool hideWhen = triplets[i + 2] is bool b && b;

                if (string.IsNullOrEmpty(fieldName))
                    continue;

                var condProp = ResolveCondition(property, fieldName);
                if (condProp == null) // unresolved → treat as not hiding
                {
                    if (isAll) return false; // ALL fails if any can't be evaluated
                    continue;                // ANY just skips this rule
                }

                bool hit;
                if (compare is bool)
                {
                    // boolean rule: hide when (cond == hideWhen)
                    bool cond = ReadBool(condProp);
                    hit = (cond == hideWhen);
                }
                else
                {
                    int c = ReadInt(condProp);
                    int v = ToInt(compare);
                    // enum/int rule: hide when ((c == v) == hideWhen)
                    hit = ((c == v) == hideWhen);
                }

                if (isAll)
                {
                    if (!hit) return false; // ALL requires all rules hit
                }
                else
                {
                    if (hit) { anyHit = true; break; } // ANY short-circuit
                }
            }

            return isAll ? true : anyHit;
        }

        // -------- robust resolver: dotted path, relative, bubble-up, root --------
        static SerializedProperty ResolveCondition(SerializedProperty context, string nameOrPath)
        {
            if (string.IsNullOrEmpty(nameOrPath)) return null;

            // 0) dotted or absolute path as-is
            var p = context.serializedObject.FindProperty(nameOrPath);
            if (p != null) return p;

            // 1) basePath.name
            string basePath = GetBase(context.propertyPath);
            if (!string.IsNullOrEmpty(basePath))
            {
                p = context.serializedObject.FindProperty($"{basePath}.{nameOrPath}");
                if (p != null) return p;
            }

            // 2) bubble up owners
            string path = basePath;
            while (!string.IsNullOrEmpty(path))
            {
                int lastDot = path.LastIndexOf('.');
                if (lastDot <= 0) { path = string.Empty; break; }
                path = path[..lastDot];
                p = context.serializedObject.FindProperty($"{path}.{nameOrPath}");
                if (p != null) return p;
            }

            // 3) root fallback
            return context.serializedObject.FindProperty(nameOrPath);
        }

        static string GetBase(string propPath)
        {
            int i = propPath.LastIndexOf('.');
            return i > 0 ? propPath[..i] : string.Empty;
        }

        // -------- tolerant readers (bool/enum/int/objectref/string) --------
        static bool ReadBool(SerializedProperty p) =>
            p.propertyType switch
            {
                SerializedPropertyType.Boolean => p.boolValue,
                SerializedPropertyType.Enum => p.enumValueIndex != 0,
                SerializedPropertyType.Integer => p.intValue != 0,
                SerializedPropertyType.ObjectReference => p.objectReferenceValue != null,
                SerializedPropertyType.String => !string.IsNullOrEmpty(p.stringValue),
                _ => ReadInt(p) != 0
            };

        static int ReadInt(SerializedProperty p) =>
            p.propertyType switch
            {
                SerializedPropertyType.Enum => p.enumValueIndex,
                SerializedPropertyType.Integer => p.intValue,
                SerializedPropertyType.Boolean => p.boolValue ? 1 : 0,
                SerializedPropertyType.Character => p.intValue,
                SerializedPropertyType.ObjectReference => p.objectReferenceValue ? 1 : 0,
                SerializedPropertyType.String => string.IsNullOrEmpty(p.stringValue) ? int.MinValue : p.stringValue.GetHashCode(),
                _ => 0
            };

        static int ToInt(object o) =>
            o switch
            {
                null => int.MinValue,
                Enum e => Convert.ToInt32(e),
                bool b => b ? 1 : 0,
                sbyte or byte or short or ushort or int => Convert.ToInt32(o),
                long l => unchecked((int)l),
                string s => string.IsNullOrEmpty(s) ? int.MinValue : s.GetHashCode(),
                _ => o.GetHashCode()
            };
    }
#endif
    #endregion
}
