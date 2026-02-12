using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Solo.MOST_IN_ONE
{
    [CustomEditor(typeof(MOST_Action))]
    public class MOST_ActionEditor : Editor
    {
        SerializedProperty effectsProp;
        MOST_Action effectsTarget;
        List<bool> effectFoldouts = new();

        static readonly Dictionary<int, bool> _persistentFoldoutStates = new();

        void OnEnable()
        {
            effectsTarget = target as MOST_Action;
            if (effectsTarget == null) return;

            effectsProp = serializedObject.FindProperty("actions");
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            RebuildFoldoutStates();
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        void OnUndoRedoPerformed()
        {
            RebuildFoldoutStates();
            Repaint();
        }

        void RebuildFoldoutStates()
        {
            var newFoldouts = new List<bool>();
            for (int i = 0; i < effectsTarget.Actions.Count; i++)
            {
                if (i < effectFoldouts.Count)
                    newFoldouts.Add(effectFoldouts[i]);
                else
                    newFoldouts.Add(GetPersistentFoldoutState(i));
            }
            effectFoldouts = newFoldouts;
        }

        bool GetPersistentFoldoutState(int index)
        {
            string key = $"{effectsTarget.GetInstanceID()}_{index}";
            if (!_persistentFoldoutStates.ContainsKey(key.GetHashCode()))
                _persistentFoldoutStates[key.GetHashCode()] = false;
            return _persistentFoldoutStates[key.GetHashCode()];
        }

        void SetPersistentFoldoutState(int index, bool state)
        {
            string key = $"{effectsTarget.GetInstanceID()}_{index}";
            _persistentFoldoutStates[key.GetHashCode()] = state;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            SerializedProperty prop = serializedObject.GetIterator();
            bool enterMainChildren = true;

            while (prop.NextVisible(enterMainChildren))
            {
                enterMainChildren = false;

                if (prop.name == "actions" || prop.name == "m_Script")continue;

                EditorGUILayout.PropertyField(prop, true);
            }
            EditorGUILayout.Space(5);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
            EditorGUILayout.Space(5);

            while (effectFoldouts.Count < effectsTarget.Actions.Count)
            {
                effectFoldouts.Add(GetPersistentFoldoutState(effectFoldouts.Count));
            }
            while (effectFoldouts.Count > effectsTarget.Actions.Count)
            {
                effectFoldouts.RemoveAt(effectFoldouts.Count - 1);
            }

            if (effectsTarget == null)
            {
                EditorGUILayout.HelpBox("Most_Action not available.", MessageType.Error);
                return;
            }

            for (int i = 0; i < effectsProp.arraySize; i++)
            {
                var element = effectsProp.GetArrayElementAtIndex(i);
                var effectObj = effectsTarget.Actions[i];
                if (effectObj == null) continue;

                Rect headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

                // Header row
                EditorGUI.DrawRect(headerRect, new Color(0.17f, 0.17f, 0.17f));

                // ▶/▼ (clickable for foldout)
                Rect dropdownRect = new(headerRect.x + 4, headerRect.y, 18, headerRect.height);
                if (GUI.Button(dropdownRect, effectFoldouts[i] ? "▼" : "▶", EditorStyles.label))
                {
                    effectFoldouts[i] = !effectFoldouts[i];
                    SetPersistentFoldoutState(i, effectFoldouts[i]);
                }

                // Enable toggle
                Rect toggleRect = new(dropdownRect.xMax + 4, headerRect.y, 18, headerRect.height);
                effectObj.Enabled = EditorGUI.Toggle(toggleRect, effectObj.Enabled);

                // Name label (clickable for foldout)
                Rect labelRect = new(toggleRect.xMax + 4, headerRect.y, headerRect.width - 120, headerRect.height);
                if (GUI.Button(labelRect, effectObj.ActionName, EditorStyles.boldLabel))
                {
                    effectFoldouts[i] = !effectFoldouts[i];
                    SetPersistentFoldoutState(i, effectFoldouts[i]);
                }

                // Move ↑/↓
                float buttonWidth = 20;
                if (GUI.Button(new Rect(headerRect.xMax - (buttonWidth * 3), headerRect.y, buttonWidth, headerRect.height), "↑"))
                    MoveEffect(i, i - 1);
                if (GUI.Button(new Rect(headerRect.xMax - (buttonWidth * 2), headerRect.y, buttonWidth, headerRect.height), "↓"))
                    MoveEffect(i, i + 1);

                // ⋮ Remove
                Rect menuRect = new(headerRect.xMax - buttonWidth, headerRect.y, buttonWidth, headerRect.height);
                GUIStyle dotStyle = new(EditorStyles.miniButton)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                if (GUI.Button(menuRect, "⋮", dotStyle))
                {
                    int indexToRemove = i;
                    GenericMenu menu = new();
                    menu.AddItem(new GUIContent("Remove"), false, () =>
                    {
                        if (indexToRemove >= 0 && indexToRemove < effectsTarget.Actions.Count)
                        {
                            Undo.RecordObject(effectsTarget, "Remove Effect");
                            effectsTarget.RemoveEffectAt(indexToRemove);
                            EditorUtility.SetDirty(effectsTarget);
                            RebuildFoldoutStates();
                        }
                    });
                    menu.ShowAsContext();
                }

                if (effectFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.HelpBox("Type: " + effectsTarget.Actions[i].GetType().ToString().Remove(0,17), MessageType.None);

                    var iterator = element.Copy();
                    var endProperty = iterator.GetEndProperty();
                    bool enterChildren = true;

                    while (iterator.NextVisible(enterChildren))
                    {
                        if (SerializedProperty.EqualContents(iterator, endProperty))
                            break;

                        EditorGUILayout.PropertyField(iterator, true);

                        enterChildren = false;
                    }

                    if (Application.isPlaying)
                    {
                        EditorGUILayout.Space(2);
                        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(.32f,.32f,.32f));
                        EditorGUILayout.Space(2);

                        EditorGUILayout.HelpBox("Is Playing/Active is a debug for MOST_Action.IsPlaying\nLogically in case of instant or moment actions will always be equal false", MessageType.Info);
                        GUI.enabled = false;
                        EditorGUILayout.Toggle("Is Playing/Active", effectsTarget.Actions[i].IsPlaying);
                        GUI.enabled = true;
                        EditorGUILayout.Space(5);
                        EditorGUILayout.BeginHorizontal();
                        {
                            if (GUILayout.Button("Play", GUILayout.Height(20)))
                            {
                                effectsTarget.PlayAction(effectsTarget.Actions[i].ActionName);
                            }

                            if (GUILayout.Button("Stop", GUILayout.Height(20)))
                            {
                                effectsTarget.StopAction(effectsTarget.Actions[i].ActionName);
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space(5);
                EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.black);
                EditorGUILayout.Space(2);
            }

            if (effectsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No Actions Added, Tap To Add.", MessageType.Warning);
            }
            // Add Effect Button
            if (GUILayout.Button("Add Effect", GUILayout.Height(20)))
            {
                GenericMenu menu = new();
                DrawType(menu, typeof(MOSTAction_HDRGlow), "HDR Glow");
                DrawType(menu, typeof(MOSTAction_GrayShift), "Gray Shift");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_CameraShake), "Camera Shake");
                DrawType(menu, typeof(MOSTAction_CameraFollow), "Camera Follow");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_AudioSource), "Audio Source");
                DrawType(menu, typeof(MOSTAction_HapticFeedback), "Haptic Feedback");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_Spawner), "Spawner");
                DrawType(menu, typeof(MOSTAction_Burst), "Burst");
                DrawType(menu, typeof(MOSTAction_Swap), "Object Swap");
                DrawType(menu, typeof(MOSTAction_RandomSelect), "Random Select");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_PositionAnimation), "Position Animation");
                DrawType(menu, typeof(MOSTAction_RotationAnimation), "Rotate Animation");
                DrawType(menu, typeof(MOSTAction_ScaleAnimation), "Scale Animation");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_Events), "Events");
                DrawType(menu, typeof(MOSTAction_Collision_Trigger), "Collision And Trigger");
                DrawType(menu, typeof(MOSTAction_Destroy), "Destroy");
                menu.AddSeparator("");
                DrawType(menu, typeof(MOSTAction_DisplayData), "Display Data");
                DrawType(menu, typeof(MOSTAction_UpdateData), "Update Data");

                menu.ShowAsContext();
            }

            serializedObject.ApplyModifiedProperties();
        }

        void MoveEffect(int fromIndex, int toIndex)
        {
            if (toIndex < 0 || toIndex >= effectsProp.arraySize) return;

            effectsProp.MoveArrayElement(fromIndex, toIndex);
            var tmp = effectFoldouts[fromIndex];
            effectFoldouts.RemoveAt(fromIndex);
            effectFoldouts.Insert(toIndex, tmp);

            SetPersistentFoldoutState(fromIndex, effectFoldouts[fromIndex]);
            SetPersistentFoldoutState(toIndex, effectFoldouts[toIndex]);
        }

        void DrawType(GenericMenu menu, Type type, string name)
        {
            menu.AddItem(new GUIContent(name), false, () =>
            {
                var newEffect = (MOST_ActionCore)Activator.CreateInstance(type);
                Undo.RecordObject(effectsTarget, "Add Effect");
                effectsTarget.AddEffect(newEffect);
                EditorUtility.SetDirty(effectsTarget);
                effectFoldouts.Add(true);
                SetPersistentFoldoutState(effectFoldouts.Count - 1, true);
            });
        }
    }
}