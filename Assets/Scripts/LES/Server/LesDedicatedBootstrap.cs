using UnityEngine;

namespace GameAct.Les.Server
{
    /// <summary>
    /// VPS / 官服启动入口。
    /// 构建时勾选 Dedicated Server / 使用 -batchmode -nographics，
    /// 或命令行：-lesPort 9050 -lesMonsters 24
    /// </summary>
    public sealed class LesDedicatedBootstrap : MonoBehaviour
    {
        [SerializeField] LesServerHost _host;

        void Awake()
        {
            if (_host == null)
                _host = gameObject.AddComponent<LesServerHost>();

            int port = LesServerHost.DefaultPort;
            int monsters = LesServerHost.DefaultMonsterCount;

            // 解析命令行（Launcher / systemd 可传）
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-lesPort" && int.TryParse(args[i + 1], out var p))
                    port = p;
                if (args[i] == "-lesMonsters" && int.TryParse(args[i + 1], out var m))
                    monsters = m;
            }

            _host.StartServer(port, monsters);
            DontDestroyOnLoad(gameObject);
        }
    }
}
