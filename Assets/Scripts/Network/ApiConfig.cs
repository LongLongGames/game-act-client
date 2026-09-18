namespace GameAct.Network
{
    /// <summary>
    /// API 配置。可在 Inspector / 远程配置覆盖。
    /// act 游戏 G=2：Gateway = 13000 + 2*100 + 80 = 13280
    /// </summary>
    public class ApiConfig
    {
        public string MpBaseUrl { get; set; } = "http://localhost:11080";
        public string GameBaseUrl { get; set; } = "http://localhost:13280";
        public string Channel { get; set; } = "official";
        public string Region { get; set; } = "cn";
        public string Platform { get; set; } = "pc";
        public string GameId { get; set; } = "act";
        public string AppId { get; set; } = "test_app";
        /// <summary>客户端版本号（整数 code，用于 version-check）。</summary>
        public int ClientVersionCode { get; set; } = 1;
        /// <summary>资源版本字符串。</summary>
        public string ResourceVersion { get; set; } = "0.0.1";
    }
}
