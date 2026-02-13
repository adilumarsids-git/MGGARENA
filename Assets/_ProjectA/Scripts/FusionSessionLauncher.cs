using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectA
{
    public class FusionSessionLauncher : MonoBehaviour
    {
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private FusionInputProvider_MOST inputProvider;
        [SerializeField] private string sessionName = "projecta-random-queue";

        private NetworkRunner _runner;

        public async void StartSharedSession()
        {
            if (_runner != null) return;

            _runner = Instantiate(runnerPrefab);
            _runner.ProvideInput = true;

            var sceneManager = _runner.GetComponent<NetworkSceneManagerDefault>();
            if (!sceneManager)
            {
                sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            RegisterCallbacks(_runner);

            var sceneInfo = new NetworkSceneInfo();
            var activeScene = SceneManager.GetActiveScene();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(activeScene.buildIndex), LoadSceneMode.Single);

            var result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = sessionName,
                Scene = sceneInfo,
                SceneManager = sceneManager,
                PlayerCount = 6
            });

            if (!result.Ok)
            {
                Debug.LogError($"Failed to start Fusion shared session: {result.ShutdownReason}");
            }
        }

        private void RegisterCallbacks(NetworkRunner runner)
        {
            if (inputProvider)
            {
                runner.AddCallbacks(inputProvider);
            }

            var callbacks = FindObjectsOfType<MonoBehaviour>(true).OfType<INetworkRunnerCallbacks>();
            foreach (var callback in callbacks)
            {
                if (callback == inputProvider) continue;
                runner.AddCallbacks(callback);
            }
        }
    }
}
