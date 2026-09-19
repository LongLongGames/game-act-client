using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GameAct.Network;
using GameAct.UI;

namespace GameAct.Lobby
{
    /// <summary>
    /// 对接 game-lobby：Create / List / Join / Leave / Heartbeat。
    /// Gateway 基址与 JWT 与游戏服一致。
    /// </summary>
    public class LobbyService : ILobbyService
    {
        readonly IHttpClient _http;
        readonly ApiConfig _config;

        public LobbyService(IHttpClient http, ApiConfig config)
        {
            _http = http;
            _config = config;
        }

        string Base => _config.GameBaseUrl.TrimEnd('/');

        public async UniTask<(bool ok, LobbyRoomDto[] rooms, string error)> ListAsync(CancellationToken ct = default)
        {
            try
            {
                var text = await _http.GetAsync(Base + "/api/v1/lobby/list", auth: false, ct);
                var resp = JsonUtility.FromJson<LobbyListResponse>(WrapArray(text));
                // 服务端返回 { "rooms": [ ... ] }，JsonUtility 需要包装；若已是对象则直接解
                if (resp == null)
                    resp = JsonUtility.FromJson<LobbyListResponse>(text);
                var rooms = resp?.rooms ?? Array.Empty<LobbyRoomDto>();
                return (true, rooms, null);
            }
            catch (Exception e)
            {
                return (false, Array.Empty<LobbyRoomDto>(), e.Message);
            }
        }

        public async UniTask<(bool ok, LobbyRoomDto room, string error)> CreateAsync(
            string name, string hostAddress, int hostPort, int maxPlayers = 4, CancellationToken ct = default)
        {
            var body = new CreateRoomBody
            {
                name = name,
                max_players = maxPlayers,
                host_address = hostAddress,
                host_port = hostPort
            };
            try
            {
                var text = await _http.PostJsonAsync(Base + "/api/v1/lobby/create", JsonUtility.ToJson(body), auth: true, ct);
                var room = JsonUtility.FromJson<LobbyRoomDto>(text);
                if (room == null || string.IsNullOrEmpty(room.room_id))
                    return (false, null, "create: empty room");
                return (true, room, null);
            }
            catch (Exception e)
            {
                return (false, null, e.Message);
            }
        }

        public async UniTask<(bool ok, LobbyRoomDto room, string error)> JoinAsync(string roomId, CancellationToken ct = default)
        {
            var body = new RoomIdBody { room_id = roomId };
            try
            {
                var text = await _http.PostJsonAsync(Base + "/api/v1/lobby/join", JsonUtility.ToJson(body), auth: true, ct);
                var room = JsonUtility.FromJson<LobbyRoomDto>(text);
                if (room == null) return (false, null, "join: empty");
                return (true, room, null);
            }
            catch (Exception e)
            {
                return (false, null, e.Message);
            }
        }

        public async UniTask<(bool ok, string error)> LeaveAsync(string roomId, CancellationToken ct = default)
        {
            var body = new RoomIdBody { room_id = roomId };
            try
            {
                await _http.PostJsonAsync(Base + "/api/v1/lobby/leave", JsonUtility.ToJson(body), auth: true, ct);
                return (true, null);
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        }

        public async UniTask<(bool ok, LobbyRoomDto room, string error)> HeartbeatAsync(
            string roomId, string hostAddress, int hostPort, string status = null, CancellationToken ct = default)
        {
            var body = new HeartbeatBody
            {
                room_id = roomId,
                host_address = hostAddress,
                host_port = hostPort,
                status = status
            };
            try
            {
                var text = await _http.PostJsonAsync(Base + "/api/v1/lobby/heartbeat", JsonUtility.ToJson(body), auth: true, ct);
                var room = JsonUtility.FromJson<LobbyRoomDto>(text);
                return (true, room, null);
            }
            catch (Exception e)
            {
                return (false, null, e.Message);
            }
        }

        public async UniTask<(bool ok, LobbyRoomDto room, string error)> GetAsync(string roomId, CancellationToken ct = default)
        {
            try
            {
                var text = await _http.GetAsync(Base + "/api/v1/lobby/" + roomId, auth: false, ct);
                var room = JsonUtility.FromJson<LobbyRoomDto>(text);
                if (room == null) return (false, null, "empty");
                return (true, room, null);
            }
            catch (Exception e)
            {
                return (false, null, e.Message);
            }
        }

        public RoomListItem[] ToListItems(LobbyRoomDto[] rooms)
        {
            if (rooms == null || rooms.Length == 0)
                return Array.Empty<RoomListItem>();

            var items = new RoomListItem[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                var r = rooms[i];
                items[i] = new RoomListItem
                {
                    id = r.room_id,
                    title = string.IsNullOrEmpty(r.name) ? r.room_id : r.name,
                    subtitle = r.status + (string.IsNullOrEmpty(r.host_address) ? "" : " · " + r.host_address),
                    players = r.player_count,
                    maxPlayers = r.max_players > 0 ? r.max_players : 4
                };
            }
            return items;
        }

        // JsonUtility 无法直接解根数组；list 已是对象无需包装。保留兼容。
        static string WrapArray(string text)
        {
            if (string.IsNullOrEmpty(text)) return "{\"rooms\":[]}";
            var t = text.Trim();
            if (t.StartsWith("["))
                return "{\"rooms\":" + t + "}";
            return text;
        }
    }
}
