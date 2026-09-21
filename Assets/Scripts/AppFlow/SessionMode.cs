namespace GameAct.AppFlow
{
    /// <summary>
    /// 进局模式：由入口显式声明，禁止用残留 NetSession 状态猜测。
    /// Solo：无网络运行时（Disconnect + 不 Poll）。
    /// Host / Client：必须网络就绪，否则失败回房间，禁止降级单机模拟。
    /// </summary>
    public enum SessionMode
    {
        Solo,
        Host,
        Client
    }
}
