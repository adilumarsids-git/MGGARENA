#if UNITY_EDITOR
using System.Collections.Generic;
using ProjectA;
using Solo.MOST_IN_ONE;
using Fusion;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectA.Editor
{
    public static class ProjectASceneBuilder
    {
        [MenuItem("ProjectA/Build Complete Test Setup")]
        public static void BuildCompleteTestSetup()
        {
            ProjectAPrefabTools.GenerateNetworkPrefabs();
            EnsureRunnerPrefab();
            BuildBootScene();
            BuildLobbyScene();
            BuildGameScene();
            ApplyBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ProjectA complete test setup generated: Boot/Lobby/Game scenes and wiring.");
        }

        private static void EnsureRunnerPrefab()
        {
            const string path = "Assets/_ProjectA/Prefabs/ProjectA_NetworkRunner.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return;

            var go = new GameObject("ProjectA_NetworkRunner");
            go.AddComponent<NetworkRunner>();
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "Boot";

            var bootstrap = new GameObject("Bootstrap");
            var client = bootstrap.AddComponent<MggBackendClient>();
            var boot = bootstrap.AddComponent<BootController>();
            SetRef(boot, "backendClient", client);
            SetString(boot, "lobbySceneName", "Lobby");

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Boot.unity");
        }

        private static void BuildLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "Lobby";

            var root = new GameObject("LobbyRoot");
            var client = root.AddComponent<MggBackendClient>();
            var lobby = root.AddComponent<LobbyController>();
            SetRef(lobby, "backendClient", client);
            SetString(lobby, "gameSceneName", "Game");
            SetString(lobby, "selectedRoomId", "room_ffa_6");

            var canvas = CreateCanvas("LobbyCanvas");
            var playButton = CreateButton(canvas.transform, "PlayButton", "Play", new Vector2(0, -40));
            UnityEventTools.AddPersistentListener(playButton.onClick, lobby.OnPlayClicked);

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Lobby.unity");
        }

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "Game";

            var backend = new GameObject("BackendClient");
            var client = backend.AddComponent<MggBackendClient>();

            var netRoot = new GameObject("NetworkingRoot");
            var inputProvider = netRoot.AddComponent<FusionInputProvider_MOST>();
            var launcher = netRoot.AddComponent<FusionSessionLauncher>();

            var runnerPrefab = AssetDatabase.LoadAssetAtPath<NetworkRunner>("Assets/_ProjectA/Prefabs/ProjectA_NetworkRunner.prefab");
            SetRef(launcher, "runnerPrefab", runnerPrefab);
            SetRef(launcher, "inputProvider", inputProvider);

            var managerGo = new GameObject("NetworkGameManager");
            managerGo.AddComponent<NetworkObject>();
            var gameManager = managerGo.AddComponent<NetworkGameManager>();
            SetRef(gameManager, "backendClient", client);

            var spawnRoot = new GameObject("SpawnPoints");
            var spawns = new List<Transform>();
            for (var i = 0; i < 6; i++)
            {
                var t = new GameObject($"P{i + 1}").transform;
                t.SetParent(spawnRoot.transform);
                var angle = i * Mathf.PI * 2f / 6f;
                t.position = new Vector3(Mathf.Cos(angle) * 6f, 0f, Mathf.Sin(angle) * 6f);
                spawns.Add(t);
            }
            SetArray(gameManager, "spawnPoints", spawns.ToArray());

            var characterPrefabs = new List<NetworkObject>();
            for (var i = 1; i <= 5; i++)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_ProjectA/Prefabs/NetworkPlayer_Character{i}.prefab");
                if (!p) continue;
                var n = p.GetComponent<NetworkObject>();
                if (n) characterPrefabs.Add(n);
            }
            if (characterPrefabs.Count > 0)
            {
                SetArray(gameManager, "characterPrefabs", characterPrefabs.ToArray());
                SetRef(gameManager, "botPrefab", characterPrefabs[0]);
            }

            var hudCanvas = CreateCanvas("GameHUD_Input");
            var move = CreateJoystick(hudCanvas.transform, "JoystickMove", new Vector2(-450, -200));
            var shoot = CreateJoystick(hudCanvas.transform, "JoystickShoot", new Vector2(450, -200));
            var thrw = CreateJoystick(hudCanvas.transform, "JoystickThrow", new Vector2(300, -80));

            SetRef(inputProvider, "moveJoystick", move);
            SetRef(inputProvider, "shootJoystick", shoot);
            SetRef(inputProvider, "throwJoystick", thrw);

            var startButton = CreateButton(hudCanvas.transform, "StartSessionButton", "Start Session", new Vector2(0, 220));
            UnityEventTools.AddPersistentListener(startButton.onClick, launcher.StartSharedSession);

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");
        }

        private static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            if (!Object.FindObjectOfType<EventSystem>())
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            return canvas;
        }

        private static Button CreateButton(Transform parent, string name, string text, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(240, 70);
            rt.anchoredPosition = anchoredPos;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.17f, 0.57f, 0.89f, 0.85f);
            var button = go.AddComponent<Button>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var txt = textGo.AddComponent<Text>();
            txt.text = text;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return button;
        }

        private static MOST_Controller CreateJoystick(Transform parent, string name, Vector2 anchoredPos)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rt = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220, 220);
            rt.anchorMin = new Vector2(.5f, 0);
            rt.anchorMax = new Vector2(.5f, 0);
            rt.anchoredPosition = anchoredPos;

            var bg = root.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.2f);

            var knobGo = new GameObject("Knob");
            knobGo.transform.SetParent(root.transform, false);
            var knobRt = knobGo.AddComponent<RectTransform>();
            knobRt.sizeDelta = new Vector2(90, 90);
            var knob = knobGo.AddComponent<Image>();
            knob.color = new Color(1f, 1f, 1f, 0.7f);

            var ctrl = root.AddComponent<MOST_Controller>();
            ctrl.Type = MOST_Controller.ControllerType.StaticJoystick;
            ctrl.Controls = MOST_Controller.InputControl.ControlledByScreenTouch_Pointer;
            ctrl.Background = bg;
            ctrl.Knob = knob;
            ctrl.EnableOnStart = true;
            ctrl.EnableTouch = true;
            ctrl.EnableButtons = false;

            return ctrl;
        }

        private static void ApplyBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Lobby.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true)
            };
            EditorBuildSettings.scenes = scenes;
        }

        private static void SetRef(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
        }

        private static void SetString(Object target, string fieldName, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.stringValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
        }

        private static void SetArray(Object target, string fieldName, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null || !prop.isArray) return;
            prop.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
#endif
