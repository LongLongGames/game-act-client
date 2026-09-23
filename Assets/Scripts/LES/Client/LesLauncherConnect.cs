using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameAct.Les.Client
{
    /// <summary>
    /// 官方 Launcher 启动客户端时的连接入口。
    /// Launcher 可通过命令行传入：
    ///   -lesConnect host:port
    ///   -lesUser "PlayerName"
    /// 或由上层 UI 调用 ConnectFromLauncher。
    /// </summary>
    public sealed class LesLauncherConnect : MonoBehaviour
    {
        [SerializeField] string _defaultAddress = "127.0.0.1";
        [SerializeField] int _defaultPort = LesClientSession.DefaultPort;

        LesClientSession _session;

        public LesClientSession Session => _session;

        void Awake()
        {
            _session = new LesClientSession();
            DontDestroyOnLoad(gameObject);

            string address = _defaultAddress;
            int port = _defaultPort;
            string user = null;

            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-lesConnect")
                {
                    var parts = args[i + 1].Split(':');
                    address = parts[0];
                    if (parts.Length > 1 && int.TryParse(parts[1], out var p))
                        port = p;
                }
                if (args[i] == "-lesUser")
                    user = args[i + 1];
            }

            if (!string.IsNullOrEmpty(user))
                _session.SetUserName(user);

            // 有 -lesConnect 时自动连；否则留给 UI
            bool hasConnectArg = false;
            foreach (var a in args)
                if (a == "-lesConnect") { hasConnectArg = true; break; }

            if (hasConnectArg)
                ConnectFromLauncher(address, port).Forget();
        }

        public async UniTask<bool> ConnectFromLauncher(string address, int port)
        {
            Debug.Log($"[Launcher] Connecting to {address}:{port}");
            return await _session.ConnectAsync(address, port);
        }

        void Update()
        {
            _session?.Poll();
        }

        void OnDestroy()
        {
            _session?.Dispose();
        }
    }
}
