#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using Solo.MOST_IN_ONE;

[CanEditMultipleObjects]
[CustomEditor(typeof(MonoBehaviour), true)]
public sealed class SmartInspector_MB : SmartInspector_HideIfAA_AndScript { }

[CanEditMultipleObjects]
#if UNITY_2020_1_OR_NEWER
[CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
#else
[CustomEditor(typeof(ScriptableObject), true)]
#endif
public sealed class SmartInspector_SO : SmartInspector_HideIfAA_AndScript { }

public abstract class SmartInspector_HideIfAA_AndScript : Editor
{
    static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // if true → hide the Script row when ANY selected object has [HideScriptField]
    // if false → require ALL selected to have it
    const bool HIDE_SCRIPT_IF_ANY_SELECTED = false;

    public override void OnInspectorGUI()
    {
        var inspectedType = targets[0].GetType();

        bool usesHideIf = TypeHasHideIfAA(inspectedType);
        bool hideScript = ShouldHideScriptForSelection(targets);

        if (!usesHideIf && !hideScript)
        {
            base.OnInspectorGUI();
            return;
        }

        serializedObject.Update();

        var it = serializedObject.GetIterator();
        bool enterChildren = true;

        while (it.NextVisible(enterChildren))
        {
            enterChildren = false;

            // 1) Hide the Script row when requested
            if (hideScript && it.propertyPath == "m_Script")
                continue;

            // 2) Skip fields hidden by HideIfAny/HideIfAll (now includes MOSTEdit.RuntimeOnly support)
            if (usesHideIf && FieldHiddenByHideIfAA(it))
                continue;

            // 3) If a list/array and ALL children would be hidden → hide the foldout/header as well
            if (usesHideIf && IsArrayButNotString(it) && AllArrayChildrenHidden(it))
                continue;

            EditorGUILayout.PropertyField(it, includeChildren: true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ---------- [HideScriptField] ----------
    static bool ShouldHideScriptForSelection(UnityEngine.Object[] objs)
    {
        if (objs == null || objs.Length == 0) return false;

        bool HasAttr(Type x) =>
            Attribute.IsDefined(x, typeof(HideScriptFieldAttribute), true) ||
            x.GetCustomAttributes(true).Any(a => a.GetType().Name == "HideScriptFeildAttribute"); // legacy typo tolerance

        return HIDE_SCRIPT_IF_ANY_SELECTED
            ? objs.Any(o => HasAttr(o.GetType()))
            : objs.All(o => HasAttr(o.GetType()));
    }

    // ---------- Type scan to decide if we need HideIf processing ----------
    static bool TypeHasHideIfAA(Type t)
    {
        var fields = t.GetFields(BF).Where(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null);

        if (fields.Any(f =>
            f.GetCustomAttributes(typeof(HideIfAnyAttribute), true).Length > 0 ||
            f.GetCustomAttributes(typeof(HideIfAllAttribute), true).Length > 0))
            return true;

        foreach (var f in fields)
        {
            var ft = f.FieldType;
            if (IsList(ft)) ft = GetElementType(ft);
            if (ft == null) continue;

            var nested = ft.GetFields(BF).Where(ff => ff.IsPublic || ff.GetCustomAttribute<SerializeField>() != null);
            if (nested.Any(ff =>
                ff.GetCustomAttributes(typeof(HideIfAnyAttribute), true).Length > 0 ||
                ff.GetCustomAttributes(typeof(HideIfAllAttribute), true).Length > 0))
                return true;
        }
        return false;
    }

    // ---------- Per-property evaluation (now honors MOSTEdit.RuntimeOnly) ----------
    static bool FieldHiddenByHideIfAA(SerializedProperty prop)
    {
        var fi = GetFieldInfoFromProperty(prop, out _);
        if (fi == null) return false;

        var any = (HideIfAnyAttribute)fi.GetCustomAttributes(typeof(HideIfAnyAttribute), true).FirstOrDefault();
        var all = (HideIfAllAttribute)fi.GetCustomAttributes(typeof(HideIfAllAttribute), true).FirstOrDefault();

        // MOSTEdit.RuntimeOnly: hide during Play mode even with NO triplets
        if (IsRuntimeOnly(any) || IsRuntimeOnly(all))
            return true;

        // If triplets exist, keep your ALL-first, then ANY logic
        if (all != null && all.Triplets != null && all.Triplets.Length > 0 && EvaluateTriplets(prop, all.Triplets, true))
            return true;

        if (any != null && any.Triplets != null && any.Triplets.Length > 0 && EvaluateTriplets(prop, any.Triplets, false))
            return true;

        return false;

        // local helper
        static bool IsRuntimeOnly(object attr)
        {
#if UNITY_EDITOR
            if (attr is HideIfAnyAttribute a && a.EditState == MOSTEdit.RuntimeOnly && !EditorApplication.isPlaying)
                return true;
            if (attr is HideIfAllAttribute b && b.EditState == MOSTEdit.RuntimeOnly && !EditorApplication.isPlaying)
                return true;
#endif
            return false;
        }
    }

    // (field, value, hideWhen) * N
    static bool EvaluateTriplets(SerializedProperty ctx, object[] triplets, bool requireAll)
    {
        if (triplets == null || triplets.Length == 0) return false;
        if (triplets.Length % 3 != 0)
        {
            Debug.LogError("[HideIfAny/All] Expected triplets: (field, value, hideWhen)*N");
            return false;
        }

        bool anyHit = false;

        for (int i = 0; i < triplets.Length; i += 3)
        {
            string name = triplets[i] as string;
            object compare = triplets[i + 1];
            bool hideWhen = triplets[i + 2] is bool b && b;
            if (string.IsNullOrEmpty(name)) continue;

            var cond = Resolve(ctx, name);
            if (cond == null)
            {
                if (requireAll) return false; // ALL cannot be satisfied if a rule can't be evaluated
                continue;                     // ANY skips unknown rule
            }

            bool hit = (compare is bool)
                ? (ReadBool(cond) == hideWhen)
                : (((ReadInt(cond) == ToInt(compare)) == hideWhen));

            if (requireAll) { if (!hit) return false; }
            else { if (hit) { anyHit = true; break; } }
        }

        return requireAll ? true : anyHit;
    }

    // ---------- arrays/lists helpers ----------
    static bool IsArrayButNotString(SerializedProperty p) => p.isArray && p.propertyType != SerializedPropertyType.String;

    static bool AllArrayChildrenHidden(SerializedProperty arrayProp)
    {
        var copy = arrayProp.Copy();
        int depth = copy.depth;

        if (!copy.NextVisible(true)) return false; // can't enumerate reliably → keep header
        if (copy.depth <= depth) return true;      // empty array

        do
        {
            if (copy.depth <= depth) break;
            if (FieldOrDescendantVisible(copy)) return false;
        } while (copy.NextVisible(false));

        return true;
    }

    static bool FieldOrDescendantVisible(SerializedProperty element)
    {
        if (!FieldHiddenByHideIfAA(element))
            return true;

        var copy = element.Copy();
        int parentDepth = element.depth;

        if (!copy.NextVisible(true)) return false;

        do
        {
            if (copy.depth <= parentDepth) break;
            if (!FieldHiddenByHideIfAA(copy))
                return true;
        } while (copy.NextVisible(false));

        return false;
    }

    // ---------- resolve & read ----------
    static SerializedProperty Resolve(SerializedProperty ctx, string nameOrPath)
    {
        if (string.IsNullOrEmpty(nameOrPath)) return null;
        var so = ctx.serializedObject;

        var p = so.FindProperty(nameOrPath);
        if (p != null) return p;

        string basePath = GetBase(ctx.propertyPath);
        if (!string.IsNullOrEmpty(basePath))
        {
            p = so.FindProperty($"{basePath}.{nameOrPath}");
            if (p != null) return p;
        }

        string path = basePath;
        while (!string.IsNullOrEmpty(path))
        {
            int lastDot = path.LastIndexOf('.');
            if (lastDot <= 0) { path = string.Empty; break; }
            path = path[..lastDot];
            p = so.FindProperty($"{path}.{nameOrPath}");
            if (p != null) return p;
        }

        return so.FindProperty(nameOrPath);
    }

    static string GetBase(string propPath)
    {
        int i = propPath.LastIndexOf('.');
        return i > 0 ? propPath[..i] : string.Empty;
    }

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

    static FieldInfo GetFieldInfoFromProperty(SerializedProperty prop, out Type fieldType)
    {
        fieldType = null;
        var rootType = prop.serializedObject.targetObject.GetType();
        string path = prop.propertyPath.Replace(".Array.data[", "[");
        var segments = path.Split('.');
        FieldInfo fi = null;
        Type parentType = rootType;

        foreach (var seg in segments)
        {
            if (seg.Contains("["))
            {
                string name = seg[..seg.IndexOf('[')];
                fi = parentType.GetField(name, BF);
                if (fi == null) return null;
                parentType = GetElementType(fi.FieldType);
            }
            else
            {
                fi = parentType.GetField(seg, BF);
                if (fi == null) return null;
                parentType = fi.FieldType;
            }
        }
        fieldType = parentType;
        return fi;
    }

    static bool IsList(Type t) =>
        t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>));

    static Type GetElementType(Type t)
    {
        if (t.IsArray) return t.GetElementType();
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            return t.GetGenericArguments()[0];
        return t;
    }
}
#endif
