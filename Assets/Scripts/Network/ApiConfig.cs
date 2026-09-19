namespace GameAct.Network
{
    /// <summary>
    /// 运行时配置。默认值仅作兜底；正式以 StreamingAssets/client_config.json 为准。
    /// act 游戏 G=2：Gateway = 13000 + 2*100 + 80 = 13280
    /// </summary>
    public class ApiConfig
    {
        public string MpBaseUrl { get; set; } = "http://localhost:11080";
        public string GameBaseUrl { get; set; } = "http://localhost:13280";
        /// <summary>AssetBundle / 热更清单根地址，例如 http://host:8080/ab/</summary>
        public string AbUpdateUrl { get; set; } = "http://localhost:8080/ab/";
        public string Channel { get; set; } = "official";
        public string Region { get; set; } = "cn";
        public string Platform { get; set; } = "pc";
        public string GameId { get; set; } = "act";
        public string AppId { get; set; } = "test_app";
        public int ClientVersionCode { get; set; } = 1;
        public string ResourceVersion { get; set; } = "0.0.1";
    }
}
