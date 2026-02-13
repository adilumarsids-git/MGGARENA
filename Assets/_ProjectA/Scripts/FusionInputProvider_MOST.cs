using Fusion;
using Solo.MOST_IN_ONE;
using UnityEngine;

namespace ProjectA
{
    public class FusionInputProvider_MOST : SimulationBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private MOST_Controller moveJoystick;
        [SerializeField] private MOST_Controller shootJoystick;
        [SerializeField] private MOST_Controller throwJoystick;
        [SerializeField] private KeyCode activeKey = KeyCode.Q;
        [SerializeField] private KeyCode ultimateKey = KeyCode.E;

        private bool _lastThrowHeld;

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var move = moveJoystick ? moveJoystick.RawValue : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            var aim = shootJoystick && shootJoystick.RawValue.sqrMagnitude > 0.001f ? shootJoystick.RawValue : move;
            var throwHeld = throwJoystick && throwJoystick.IsTouched;

            var data = new FusionInputData
            {
                Move = Vector2.ClampMagnitude(move, 1f),
                Aim = Vector2.ClampMagnitude(aim, 1f),
                ShootHeld = shootJoystick && shootJoystick.IsTouched,
                ThrowPressed = throwHeld && !_lastThrowHeld,
                ActivePressed = Input.GetKeyDown(activeKey),
                UltimatePressed = Input.GetKeyDown(ultimateKey)
            };
            _lastThrowHeld = throwHeld;
            input.Set(data);
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
