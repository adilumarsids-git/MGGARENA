#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Solo.MOST_IN_ONE
{
    public class RunnerLevelGeneratorWindow : EditorWindow
    {
        [MenuItem("Tools/Runner Level Generator")]
        public static void Open()
        {
            var wnd = GetWindow<RunnerLevelGeneratorWindow>(utility: false, title: "Runner Level Generator");
            wnd.minSize = new Vector2(520, 640);
            wnd.Show();
        }

        [Serializable]
        public class LayoutConfig
        {
            public Vector3 StartWorldPosition;
            public GameObject RoadPrefab;
            [Min(1)] public int RepeatTimes = 10;
            public Vector3 OffsetBetweenParts = new(0, 0, 10);
        }

        [Serializable]
        public class EndgameConfig
        {
            public GameObject EndlinePrefab;
            public Vector3 OffsetFromStart = new(0, 0, 120);
            public bool HaveBonus;
        }

        [Serializable]
        public class BonusRepeatConfig
        {
            [Header("Gate Mode")]
            public bool IsGate;

            [Header("Legacy TMP Text (xN)")]
            [Tooltip("Starting value for text (e.g., x1.0)")]
            public float TextStartValue = 1f;
            [Tooltip("Increment per spawn (e.g., 0.2 -> x1.0, x1.2, ...)")]
            public float TextJumpPerStep = 0.2f;

            [Header("GateText Progression")]
            [Tooltip("GateText starts at this value")]
            public float GateStartValue = 1f;
            [Tooltip("GateText increment per spawn")]
            public float GateJumpPerStep = 0.2f;

            [Header("OutputText Progression")]
            [Tooltip("OutputText starts at this value")]
            public float OutputStartValue = 1f;
            [Tooltip("OutputText increment per spawn")]
            public float OutputJumpPerStep = 0.2f;

            [Header("Signs")]
            [Tooltip("Sign to prepend for GateText (e.g., x or +)")]
            public string GateSign = "x";
            [Tooltip("Sign to prepend for OutputText (e.g., x or +)")]
            public string OutputSign = "x";

            [Header("Prefabs")]
            public GameObject RepeatedPrefab;

            [Header("Repeat Settings")]
            [Min(1)] public int Amount = 5;

            [Header("Position Settings")]
            public Vector3 Offset = new(0, 0, 5);
            public Vector3 OffsetFromEndpoint;

            [Header("Target Score Text (Legacy)")]
            public string[] TextNameInStepChilds = Array.Empty<string>();

            [Header("Colored Object Loop (Legacy)")]
            public string TargetColorMaterialLerpName;
            public List<Material> MaterialsLerp = new();
        }

        [Serializable]
        public class ObjectsConfig
        {
            [Header("Global Spawning Window")]
            public float OffsetFromStartZ = 5f;
            public float OffsetBeforeEndlineZ = 5f;
            public float StepZ = 5f;
            public float StepY = 0f;

            [Header("Road/Side Layout")]
            public float RoadWidth = 9f;

            [Serializable]
            public class Part
            {
                public string Name;
                public GameObject Prefab;
                [Range(1, 3)] public int Separation = 1;
                [Tooltip("Extra delta added to global Step Z when this part is selected (can be negative)")]
                public float ExtraStepZ = 0f;
                [Min(0f)] public float Chance = 1f;
                public Vector3 SpawnOffset;
                [Min(0), Tooltip("Ignore this part for the first N spawn lines (rows along Z). Example: 2 => ignored for the first and second lines.")]
                public int IgnoreFirstLines = 0;

                [Header("Gate (optional)")]
                public bool IsGate;

                [Serializable]
                public class GatePreset
                {
                    [Tooltip("Sign to prepend (e.g., x or +)")]
                    public string Sign = "x";

                    [Tooltip("Inclusive min of the stepped range")]
                    public float RangeMin = 1f;

                    [Tooltip("Inclusive max of the stepped range")]
                    public float RangeMax = 2f;

                    [Tooltip("Step size (> 0) for the stepped grid")]
                    public float Step = 1f;

                    [Tooltip("Exclude zero from the candidate values")]
                    public bool NoZero = false;
                }

                [Header("Gate Presets")]
                public List<GatePreset> GatePresets = new();

                [Tooltip("If enabled, show Output properties and write MOST_Gate.OutputText")]
                public bool UseOutput = true;

                [Header("Output (optional)")]
                [Tooltip("Sign to prepend for OutputText (e.g., x or +)")]
                public string OutputSign = "x";
                [Tooltip("Inclusive min for Output e.g. 10 -> x10")]
                public float OutputRangeMin = 1f;
                [Tooltip("Inclusive max for Output e.g. 20 -> x20")]
                public float OutputRangeMax = 2f;
                [Tooltip("Step size for Output value (> 0)")]
                public float OutputStep = 1f;
                [Tooltip("Exclude zero from Output candidates")]
                public bool OutputNoZero = false;
            }
            public List<Part> Parts = new();
        }

        [Serializable]
        public class OutputConfig
        {
            public bool SaveAsPrefab = true;
            public string FolderPath = "Assets/Levels";
            public string BaseLevelName = "Level";
        }

        const string LastProfileGuidKey = "RunnerLevelGenerator.LastProfileGuid";

        [SerializeField] RunnerLevelProfile currentProfile = null;
        [SerializeField] LayoutConfig layout = new();
        [SerializeField] EndgameConfig endgame = new();
        [SerializeField] BonusRepeatConfig bonus = new();
        [SerializeField] ObjectsConfig objectsCfg = new();
        [SerializeField] OutputConfig output = new();

        [SerializeField] bool showLayout = true;
        [SerializeField] bool showEndgame = true;
        [SerializeField] bool showBonus = true;
        [SerializeField] bool showObjects = true;

        [SerializeField] Vector2 _scroll;
        [SerializeField] bool foldBonusTextList = true;
        [SerializeField] bool foldBonusMaterials = true;
        [SerializeField] bool foldPartsList = true;
        [SerializeField] List<bool> partFoldouts = new();

        void OnEnable()
        {
            var guid = EditorPrefs.GetString(LastProfileGuidKey, string.Empty);
            if (!string.IsNullOrEmpty(guid))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    currentProfile = AssetDatabase.LoadAssetAtPath<RunnerLevelProfile>(path);
                    if (currentProfile) LoadFromAsset(currentProfile);
                }
            }
        }

        void OnGUI()
        {
            DrawHeader();
            DrawProfilesUI();

            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
            GUILayout.Space(2);
            using (new EditorGUI.IndentLevelScope())
            {
                output.SaveAsPrefab = EditorGUILayout.ToggleLeft(new GUIContent("Save As Prefab"), output.SaveAsPrefab);
                using (new EditorGUILayout.HorizontalScope())
                {
                    output.FolderPath = EditorGUILayout.TextField(new GUIContent("Save Folder"), output.FolderPath);
                    if (GUILayout.Button("Choose…", GUILayout.Width(80)))
                    {
                        var chosen = EditorUtility.OpenFolderPanel("Choose Save Folder", Application.dataPath, "Levels");
                        if (!string.IsNullOrEmpty(chosen))
                            output.FolderPath = ToUnityRelativePath(chosen);
                    }
                }

                output.BaseLevelName = EditorGUILayout.TextField(new GUIContent("Base Name"), output.BaseLevelName);
                var preview = ComputeNextAutoName(output.FolderPath, output.BaseLevelName);
                EditorGUILayout.HelpBox($"Auto name preview: {preview}", MessageType.Info);
            }
            GUILayout.Space(4);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 3), new Color(.13f, .13f, .13f, 1));

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            GUILayout.Space(6);
            showLayout = DrawFoldoutHeader("Layout", showLayout);
            if (showLayout)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    layout.StartWorldPosition = EditorGUILayout.Vector3Field(new GUIContent("Start Position"), layout.StartWorldPosition);
                    layout.RoadPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Road Prefab"), layout.RoadPrefab, typeof(GameObject), false);
                    layout.RepeatTimes = EditorGUILayout.IntSlider(new GUIContent("Repeat Times"), Mathf.Max(1, layout.RepeatTimes), 1, 500);
                    layout.OffsetBetweenParts = EditorGUILayout.Vector3Field(new GUIContent("Offset Between Parts"), layout.OffsetBetweenParts);
                }
            }

            GUILayout.Space(4);
            showEndgame = DrawFoldoutHeader("Endgame", showEndgame);
            if (showEndgame)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    endgame.EndlinePrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Endline Prefab"), endgame.EndlinePrefab, typeof(GameObject), false);
                    endgame.OffsetFromStart = EditorGUILayout.Vector3Field(new GUIContent("Offset From Start"), endgame.OffsetFromStart);
                    endgame.HaveBonus = EditorGUILayout.ToggleLeft(new GUIContent("Have Bonus"), endgame.HaveBonus);
                }
            }

            if (endgame.HaveBonus)
            {
                GUILayout.Space(4);
                showBonus = DrawFoldoutHeader("Bonus Repeater (when 'Have Bonus' is enabled)", showBonus);
                if (showBonus)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        bonus.IsGate = EditorGUILayout.ToggleLeft(new GUIContent("Is a Gate", "Use MOST_Gate and apply signs with random values"), bonus.IsGate);

                        GUILayout.Space(2);
                        bonus.RepeatedPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Repeated Prefab"), bonus.RepeatedPrefab, typeof(GameObject), false);
                        bonus.Amount = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Amount"), bonus.Amount));

                        bonus.Offset = EditorGUILayout.Vector3Field(new GUIContent("Offset per Step"), bonus.Offset);
                        bonus.OffsetFromEndpoint = EditorGUILayout.Vector3Field(new GUIContent("Offset From Endpoint"), bonus.OffsetFromEndpoint);

                        if (bonus.IsGate)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                bonus.GateSign = EditorGUILayout.TextField(new GUIContent("Gate Sign"), string.IsNullOrEmpty(bonus.GateSign) ? "x" : bonus.GateSign);
                                bonus.OutputSign = EditorGUILayout.TextField(new GUIContent("Output Sign"), string.IsNullOrEmpty(bonus.OutputSign) ? "x" : bonus.OutputSign);
                            }
                            EditorGUILayout.LabelField("GateText Progression", EditorStyles.boldLabel);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                bonus.GateStartValue = EditorGUILayout.FloatField(new GUIContent("Start"), bonus.GateStartValue);
                                bonus.GateJumpPerStep = EditorGUILayout.FloatField(new GUIContent("Jump"), bonus.GateJumpPerStep);
                            }
                            EditorGUILayout.LabelField("OutputText Progression", EditorStyles.boldLabel);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                bonus.OutputStartValue = EditorGUILayout.FloatField(new GUIContent("Start"), bonus.OutputStartValue);
                                bonus.OutputJumpPerStep = EditorGUILayout.FloatField(new GUIContent("Jump"), bonus.OutputJumpPerStep);
                            }
                        }
                        else
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                bonus.TextStartValue = EditorGUILayout.FloatField(new GUIContent("Start"), bonus.TextStartValue);
                                bonus.TextJumpPerStep = EditorGUILayout.FloatField(new GUIContent("Jump"), bonus.TextJumpPerStep);
                            }

                            foldBonusTextList = DrawListHeader("Text Names in Step Children", foldBonusTextList);
                            if (foldBonusTextList)
                                DrawStringArray(ref bonus.TextNameInStepChilds, "Text Names in Step Children");

                            foldBonusMaterials = DrawListHeader("Materials Lerp", foldBonusMaterials);
                            if (foldBonusMaterials)
                            {
                                bonus.TargetColorMaterialLerpName = EditorGUILayout.TextField(new GUIContent("Target Color Child Path"), bonus.TargetColorMaterialLerpName);
                                DrawMaterialList(ref bonus.MaterialsLerp, "Materials Lerp");
                            }
                        }
                    }
                }
            }

            GUILayout.Space(4);
            showObjects = DrawFoldoutHeader("Level Objects (Parts)", showObjects);
            if (showObjects)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        objectsCfg.OffsetFromStartZ = EditorGUILayout.FloatField(new GUIContent("Offset From Start (Z)"), objectsCfg.OffsetFromStartZ);
                        objectsCfg.OffsetBeforeEndlineZ = EditorGUILayout.FloatField(new GUIContent("Offset Before Endline (Z)"), objectsCfg.OffsetBeforeEndlineZ);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        objectsCfg.StepZ = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Step Z"), objectsCfg.StepZ));
                        objectsCfg.StepY = EditorGUILayout.FloatField(new GUIContent("Step Y"), objectsCfg.StepY);
                    }

                    GUILayout.Space(2);
                    objectsCfg.RoadWidth = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Road Width"), objectsCfg.RoadWidth));

                    GUILayout.Space(2);
                    foldPartsList = DrawListHeader($"Parts ({objectsCfg.Parts?.Count ?? 0})", foldPartsList);
                    if (foldPartsList)
                    {
                        int size = Mathf.Max(0, EditorGUILayout.IntField("Count", objectsCfg.Parts?.Count ?? 0));
                        objectsCfg.Parts ??= new List<ObjectsConfig.Part>(size);
                        while (objectsCfg.Parts.Count < size) objectsCfg.Parts.Add(new ObjectsConfig.Part());
                        while (objectsCfg.Parts.Count > size) objectsCfg.Parts.RemoveAt(objectsCfg.Parts.Count - 1);

                        while (partFoldouts.Count < objectsCfg.Parts.Count) partFoldouts.Add(true);
                        while (partFoldouts.Count > objectsCfg.Parts.Count) partFoldouts.RemoveAt(partFoldouts.Count - 1);

                        for (int i = 0; i < objectsCfg.Parts.Count; i++)
                        {
                            var p = objectsCfg.Parts[i];
                            EditorGUILayout.BeginVertical(GUI.skin.box);
                            EditorGUILayout.BeginHorizontal();

                            string label = string.IsNullOrEmpty(p.Name) ? $"Part #{i}" : $"Part #{i}: {p.Name}";

                            var oldCol = GUI.contentColor;
                            GUI.contentColor = new Color(1f, .7f, 0f);
                            partFoldouts[i] = EditorGUILayout.Foldout(partFoldouts[i], label, true);
                            GUI.contentColor = oldCol;

                            GUILayout.FlexibleSpace();
                            var oldBg = GUI.backgroundColor;
                            GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
                            if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                            {
                                objectsCfg.Parts.RemoveAt(i);
                                partFoldouts.RemoveAt(i);
                                EditorGUILayout.EndHorizontal();
                                EditorGUILayout.EndVertical();
                                GUILayout.Space(2);
                                i--;
                                continue;
                            }
                            GUI.backgroundColor = oldBg;

                            EditorGUILayout.EndHorizontal();
                            if (partFoldouts[i])
                                DrawPart(p);
                            EditorGUILayout.EndVertical();
                            GUILayout.Space(2);
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(8);
            using (new EditorGUI.DisabledScope(!CanGenerate()))
            {
                var rect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
                if (GUI.Button(rect, "Generate Level"))
                    Generate();
            }
            GUILayout.Space(8);
        }

        void DrawPart(ObjectsConfig.Part p)
        {
            p.Name = EditorGUILayout.TextField("Name", p.Name);
            p.Prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", p.Prefab, typeof(GameObject), false);
            p.Separation = EditorGUILayout.IntSlider(new GUIContent("Road Separation"), Mathf.Clamp(p.Separation, 1, 3), 1, 3);
            using (new EditorGUILayout.HorizontalScope())
            {
                p.ExtraStepZ = EditorGUILayout.FloatField(new GUIContent("Extra Step Z"), p.ExtraStepZ);
                p.Chance = Mathf.Max(0f, EditorGUILayout.FloatField("Chance", p.Chance));
            }
            p.SpawnOffset = EditorGUILayout.Vector3Field("Spawn Offset", p.SpawnOffset);

            p.IgnoreFirstLines = Mathf.Max(0,
    EditorGUILayout.IntField(
        new GUIContent("Ignore First Lines", "If > 0, this part will not be considered until this many spawn lines have been generated."),
        p.IgnoreFirstLines
    )
);
            GUILayout.Space(4);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
            GUILayout.Space(4);
            p.IsGate = EditorGUILayout.ToggleLeft(new GUIContent("Is a Gate (MOST_Gate)"), p.IsGate);
            if (p.IsGate)
            {
                int presetCount = Mathf.Max(1, EditorGUILayout.IntField("Presets Count", p.GatePresets?.Count ?? 1));
                p.GatePresets ??= new List<ObjectsConfig.Part.GatePreset>(presetCount);
                while (p.GatePresets.Count < presetCount) p.GatePresets.Add(new ObjectsConfig.Part.GatePreset());
                while (p.GatePresets.Count > presetCount) p.GatePresets.RemoveAt(p.GatePresets.Count - 1);

                for (int gi = 0; gi < p.GatePresets.Count; gi++)
                {
                    var g = p.GatePresets[gi];
                    EditorGUILayout.BeginVertical(GUI.skin.box);

                    g.Sign = EditorGUILayout.TextField(new GUIContent("Sign"), string.IsNullOrEmpty(g.Sign) ? "x" : g.Sign);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        g.RangeMin = EditorGUILayout.FloatField(new GUIContent("Min"), g.RangeMin);
                        g.RangeMax = EditorGUILayout.FloatField(new GUIContent("Max"), Mathf.Max(g.RangeMin, g.RangeMax));
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        g.Step = Mathf.Max(0.0001f, EditorGUILayout.FloatField(new GUIContent("Step"), g.Step));
                        g.NoZero = EditorGUILayout.ToggleLeft(new GUIContent("No Zero"), g.NoZero);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
                GUILayout.Space(4);
                EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
                GUILayout.Space(4);
                p.UseOutput = EditorGUILayout.ToggleLeft(new GUIContent("Update Output Text"), p.UseOutput);

                if (p.UseOutput)
                {
                    p.OutputSign = EditorGUILayout.TextField(new GUIContent("Output Sign"), string.IsNullOrEmpty(p.OutputSign) ? "x" : p.OutputSign);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        p.OutputRangeMin = EditorGUILayout.FloatField(new GUIContent("Out Min"), p.OutputRangeMin);
                        p.OutputRangeMax = EditorGUILayout.FloatField(new GUIContent("Out Max"), Mathf.Max(p.OutputRangeMin, p.OutputRangeMax));
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        p.OutputStep = Mathf.Max(0.0001f, EditorGUILayout.FloatField(new GUIContent("Out Step"), p.OutputStep));
                        p.OutputNoZero = EditorGUILayout.ToggleLeft(new GUIContent("Out No Zero"), p.OutputNoZero);
                    }
                }
            }
        }

        void DrawHeader()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            EditorGUILayout.HelpBox($"Active Scene: {sceneName}. The generator will place objects into this open scene.", MessageType.None);
        }

        static bool DrawFoldoutHeader(string label, bool state)
        {
            var rect = GUILayoutUtility.GetRect(20, 22, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.11f, 0.11f, 0.11f, 1) : new Color(0.90f, 0.90f, 0.90f, 1));
            var foldRect = new Rect(rect.x + 6, rect.y + 2, rect.width - 12, rect.height - 4);
            return EditorGUI.Foldout(foldRect, state, new GUIContent(label), true);
        }

        bool DrawListHeader(string title, bool state)
        {
            var rect = GUILayoutUtility.GetRect(20, 22, GUILayout.ExpandWidth(true));
            var bg = EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f, 1f) : new Color(0.92f, 0.96f, 1f, 1f);
            EditorGUI.DrawRect(rect, bg);

            var stripe = new Rect(rect.x, rect.y, 3, rect.height);
            var stripeCol = new Color(0.25f, 0.60f, 1f, 1f);
            EditorGUI.DrawRect(stripe, stripeCol);

            var toggleRect = new Rect(rect.x + 8, rect.y + 2, 16, rect.height - 4);
            var labelRect = new Rect(toggleRect.x + 18, rect.y + 2, rect.width - 28, rect.height - 4);
            state = EditorGUI.Foldout(toggleRect, state, GUIContent.none, true);
            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);
            return state;
        }

        bool CanGenerate()
        {
            // Only block on output path if we’re saving as prefab
            if (output.SaveAsPrefab && string.IsNullOrEmpty(output.FolderPath)) return false;
            return true;
        }

        static bool IsPrefab(GameObject go)
        {
            var path = AssetDatabase.GetAssetPath(go);
            return !string.IsNullOrEmpty(path);
        }

        void Generate()
        {
            string rootName = output.SaveAsPrefab ? ComputeNextAutoName(output.FolderPath, output.BaseLevelName) : $"{output.BaseLevelName}_Preview";
            var root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Level Root");

            Vector3 start = layout.StartWorldPosition;
            if (layout.RoadPrefab != null)
            {
                for (int i = 0; i < layout.RepeatTimes; i++)
                {
                    var pos = start + (layout.OffsetBetweenParts * i);
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(layout.RoadPrefab);
                    Undo.RegisterCreatedObjectUndo(inst, "Instantiate Road Segment");
                    inst.transform.SetParent(root.transform, true);
                    inst.transform.position = pos;
                    inst.name = $"Road_{i + 1:000}";
                }
            }

            GameObject endlineInstance = null;
            if (endgame.EndlinePrefab != null)
            {
                var endPos = start + endgame.OffsetFromStart;
                endlineInstance = (GameObject)PrefabUtility.InstantiatePrefab(endgame.EndlinePrefab);
                Undo.RegisterCreatedObjectUndo(endlineInstance, "Instantiate Endline");
                endlineInstance.transform.SetParent(root.transform, true);
                endlineInstance.transform.position = endPos;
                endlineInstance.name = "Endline";
            }

            if (endgame.HaveBonus && bonus.RepeatedPrefab != null) GenerateBonus(root.transform, start, endlineInstance);
            GenerateLevelObjects(root.transform, start, endlineInstance);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (output.SaveAsPrefab)
            {
                EnsureFolder(output.FolderPath);
                var prefabPath = GetUniquePrefabPath(output.FolderPath, output.BaseLevelName);
                var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);
                if (saved != null) { EditorGUIUtility.PingObject(saved); Debug.Log($"Saved level prefab at: {prefabPath}"); }
                else EditorUtility.DisplayDialog("Prefab Save Failed", "Could not save the generated level as a prefab.", "OK");
            }
        }

        static IEnumerable<Component> FindMostGates(Transform root)
        {
            var all = root.GetComponentsInChildren<Component>(includeInactive: true);
            foreach (var c in all)
            {
                if (c == null) continue;
                var t = c.GetType();
                if (t != null && t.Name == "MOST_Gate") yield return c;
            }
        }

        void GenerateBonus(Transform root, Vector3 start, GameObject endlineInstance)
        {
            if (bonus.RepeatedPrefab == null) return;
            var bonusRoot = new GameObject("Bonus");
            Undo.RegisterCreatedObjectUndo(bonusRoot, "Create Bonus Root");
            bonusRoot.transform.SetParent(root, true);

            Vector3 fallbackVec =
                (endgame.OffsetFromStart.sqrMagnitude > 1e-6f)
                    ? endgame.OffsetFromStart
                    : (layout.OffsetBetweenParts * Mathf.Max(1, layout.RepeatTimes));

            Vector3 endpoint = (endlineInstance != null)
                ? endlineInstance.transform.position
                : start + fallbackVec;

            Vector3 basePos = endpoint + bonus.OffsetFromEndpoint;
            Vector3 tmpPoint = basePos;

            float legacyVal = bonus.TextStartValue;
            float gateVal = bonus.GateStartValue;
            float outVal = bonus.OutputStartValue;

            for (int count = 0; count < Mathf.Max(1, bonus.Amount); count++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(bonus.RepeatedPrefab);
                if (inst == null) { tmpPoint += bonus.Offset; continue; }

                Undo.RegisterCreatedObjectUndo(inst, "Instantiate Bonus Step");
                inst.transform.SetParent(bonusRoot.transform, true);
                inst.transform.position = tmpPoint;
                inst.name = $"BonusStep_{count + 1:000}";

                if (bonus.IsGate)
                {
                    foreach (var gate in FindMostGates(inst.transform))
                    {
                        var so = new SerializedObject(gate);
                        var pG = so.FindProperty("GateText");
                        var pOut = so.FindProperty("OutputText");

                        string gTxt = ComposeSigned(bonus.GateSign, gateVal);
                        string oTxt = ComposeSigned(bonus.OutputSign, outVal);

                        if (pG != null) pG.stringValue = gTxt;
                        if (pOut != null) pOut.stringValue = oTxt;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    gateVal += bonus.GateJumpPerStep;
                    outVal += bonus.OutputJumpPerStep;
                }
                else
                {
                    foreach (var path in bonus.TextNameInStepChilds)
                    {
                        if (string.IsNullOrEmpty(path)) continue;
                        var t = inst.transform.Find(path);
                        if (t)
                        {
                            var tmp = t.GetComponent<TMP_Text>();
                            if (tmp) tmp.text = "x" + legacyVal.ToString("0.0");
                        }
                    }

                    legacyVal += bonus.TextJumpPerStep;
                    if (!string.IsNullOrEmpty(bonus.TargetColorMaterialLerpName) && bonus.MaterialsLerp != null && bonus.MaterialsLerp.Count > 0)
                    {
                        var rT = inst.transform.Find(bonus.TargetColorMaterialLerpName);
                        if (rT)
                        {
                            var rend = rT.GetComponent<Renderer>();
                            if (rend)
                            {
                                var mat = bonus.MaterialsLerp[count % bonus.MaterialsLerp.Count];
                                if (mat) rend.material = mat;
                            }
                        }
                    }
                }
                tmpPoint += bonus.Offset;
            }
        }

        static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        void GenerateLevelObjects(Transform root, Vector3 start, GameObject endlineInstance)
        {
            if (objectsCfg.Parts == null || objectsCfg.Parts.Count == 0) return;

            float startZ = start.z + objectsCfg.OffsetFromStartZ;

            float fallbackLenZ =
                (Mathf.Abs(endgame.OffsetFromStart.z) > 1e-6f)
                    ? endgame.OffsetFromStart.z
                    : (layout.OffsetBetweenParts.z * Mathf.Max(1, layout.RepeatTimes));

            float trackEndZ = (endlineInstance != null)
                ? endlineInstance.transform.position.z
                : (start.z + fallbackLenZ);

            float endZ = trackEndZ - objectsCfg.OffsetBeforeEndlineZ;

            if (endZ <= startZ)
                endZ = startZ + Mathf.Max(0.01f, objectsCfg.StepZ);

            var validParts = objectsCfg.Parts.Where(p => p != null && p.Prefab != null && p.Chance > 0f).ToList();
            if (validParts.Count == 0) return;

            List<float> GetLaneXs(int separation)
            {
                float w = objectsCfg.RoadWidth;
                return Mathf.Clamp(separation, 1, 3) switch
                {
                    1 => new List<float> { 0f },
                    2 => new List<float> { -w * 0.25f, +w * 0.25f },
                    _ => new List<float> { -w * 0.25f, 0f, +w * 0.25f },
                };
            }

            float anchorZ = startZ;
            int lineIndex = 0;
            while (true)
            {
                var activeParts = validParts.Where(p => lineIndex >= Mathf.Max(0, p.IgnoreFirstLines)).ToList();

                if (activeParts.Count == 0)
                {
                    float stepSkip = Mathf.Max(0.01f, objectsCfg.StepZ);
                    float midZSkip = anchorZ + stepSkip * 0.5f;
                    if (midZSkip > endZ + 1e-4f) break;

                    anchorZ += stepSkip;
                    lineIndex++;
                    continue;
                }

                var g1 = activeParts.Where(p => p.Separation == 1).ToList();
                var g2 = activeParts.Where(p => p.Separation == 2).ToList();
                var g3 = activeParts.Where(p => p.Separation == 3).ToList();

                float s1 = g1.Sum(p => p.Chance);
                float s2 = g2.Sum(p => p.Chance);
                float s3 = g3.Sum(p => p.Chance);
                float totalSep = (g1.Count > 0 ? s1 : 0f) + (g2.Count > 0 ? s2 : 0f) + (g3.Count > 0 ? s3 : 0f);
                if (totalSep <= 0f) break;

                float rr = UnityEngine.Random.Range(0f, totalSep), accSep = 0f;
                int sep;
                if (g1.Count > 0 && (accSep += s1) >= rr) sep = 1;
                else if (g2.Count > 0 && (accSep += s2) >= rr) sep = 2;
                else sep = 3;

                var group = sep == 1 ? g1 : sep == 2 ? g2 : g3;
                if (group.Count == 0) break;

                var selected = new List<ObjectsConfig.Part>(sep);
                Shuffle(selected);
                var pool = new List<ObjectsConfig.Part>(group);
                for (int need = sep; need > 0; need--)
                {
                    if (pool.Count == 0) pool = new List<ObjectsConfig.Part>(group);
                    float sum = pool.Sum(p => p.Chance);
                    ObjectsConfig.Part pick;
                    if (sum <= 0f)
                    {
                        pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                    }
                    else
                    {
                        float r = UnityEngine.Random.Range(0f, sum), acc = 0f;
                        pick = pool[0];
                        for (int i = 0; i < pool.Count; i++)
                        {
                            acc += pool[i].Chance;
                            if (r <= acc) { pick = pool[i]; break; }
                        }
                    }
                    selected.Add(pick);
                    pool.Remove(pick);
                }

                float avgExtra = selected.Average(p => p.ExtraStepZ);
                float localStepZ = Mathf.Max(0.01f, objectsCfg.StepZ + avgExtra);
                float midZ = anchorZ + localStepZ * 0.5f;
                if (midZ > endZ + 1e-4f) break;

                var lanes = GetLaneXs(sep);
                float yStart = start.y;
                float yEnd = start.y + (Mathf.Abs(objectsCfg.StepY) < 1e-5f ? 0f : objectsCfg.StepY);
                for (float y = yStart; y <= yEnd + 1e-6f; y += Mathf.Max(0.0001f, objectsCfg.StepY == 0 ? float.MaxValue : objectsCfg.StepY))
                {
                    for (int li = 0; li < lanes.Count; li++)
                    {
                        var part = selected[li % selected.Count];
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(part.Prefab);
                        if (inst == null) continue;

                        Undo.RegisterCreatedObjectUndo(inst, "Instantiate Level Object");
                        inst.transform.SetParent(root, true);
                        inst.transform.position = new Vector3(start.x + lanes[li], y, midZ) + part.SpawnOffset;
                        inst.name = $"Obj_{(string.IsNullOrEmpty(part.Name) ? "Part" : part.Name)}_{midZ:0.#}_{lanes[li]:0.#}";

                        if (part.IsGate)
                        {
                            if (part.GatePresets == null || part.GatePresets.Count == 0)
                                part.GatePresets = new List<ObjectsConfig.Part.GatePreset> { new ObjectsConfig.Part.GatePreset() };

                            foreach (var gate in FindMostGates(inst.transform))
                            {
                                var pr = part.GatePresets[UnityEngine.Random.Range(0, part.GatePresets.Count)];
                                string gateSign = string.IsNullOrEmpty(pr.Sign) ? "x" : pr.Sign;

                                var gateVals = BuildStepped(pr.RangeMin, pr.RangeMax, Mathf.Max(0.0001f, pr.Step), pr.NoZero);
                                float gv = gateVals[UnityEngine.Random.Range(0, gateVals.Count)];
                                string gText = ComposeSigned(gateSign, gv);

                                string oText = null;
                                if (part.UseOutput)
                                {
                                    string outSign = string.IsNullOrEmpty(part.OutputSign) ? "x" : part.OutputSign;
                                    var outVals = BuildStepped(part.OutputRangeMin, part.OutputRangeMax,
                                                               Mathf.Max(0.0001f, part.OutputStep), part.OutputNoZero);
                                    float ov = (outVals != null && outVals.Count > 0)
                                                ? outVals[UnityEngine.Random.Range(0, outVals.Count)]
                                                : gv;
                                    oText = ComposeSigned(outSign, ov);
                                }

                                var so = new SerializedObject(gate);
                                var pG = so.FindProperty("GateText");
                                var pO = so.FindProperty("OutputText");
                                if (pG != null) pG.stringValue = gText;
                                if (part.UseOutput && pO != null) pO.stringValue = oText;
                                so.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }

                    }

                    if (Mathf.Abs(objectsCfg.StepY) < 1e-5f) break;
                }
                anchorZ += localStepZ;
                lineIndex++;
            }
        }

        static string FormatNum(float v)
        {
            return Mathf.Approximately(v, Mathf.Round(v))
                ? Mathf.RoundToInt(v).ToString()
                : v.ToString("0.0");
        }

        static string ComposeSigned(string sign, float v)
        {
            if (string.IsNullOrEmpty(sign)) sign = "x";
            var num = FormatNum(v);
            if (sign == "+" && v < 0f) return num;
            return sign + num;
        }

        List<float> BuildStepped(float minV, float maxV, float step, bool noZero)
        {
            List<float> list = new();
            float lo = Mathf.Min(minV, maxV);
            float hi = Mathf.Max(minV, maxV);
            step = Mathf.Max(0.0001f, step);

            float k = Mathf.Ceil(lo / step);
            for (; ; k += 1f)
            {
                float v = k * step;
                if (v > hi + 1e-6f) break;
                if (noZero && Mathf.Abs(v) <= 1e-6f) continue;
                v = Mathf.Abs(v) < 1e-6f ? 0f : v;
                list.Add(v);
            }

            if (list.Count == 0)
            {
                if (!(noZero && Mathf.Abs(lo) <= 1e-6f)) list.Add(lo);
                if (!(noZero && Mathf.Abs(hi) <= 1e-6f) && Mathf.Abs(hi - lo) > 1e-6f) list.Add(hi);
                if (list.Count == 0) list.Add(lo != 0f ? lo : (hi != 0f ? hi : step));
            }
            return list;
        }

        void DrawProfilesUI()
        {
            EditorGUILayout.BeginHorizontal();
            var prev = currentProfile;

            currentProfile = (RunnerLevelProfile)EditorGUILayout.ObjectField(
                new GUIContent("Profile Asset"), currentProfile, typeof(RunnerLevelProfile), false);

            if (currentProfile != prev && currentProfile != null) LoadFromAsset(currentProfile);

            if (GUILayout.Button("New", GUILayout.Width(80)))
            {
                var path = EditorUtility.SaveFilePanelInProject("Create Profile", "RunnerLevelProfile", "asset", "Save under Assets/");
                if (!string.IsNullOrEmpty(path))
                {
                    var asset = ScriptableObject.CreateInstance<RunnerLevelProfile>();
                    AssetDatabase.CreateAsset(asset, path);
                    AssetDatabase.SaveAssets();
                    currentProfile = asset;
                    ApplyToAsset(currentProfile);
                }
            }

            if (GUILayout.Button("Load ▾", GUILayout.Width(80)))
            {
                var menu = new GenericMenu();
                var guids = AssetDatabase.FindAssets("t:RunnerLevelProfile");
                if (guids.Length == 0)
                {
                    menu.AddDisabledItem(new GUIContent("No profiles found"));
                }
                else
                {
                    var items = guids
                        .Select(g => new { guid = g, path = AssetDatabase.GUIDToAssetPath(g) })
                        .Select(x => new { x.guid, x.path, name = Path.GetFileNameWithoutExtension(x.path) })
                        .OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var it in items)
                    {
                        bool on = currentProfile != null && AssetDatabase.GetAssetPath(currentProfile) == it.path;
                        menu.AddItem(new GUIContent(it.name), on, () =>
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<RunnerLevelProfile>(it.path);
                            if (asset != null)
                            {
                                currentProfile = asset;
                                LoadFromAsset(currentProfile);
                            }
                        });
                    }

                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Reveal in Project"), false, () =>
                    {
                        if (currentProfile) EditorGUIUtility.PingObject(currentProfile);
                    });
                }

                var pos = Event.current != null ? Event.current.mousePosition : Vector2.zero;
                menu.DropDown(new Rect(pos, Vector2.zero));
            }

            if (GUILayout.Button("Save", GUILayout.Width(80)))
            {
                if (!currentProfile)
                    EditorUtility.DisplayDialog("Profile", "Assign or create a Profile Asset first.", "OK");
                else
                    ApplyToAsset(currentProfile);
            }

            EditorGUILayout.EndHorizontal();
        }

        static T Clone<T>(T src) where T : class, new()
            => JsonUtility.FromJson<T>(JsonUtility.ToJson(src));

        void ApplyToAsset(RunnerLevelProfile asset)
        {
            asset.layout = Clone(layout);
            asset.endgame = Clone(endgame);
            asset.bonus = Clone(bonus);
            asset.objectsCfg = Clone(objectsCfg);
            asset.output = Clone(output);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            RememberProfile();
        }

        void LoadFromAsset(RunnerLevelProfile asset)
        {
            layout = Clone(asset.layout) ?? new LayoutConfig();
            endgame = Clone(asset.endgame) ?? new EndgameConfig();
            bonus = Clone(asset.bonus) ?? new BonusRepeatConfig();
            objectsCfg = Clone(asset.objectsCfg) ?? new ObjectsConfig();
            output = Clone(asset.output) ?? new OutputConfig();
            RememberProfile();
            Repaint();
        }

        static void DrawStringArray(ref string[] arr, string label)
        {
            int size = Mathf.Max(0, EditorGUILayout.IntField(label + " Size", arr?.Length ?? 0));
            if (arr == null || arr.Length != size) Array.Resize(ref arr, size);
            for (int i = 0; i < size; i++)
                arr[i] = EditorGUILayout.TextField($"  [{i}]", arr[i]);
        }

        static void DrawMaterialList(ref List<Material> list, string label)
        {
            int size = Mathf.Max(0, EditorGUILayout.IntField(label + " Size", list?.Count ?? 0));
            list ??= new List<Material>(size);
            while (list.Count < size) list.Add(null);
            while (list.Count > size) list.RemoveAt(list.Count - 1);
            for (int i = 0; i < size; i++)
                list[i] = (Material)EditorGUILayout.ObjectField($"  [{i}]", list[i], typeof(Material), false);
        }

        void RememberProfile()
        {
            if (!currentProfile) return;
            var path = AssetDatabase.GetAssetPath(currentProfile);
            var guid = AssetDatabase.AssetPathToGUID(path);
            EditorPrefs.SetString(LastProfileGuidKey, guid);
        }

        static string ToUnityRelativePath(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (absolute.StartsWith(dataPath))
            {
                var rel = "Assets" + absolute[dataPath.Length..];
                return rel.TrimEnd('/');
            }
            EditorUtility.DisplayDialog("Invalid Folder", "Please choose a folder inside your project's Assets/ directory.", "OK");
            return "Assets";
        }

        static void EnsureFolder(string unityFolderPath)
        {
            if (AssetDatabase.IsValidFolder(unityFolderPath)) return;

            var parts = unityFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 0 || parts[0] != "Assets")
            {
                throw new Exception("Folder path must start with 'Assets'.");
            }

            string acc = "Assets";
            for (int i = 1; i < parts.Count; i++)
            {
                string next = acc + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(acc, parts[i]);
                }
                acc = next;
            }
        }

        static string ComputeNextAutoName(string folderPath, string baseName)
        {
            int n = GetNextNumber(folderPath, baseName);
            return $"{baseName} #{n}";
        }

        static string GetUniquePrefabPath(string folderPath, string baseName)
        {
            int n = GetNextNumber(folderPath, baseName);
            string fileName = $"{baseName} #{n}.prefab";
            return Path.Combine(folderPath, fileName).Replace('\\', '/');
        }

        static int GetNextNumber(string folderPath, string baseName)
        {
            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(baseName)) return 1;
            if (!AssetDatabase.IsValidFolder(folderPath)) return 1;

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            var regex = new Regex($@"^{Regex.Escape(baseName)} #(?<num>\d+)$");
            int max = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                var m = regex.Match(name);
                if (m.Success && int.TryParse(m.Groups["num"].Value, out int val))
                {
                    if (val > max) max = val;
                }
            }
            return Mathf.Max(1, max + 1);
        }
    }
}
#endif
