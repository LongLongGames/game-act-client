using UnityEngine;

namespace GameAct.Services
{
    /// <summary>
    /// PlayerPrefs 持久化 access_token。写入/删除后必须 Save()。
    /// </summary>
    public class TokenStore : ITokenStore
    {
        const string Key = "access_token";

        public string GetAccessToken()
        {
            return PlayerPrefs.GetString(Key, null);
        }

        public void SetAccessToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                ClearToken();
                return;
            }
            PlayerPrefs.SetString(Key, token);
            PlayerPrefs.Save();
        }

        public void ClearToken()
        {
            if (PlayerPrefs.HasKey(Key))
            {
                PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
            }
        }
    }
}
