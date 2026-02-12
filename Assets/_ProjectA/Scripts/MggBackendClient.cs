using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectA
{
    public class MggBackendClient : MonoBehaviour
    {
        [SerializeField] private string backendBaseUrl = "http://localhost:8080";

        public string BackendBaseUrl => backendBaseUrl;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            ProjectASession.BackendBaseUrl = backendBaseUrl;
        }

        public IEnumerator GetConfig(Action<ConfigResponse> onSuccess, Action<string> onError)
        {
            yield return SendGet("/client/config", onSuccess, onError);
        }

        public IEnumerator Verify(string gameToken, string uid, string nickname, string accessToken, string refreshToken, Action<VerifyResponse> onSuccess, Action<string> onError)
        {
            var payload = new VerifyRequest
            {
                game_token = gameToken,
                uid = uid,
                nickname = nickname,
                access_token = accessToken,
                refresh_token = refreshToken
            };
            yield return SendPost("/client/verify", payload, onSuccess, onError);
        }

        public IEnumerator MatchStart(string roomId, float entryFee, Action<string> onSuccess, Action<string> onError)
        {
            var payload = new MatchStartRequest { session_jwt = ProjectASession.SessionJwt, room_id = roomId, entry_fee = entryFee };
            yield return SendPostRaw("/client/match/start", payload, onSuccess, onError);
        }

        public IEnumerator GameStart(GameStartRequest request, Action<GameStartResponse> onSuccess, Action<string> onError)
        {
            request.session_jwt = ProjectASession.SessionJwt;
            yield return SendPost("/client/game/start", request, onSuccess, onError);
        }

        public IEnumerator GameResult(GameResultRequest request, Action<GameResultResponse> onSuccess, Action<string> onError)
        {
            request.session_jwt = ProjectASession.SessionJwt;
            yield return SendPost("/client/game/result", request, onSuccess, onError);
        }

        public IEnumerator Purchase(string itemId, int itemCount, Action<string> onSuccess, Action<string> onError)
        {
            var payload = new PurchaseRequest { session_jwt = ProjectASession.SessionJwt, item_id = itemId, item_count = itemCount };
            yield return SendPostRaw("/client/items/purchase", payload, onSuccess, onError);
        }

        private IEnumerator SendGet<T>(string path, Action<T> onSuccess, Action<string> onError)
        {
            using var req = UnityWebRequest.Get(backendBaseUrl.TrimEnd('/') + path);
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(JsonUtility.FromJson<T>(req.downloadHandler.text));
            else
                onError?.Invoke(req.downloadHandler.text);
        }

        private IEnumerator SendPost<TReq, TRes>(string path, TReq payload, Action<TRes> onSuccess, Action<string> onError)
        {
            var json = JsonUtility.ToJson(payload);
            using var req = new UnityWebRequest(backendBaseUrl.TrimEnd('/') + path, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(JsonUtility.FromJson<TRes>(req.downloadHandler.text));
            else
                onError?.Invoke(req.downloadHandler.text);
        }

        private IEnumerator SendPostRaw<TReq>(string path, TReq payload, Action<string> onSuccess, Action<string> onError)
        {
            var json = JsonUtility.ToJson(payload);
            using var req = new UnityWebRequest(backendBaseUrl.TrimEnd('/') + path, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) onSuccess?.Invoke(req.downloadHandler.text);
            else onError?.Invoke(req.downloadHandler.text);
        }

        [Serializable] private class VerifyRequest { public string game_token; public string uid; public string nickname; public string access_token; public string refresh_token; }
        [Serializable] private class MatchStartRequest { public string session_jwt; public string room_id; public float entry_fee; }
        [Serializable] private class PurchaseRequest { public string session_jwt; public string item_id; public int item_count; }
    }

    [Serializable]
    public class GameStartRequest
    {
        public string session_jwt;
        public string room_id;
        public int player_count;
        public int user_count;
        public int bot_count;
        public string[] nicknames;
        public string game_start_time;
    }

    [Serializable] public class GameStartResponse { public int room_sequence; public float total_pot; public string server_time; }

    [Serializable] public class RankEntry { public string nickname; public int rank; }

    [Serializable]
    public class GameResultRequest
    {
        public string session_jwt;
        public string room_id;
        public int room_sequence;
        public float entry_fee;
        public int player_count;
        public int user_count;
        public int bot_count;
        public RankEntry[] results;
        public string game_start_time;
        public string game_end_time;
    }

    [Serializable] public class GameResultResponse { public bool ok; public bool duplicate; public string server_time; public string rewards; }
}
