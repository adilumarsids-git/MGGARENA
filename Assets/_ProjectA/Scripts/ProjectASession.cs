using System;
using System.Collections.Generic;

namespace ProjectA
{
    [Serializable]
    public class VerifyResponse
    {
        public bool is_verified;
        public float emgg_balance;
        public string server_time;
        public string session_jwt;
    }

    [Serializable] public class RoomConfig { public string room_id; public string name; public float entry_fee; public string status; }
    [Serializable] public class ItemConfig { public string item_id; public string name; public int price; }
    [Serializable] public class ConfigResponse { public List<RoomConfig> rooms; public List<ItemConfig> items; public string server_time; }

    public static class ProjectASession
    {
        public static string BackendBaseUrl;
        public static string SessionJwt;
        public static string GameToken;
        public static string Nickname;
        public static float Balance;
        public static string CurrentRoomId;
        public static int CurrentRoomSequence;
        public static DateTime MatchStartUtc;

        public static void ApplyVerify(VerifyResponse verify)
        {
            SessionJwt = verify.session_jwt;
            Balance = verify.emgg_balance;
        }
    }
}
