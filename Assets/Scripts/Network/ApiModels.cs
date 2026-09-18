using System;

namespace GameAct.Network
{
    // ---------- MP Auth ----------

    [Serializable]
    public class LoginRequest
    {
        public string provider;
        public string app_id;
        public string device_id;
        public LoginAuthPayload auth_payload;
    }

    [Serializable]
    public class LoginAuthPayload
    {
        public string username;
        public string password;
    }

    [Serializable]
    public class LoginResponse
    {
        public string access_token;
        public string refresh_token;
    }

    // ---------- User profile ----------

    [Serializable]
    public class PlayerProfile
    {
        public string id;
        public string mp_account_id;
        public string game_id;
        public string nickname;
        public int level;
        public string extra_json;
    }

    [Serializable]
    public class UpdateProfileRequest
    {
        public string game_id;
        public string nickname;
    }

    // ---------- Version check (公开，无需 JWT) ----------

    [Serializable]
    public class VersionCheckResponse
    {
        public bool force_update;
        public bool optional_update;
        public string min_client_version;
        public string latest_client_version;
        public string resource_version;
        public string download_url;
        public string message;
    }
}
