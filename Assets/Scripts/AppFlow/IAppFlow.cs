using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameAct.AppFlow
{
    public interface IAppFlow
    {
        AppState State { get; }
        UniTask StartAsync(CancellationToken ct = default);
    }
}
