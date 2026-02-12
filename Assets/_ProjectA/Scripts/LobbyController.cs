using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectA
{
    public class LobbyController : MonoBehaviour
    {
        [SerializeField] private MggBackendClient backendClient;
        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private string selectedRoomId = "room_ffa_6";
        [SerializeField] private float entryFee = 10;

        public void OnPlayClicked()
        {
            StartCoroutine(PlayFlow());
        }

        private IEnumerator PlayFlow()
        {
            yield return backendClient.MatchStart(selectedRoomId, entryFee,
                _ =>
                {
                    ProjectASession.CurrentRoomId = selectedRoomId;
                    SceneManager.LoadScene(gameSceneName);
                },
                err => Debug.LogError($"Match eligibility failed: {err}"));
        }

        public IEnumerator RefreshConfig()
        {
            yield return backendClient.GetConfig(
                cfg => Debug.Log($"Config rooms={cfg.rooms?.Count ?? 0} items={cfg.items?.Count ?? 0}"),
                err => Debug.LogError(err));
        }
    }
}
