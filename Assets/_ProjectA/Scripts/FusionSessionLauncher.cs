using System.Threading.Tasks;
using Fusion;
using UnityEngine;

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

            await _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = sessionName,
                Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
                PlayerCount = 6
            });
        }
    }
}
