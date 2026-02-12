using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace ProjectA
{
    public enum MatchMode { Team3v3, FFA6 }

    public class NetworkGameManager : NetworkBehaviour, INetworkRunnerCallbacks
    {
        [Header("Prefabs")]
        [SerializeField] private NetworkPrefabRef[] characterPrefabs;
        [SerializeField] private NetworkPrefabRef botPrefab;
        [SerializeField] private Transform[] spawnPoints;

        [Header("Rules")]
        [SerializeField] private MatchMode mode = MatchMode.FFA6;
        [SerializeField] private int maxPlayers = 6;
        [SerializeField] private int matchLengthSeconds = 180;

        [Header("Refs")]
        [SerializeField] private MggBackendClient backendClient;

        [Networked] public TickTimer MatchTimer { get; set; }
        [Networked] public NetworkBool MatchEnded { get; set; }

        private readonly Dictionary<PlayerRef, NetworkObject> _playerObjects = new();

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                MatchTimer = TickTimer.CreateFromSeconds(Runner, matchLengthSeconds);
                ProjectASession.MatchStartUtc = DateTime.UtcNow;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || MatchEnded) return;
            if (MatchTimer.Expired(Runner))
            {
                MatchEnded = true;
                StartCoroutine(SendResults());
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            SpawnPlayerFor(player, false);
            FillBotsIfNeeded();
            if (runner.ActivePlayers.Count() == 1)
            {
                StartCoroutine(SendGameStart());
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            if (_playerObjects.TryGetValue(player, out var existing))
            {
                var pos = existing.transform.position;
                runner.Despawn(existing);
                _playerObjects.Remove(player);
                SpawnBotAt(pos, $"Bot_{player.PlayerId}");
            }
            FillBotsIfNeeded();
        }

        private void SpawnPlayerFor(PlayerRef player, bool isBot)
        {
            var point = spawnPoints.Length > 0 ? spawnPoints[_playerObjects.Count % spawnPoints.Length] : transform;
            var prefab = isBot ? botPrefab : characterPrefabs[player.PlayerId % characterPrefabs.Length];
            var obj = Runner.Spawn(prefab, point.position, Quaternion.identity, isBot ? PlayerRef.None : player);
            var controller = obj.GetComponent<NetworkPlayer_MOST>();
            if (controller)
            {
                controller.IsBot = isBot;
                controller.SetLocalPresentation(obj.HasInputAuthority);
            }
            if (!isBot) _playerObjects[player] = obj;
        }

        private void SpawnBotAt(Vector3 position, string nickname)
        {
            var obj = Runner.Spawn(botPrefab, position, Quaternion.identity, PlayerRef.None);
            var controller = obj.GetComponent<NetworkPlayer_MOST>();
            if (controller) controller.IsBot = true;
            obj.name = nickname;
        }

        private void FillBotsIfNeeded()
        {
            var count = FindObjectsOfType<NetworkPlayer_MOST>().Length;
            while (count < maxPlayers)
            {
                SpawnPlayerFor(PlayerRef.None, true);
                count++;
            }
        }

        private IEnumerator SendGameStart()
        {
            var allPlayers = FindObjectsOfType<NetworkPlayer_MOST>();
            var nicknames = allPlayers.Select(x => x.name).ToArray();
            var req = new GameStartRequest
            {
                room_id = ProjectASession.CurrentRoomId,
                player_count = maxPlayers,
                user_count = _playerObjects.Count,
                bot_count = maxPlayers - _playerObjects.Count,
                nicknames = nicknames,
                game_start_time = DateTime.UtcNow.ToString("o")
            };

            yield return backendClient.GameStart(req,
                ok => ProjectASession.CurrentRoomSequence = ok.room_sequence,
                err => Debug.LogError($"Game start failed: {err}"));
        }

        private IEnumerator SendResults()
        {
            var ordered = FindObjectsOfType<NetworkPlayer_MOST>().OrderByDescending(x => x.Kills).ToArray();
            var ranks = new List<RankEntry>();
            for (int i = 0; i < ordered.Length; i++)
            {
                ranks.Add(new RankEntry { nickname = ordered[i].name, rank = i + 1 });
            }

            var req = new GameResultRequest
            {
                room_id = ProjectASession.CurrentRoomId,
                room_sequence = ProjectASession.CurrentRoomSequence,
                entry_fee = 0,
                player_count = maxPlayers,
                user_count = _playerObjects.Count,
                bot_count = maxPlayers - _playerObjects.Count,
                results = ranks.ToArray(),
                game_start_time = ProjectASession.MatchStartUtc.ToString("o"),
                game_end_time = DateTime.UtcNow.ToString("o")
            };

            yield return backendClient.GameResult(req,
                ok => Debug.Log($"Result sent. duplicate={ok.duplicate}"),
                err => Debug.LogError($"Result failed: {err}"));
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
