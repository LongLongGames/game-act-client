using System.Threading;
using Cysharp.Threading.Tasks;
using GameAct.UI;

namespace GameAct.Lobby
{
    public interface ILobbyService
    {
        UniTask<(bool ok, LobbyRoomDto[] rooms, string error)> ListAsync(CancellationToken ct = default);
        UniTask<(bool ok, LobbyRoomDto room, string error)> CreateAsync(string name, string hostAddress, int hostPort, int maxPlayers = 4, CancellationToken ct = default);
        UniTask<(bool ok, LobbyRoomDto room, string error)> JoinAsync(string roomId, CancellationToken ct = default);
        UniTask<(bool ok, string error)> LeaveAsync(string roomId, CancellationToken ct = default);
        UniTask<(bool ok, LobbyRoomDto room, string error)> HeartbeatAsync(string roomId, string hostAddress, int hostPort, string status = null, CancellationToken ct = default);
        UniTask<(bool ok, LobbyRoomDto room, string error)> GetAsync(string roomId, CancellationToken ct = default);

        RoomListItem[] ToListItems(LobbyRoomDto[] rooms);
    }
}
