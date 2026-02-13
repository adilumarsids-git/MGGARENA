using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectA.Networking
{
    public class FusionBootstrap : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static FusionBootstrap Instance { get; private set; }

        [Header("Runner")]
        [SerializeField] private NetworkRunner runner;
        [SerializeField] private NetworkSceneManagerDefault sceneManager;
        [SerializeField] private NetworkInputProvider_MOST inputProvider;

        [Header("Spawn")]
        [SerializeField] private NetworkObject networkPlayerPrefab;
        [SerializeField] private NetworkObject networkGameManagerPrefab;
        [SerializeField] private NetworkSpawnPoints spawnPoints;
        [SerializeField] private float matchDurationSeconds = 180f;

        [Header("Match")]
        [SerializeField] private FusionGameMode selectedMode = FusionGameMode.FFA;
        [SerializeField] private string matchSceneName = "Match";
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private int maxPlayers = 8;

        private readonly Dictionary<PlayerRef, NetworkObject> _spawned = new();
        private bool _managerSpawnRequested;

        public NetworkRunner Runner => runner;

        private void Awake()
        {
            if (Instance == null || !Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                RemoveChildGameManagersIfAny();
                EnsureDependencies();
                return;
            }

            if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void StartSelectedModeFromUI()
        {
            if (Instance == null)
            {
                Debug.LogError("[FusionBootstrap] No instance found. Ensure UIRoot prefab includes FusionBootstrap.");
                return;
            }

            Instance.StartOrJoinSelectedMode();
        }

        public async void StartOrJoinSelectedMode()
        {
            await StartOrJoin(selectedMode);
        }

        public async Task StartOrJoin(FusionGameMode mode)
        {
            selectedMode = mode;

            if (runner == null)
            {
                EnsureDependencies();
            }

            if (runner.IsRunning)
            {
                Debug.LogWarning("[FusionBootstrap] Runner already running.");
                return;
            }

            var sceneIndex = ResolveSceneBuildIndex(matchSceneName);
            if (sceneIndex < 0)
            {
                Debug.LogError($"[FusionBootstrap] Match scene '{matchSceneName}' not found in Build Settings.");
                return;
            }

            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = null,
                Scene = SceneRef.FromIndex(sceneIndex),
                SceneManager = sceneManager,
                PlayerCount = maxPlayers,
                EnableClientSessionCreation = true,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    { "mode", (int)mode }
                }
            };

            var result = await runner.StartGame(args);
            if (!result.Ok)
            {
                Debug.LogError($"[FusionBootstrap] Start failed: {result.ShutdownReason}");
            }
        }

        public async void LeaveSessionAndReturnToLobby()
        {
            if (runner != null && runner.IsRunning)
            {
                await runner.Shutdown();
            }

            if (!string.Equals(SceneManager.GetActiveScene().name, lobbySceneName, StringComparison.Ordinal))
            {
                SceneManager.LoadScene(lobbySceneName);
            }
        }

        private int ResolveSceneBuildIndex(string sceneName)
        {
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path.EndsWith($"/{sceneName}.unity", StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RemoveChildGameManagersIfAny()
        {
            var childManagers = GetComponentsInChildren<NetworkGameManager>(true);
            foreach (var manager in childManagers)
            {
                if (manager == null || manager.transform == transform)
                {
                    continue;
                }

                Debug.LogWarning("[FusionBootstrap] Remove child NetworkGameManager from UIRoot. It is spawned automatically at runtime.");
                Destroy(manager.gameObject);
            }
        }

        private void EnsureDependencies()
        {
            if (runner == null)
            {
                runner = gameObject.GetComponent<NetworkRunner>() ?? gameObject.AddComponent<NetworkRunner>();
            }

            runner.ProvideInput = true;
            runner.AddCallbacks(this);

            if (sceneManager == null)
            {
                sceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>() ?? gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            if (inputProvider == null)
            {
                inputProvider = gameObject.GetComponent<NetworkInputProvider_MOST>() ?? gameObject.AddComponent<NetworkInputProvider_MOST>();
            }

            if (spawnPoints == null)
            {
                spawnPoints = FindObjectOfType<NetworkSpawnPoints>();
            }
        }

        public void OnPlayerJoined(NetworkRunner runnerInstance, PlayerRef player)
        {
            TryEnsureGameManagerSpawned(runnerInstance);

            if (networkPlayerPrefab == null || player != runnerInstance.LocalPlayer || _spawned.ContainsKey(player))
            {
                return;
            }

            if (spawnPoints == null)
            {
                spawnPoints = FindObjectOfType<NetworkSpawnPoints>();
            }

            var spawnPosition = spawnPoints != null
                ? spawnPoints.ResolveSpawn(selectedMode, player)
                : Vector3.up * 0.5f;

            var spawned = runnerInstance.Spawn(networkPlayerPrefab, spawnPosition, Quaternion.identity, player);
            _spawned[player] = spawned;
        }

        private void TryEnsureGameManagerSpawned(NetworkRunner runnerInstance)
        {
            if (networkGameManagerPrefab == null || !runnerInstance.IsSharedModeMasterClient)
            {
                return;
            }

            if (_managerSpawnRequested || NetworkGameManager.Instance != null || FindObjectOfType<NetworkGameManager>() != null)
            {
                return;
            }

            _managerSpawnRequested = true;
            var managerObject = runnerInstance.Spawn(networkGameManagerPrefab, Vector3.zero, Quaternion.identity, runnerInstance.LocalPlayer);
            var manager = managerObject.GetComponent<NetworkGameManager>();
            manager?.Configure(selectedMode, matchDurationSeconds);
        }

        public void OnPlayerLeft(NetworkRunner runnerInstance, PlayerRef player)
        {
            if (_spawned.TryGetValue(player, out var networkObject) && networkObject != null && networkObject.HasStateAuthority)
            {
                runnerInstance.Despawn(networkObject);
            }

            _spawned.Remove(player);
        }

        public void OnInput(NetworkRunner runnerInstance, NetworkInput input)
        {
            if (inputProvider == null)
            {
                return;
            }

            input.Set(inputProvider.Capture());
        }

        public void OnInputMissing(NetworkRunner runnerInstance, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runnerInstance) { }
        public void OnDisconnectedFromServer(NetworkRunner runnerInstance, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runnerInstance, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runnerInstance, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runnerInstance, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runnerInstance, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runnerInstance, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runnerInstance, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner runnerInstance)
        {
            spawnPoints = FindObjectOfType<NetworkSpawnPoints>();
        }
        public void OnSceneLoadStart(NetworkRunner runnerInstance) { }
        public void OnObjectEnterAOI(NetworkRunner runnerInstance, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runnerInstance, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runnerInstance, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runnerInstance, PlayerRef player, ReliableKey key, float progress) { }
        public void OnShutdown(NetworkRunner runnerInstance, ShutdownReason shutdownReason)
        {
            _spawned.Clear();
            _managerSpawnRequested = false;
        }
    }
}
