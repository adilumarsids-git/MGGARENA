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
            _runner.AddCallbacks(inputProvider);

            var sceneInfo = new NetworkSceneInfo();
            var activeScene = SceneManager.GetActiveScene();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(activeScene.buildIndex), LoadSceneMode.Single);

            await _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = sessionName,
                Scene = sceneInfo,
                PlayerCount = 6
            });
        }
    }
}
