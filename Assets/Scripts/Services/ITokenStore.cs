namespace GameAct.Services
{
    /// <summary>
    /// 仅负责 access_token 持久化读写，不解释登录态。见 ADR-0004。
    /// </summary>
    public interface ITokenStore
    {
        string GetAccessToken();
        void SetAccessToken(string token);
        void ClearToken();
    }
}
