using System;

namespace GameAct.Network
{
    [Serializable]
    public class LoginAuthPayload
    {
        public string username;
        public string password;
        /// <summary>Steam 登录：64 位 SteamId 字符串。</summary>
        public string steam_id;
        /// <summary>Steam Session Ticket（hex）。正式服应校验。</summary>
        public string session_ticket;
    }

    [Serializable]
    public class LoginRequest
    {
        public string provider;
        public string app_id;
        public string device_id;
        public LoginAuthPayload auth_payload;
    }

    [Serializable]
    public class LoginResponse
    {
        public string access_token;
        public string token_type;
        public int expires_in;
    }

    [Serializable]
    public class VersionCheckResponse
    {
        public bool force_update;
        public bool optional_update;
        public string latest_client_version;
        public string message;
    }

    [Serializable]
    public class PlayerProfile
    {
        public string id;
        public string nickname;
        public int level;
        public string game_id;
        public string mp_account_id;
    }
}
