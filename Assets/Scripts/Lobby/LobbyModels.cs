using System;

namespace GameAct.Lobby
{
    [Serializable]
    public class LobbyRoomDto
    {
        public string room_id;
        public string name;
        public string host_user_id;
        public int max_players;
        public int player_count;
        public string status;
        public string host_address;
        public int host_port;
        public string created_at;
        public string updated_at;
        public string[] player_ids;
    }

    [Serializable]
    public class LobbyListResponse
    {
        public LobbyRoomDto[] rooms;
    }

    [Serializable]
    public class CreateRoomBody
    {
        public string name;
        public int max_players = 4;
        public string host_address;
        public int host_port = 9050;
    }

    [Serializable]
    public class RoomIdBody
    {
        public string room_id;
    }

    [Serializable]
    public class HeartbeatBody
    {
        public string room_id;
        public string host_address;
        public int host_port;
        public string status;
    }
}
