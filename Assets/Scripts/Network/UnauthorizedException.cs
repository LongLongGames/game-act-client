using System;

namespace GameAct.Network
{
    /// <summary>
    /// HTTP 401：Token 无效或过期。HTTP 层抛出，业务层应登出并回登录页。
    /// 见 ADR-0004。
    /// </summary>
    public sealed class UnauthorizedException : Exception
    {
        public long ResponseCode { get; }

        public UnauthorizedException(long responseCode, string message)
            : base(message)
        {
            ResponseCode = responseCode;
        }
    }
}
