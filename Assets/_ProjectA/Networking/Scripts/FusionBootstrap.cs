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
        [Header("Runner")]
        [SerializeField] private NetworkRunner runner;
        [SerializeField] private NetworkSceneManagerDefault sceneManager;
        [SerializeField] private NetworkInputProvider inputProvider;

        [Header("Spawn")]
        [SerializeField] private NetworkObject networkPlayerPrefab;
        [SerializeField] private NetworkSpawnPoints spawnPoints;

        [Header("Match")]
        [SerializeField] private FusionGameMode selectedMode = FusionGameMode.FFA;
        [SerializeField] private string sessionNamePrefix = "projecta";
        [SerializeField] private int maxPlayers = 8;

        private readonly Dictionary<PlayerRef, NetworkObject> _spawned = new();

        public NetworkRunner Runner => runner;

        private void Awake()
        {
            EnsureDependencies();
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

            var sceneIndex = SceneManager.GetActiveScene().buildIndex;
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

        public async void LeaveSessionAndReturnToLobby(string lobbySceneName = "Lobby")
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
                inputProvider = gameObject.GetComponent<NetworkInputProvider>() ?? gameObject.AddComponent<NetworkInputProvider>();
            }

            if (spawnPoints == null)
            {
                spawnPoints = FindObjectOfType<NetworkSpawnPoints>();
            }
        }

        public void OnPlayerJoined(NetworkRunner runnerInstance, PlayerRef player)
        {
            if (!runnerInstance.IsSharedModeMasterClient || networkPlayerPrefab == null)
            {
                return;
            }

            var spawnPosition = spawnPoints != null
                ? spawnPoints.ResolveSpawn(selectedMode, player)
                : Vector3.up * 0.5f;

            var spawned = runnerInstance.Spawn(networkPlayerPrefab, spawnPosition, Quaternion.identity, player);
            _spawned[player] = spawned;
        }

        public void OnPlayerLeft(NetworkRunner runnerInstance, PlayerRef player)
        {
            if (_spawned.TryGetValue(player, out var networkObject) && networkObject != null)
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
        public void OnSceneLoadDone(NetworkRunner runnerInstance) { }
        public void OnSceneLoadStart(NetworkRunner runnerInstance) { }
        public void OnObjectEnterAOI(NetworkRunner runnerInstance, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runnerInstance, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runnerInstance, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runnerInstance, PlayerRef player, ReliableKey key, float progress) { }
        public void OnShutdown(NetworkRunner runnerInstance, ShutdownReason shutdownReason) { }
    }
}
