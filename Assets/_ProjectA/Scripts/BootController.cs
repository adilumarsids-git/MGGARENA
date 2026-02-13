using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace ProjectA
{
    public class BootController : MonoBehaviour
    {
        [SerializeField] private MggBackendClient backendClient;
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private string fallbackNickname = "Player";

        private IEnumerator Start()
        {
            var gameToken = ExtractQuery("game_token");
            var uid = ExtractQuery("uid");
            var nickname = string.IsNullOrWhiteSpace(ExtractQuery("nickname")) ? fallbackNickname : ExtractQuery("nickname");
            var access = ExtractQuery("access_token");
            var refresh = ExtractQuery("refresh_token");

            ProjectASession.GameToken = gameToken;
            ProjectASession.Nickname = nickname;

            bool completed = false;
            yield return backendClient.Verify(gameToken, uid, nickname, access, refresh,
                ok =>
                {
                    ProjectASession.ApplyVerify(ok);
                    completed = true;
                },
                err =>
                {
                    Debug.LogError($"Verify failed: {err}");
                    completed = true;
                });

            if (completed) SceneManager.LoadScene(lobbySceneName);
        }

        private string ExtractQuery(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var absolute = Application.absoluteURL;
            var idx = absolute.IndexOf('?');
            if (idx < 0) return string.Empty;
            var query = absolute.Substring(idx + 1).Split('&');
            for (int i = 0; i < query.Length; i++)
            {
                var kv = query[i].Split('=');
                if (kv.Length == 2 && kv[0] == key) return UnityWebRequest.UnEscapeURL(kv[1]);
            }
#endif
            return string.Empty;
        }
    }
}
