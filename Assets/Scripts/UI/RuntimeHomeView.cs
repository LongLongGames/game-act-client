using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GameAct.Network;

namespace GameAct.UI
{
    /// <summary>
    /// 横屏主菜单 + 独立 ServerList（整页房间 ScrollView）。
    /// ServerList：左上返回；上方 刷新/创建/邀请；主体为房间列表。
    /// </summary>
    public class RuntimeHomeView : MonoBehaviour, IHomeView
    {
        public event Action OnSinglePlayerClicked;
        public event Action OnMultiplayerClicked;
        public event Action OnAchievementsClicked;
        public event Action OnSettingsClicked;
        public event Action OnExitClicked;

        public event Action OnServerListBackClicked;
        public event Action OnRefreshServerListClicked;
        public event Action OnCreateRoomClicked;
        public event Action OnInviteClicked;
        public event Action<string> OnJoinRoomClicked;

        public event Action OnRoomLeaveClicked;
        public event Action OnRoomInviteClicked;
        public event Action OnRoomStartClicked;

        GameObject _root;
        GameObject _menuRoot;
        GameObject _serverListRoot;

        Text _versionLabel;
        Text _userLabel;
        Text _status;
        Text _serverListStatus;

        Transform _roomContent;
        GameObject _roomWaitingRoot;
        Text _roomTitle;
        Text _roomIdLabel;
        Text _roomMembers;
        Text _roomWaitStatus;
        Button _roomStartBtn;
        readonly List<GameObject> _roomRows = new List<GameObject>();

        public void Build(Transform canvasRoot)
        {
            _root = new GameObject("HomeRoot");
            _root.transform.SetParent(canvasRoot, false);
            var rt = _root.AddComponent<RectTransform>();
            StretchFull(rt);

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.1f, 0.98f);

            BuildTopBar(_root.transform);
            BuildMainMenu(_root.transform);
            BuildServerList(_root.transform);
            BuildRoomWaiting(_root.transform);

            _status = CreateAnchoredLabel(_root.transform, "", 14,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 20), new Vector2(900, 32));
            _status.alignment = TextAnchor.MiddleCenter;
            _status.color = new Color(0.8f, 0.82f, 0.88f);

            ShowServerList(false);
            _root.SetActive(false);
        }

        void BuildTopBar(Transform parent)
        {
            _versionLabel = CreateAnchoredLabel(parent, "客户端 v?  |  资源 ?", 14,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(16, -12), new Vector2(480, 32));
            _versionLabel.alignment = TextAnchor.MiddleLeft;
            _versionLabel.color = new Color(0.65f, 0.7f, 0.78f);

            _userLabel = CreateAnchoredLabel(parent, "未登录", 16,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-16, -12), new Vector2(360, 32));
            _userLabel.alignment = TextAnchor.MiddleRight;
            _userLabel.color = new Color(0.9f, 0.92f, 0.95f);
        }

        void BuildMainMenu(Transform parent)
        {
            _menuRoot = new GameObject("MainMenu");
            _menuRoot.transform.SetParent(parent, false);
            StretchFull(_menuRoot.AddComponent<RectTransform>());

            var title = CreateLabel(_menuRoot.transform, "GAME ACT", 36, new Vector2(0, 140));
            title.fontStyle = FontStyle.Bold;

            var subtitle = CreateLabel(_menuRoot.transform, "主菜单", 16, new Vector2(0, 95));
            subtitle.color = new Color(0.6f, 0.65f, 0.75f);

            float y = 30;
            float step = -58;
            AddMenuButton(_menuRoot.transform, "单  人", new Vector2(0, y), () => OnSinglePlayerClicked?.Invoke(), new Color(0.22f, 0.48f, 0.85f));
            y += step;
            AddMenuButton(_menuRoot.transform, "多  人", new Vector2(0, y), () => OnMultiplayerClicked?.Invoke(), new Color(0.2f, 0.62f, 0.45f));
            y += step;
            AddMenuButton(_menuRoot.transform, "成  就", new Vector2(0, y), () => OnAchievementsClicked?.Invoke(), new Color(0.45f, 0.4f, 0.7f));
            y += step;
            AddMenuButton(_menuRoot.transform, "设  置", new Vector2(0, y), () => OnSettingsClicked?.Invoke(), new Color(0.4f, 0.42f, 0.48f));
            y += step;
            AddMenuButton(_menuRoot.transform, "退  出", new Vector2(0, y), () => OnExitClicked?.Invoke(), new Color(0.65f, 0.28f, 0.28f));
        }

        void BuildServerList(Transform parent)
        {
            _serverListRoot = new GameObject("ServerList");
            _serverListRoot.transform.SetParent(parent, false);
            StretchFull(_serverListRoot.AddComponent<RectTransform>());

            var dim = _serverListRoot.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.06f, 0.09f, 1f);

            // 左上：返回
            var backBtn = CreateButton(_serverListRoot.transform, "返回", Vector2.zero,
                new Color(0.35f, 0.38f, 0.45f), new Vector2(100, 40));
            var backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = backRt.anchorMax = backRt.pivot = new Vector2(0, 1);
            backRt.anchoredPosition = new Vector2(24, -56);
            backBtn.onClick.AddListener(() => OnServerListBackClicked?.Invoke());

            // 标题
            var title = CreateAnchoredLabel(_serverListRoot.transform, "多人大厅", 22,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -58), new Vector2(300, 40));
            title.alignment = TextAnchor.MiddleCenter;

            // 上方操作：刷新 / 创建 / 邀请
            var bar = new GameObject("ActionBar");
            bar.transform.SetParent(_serverListRoot.transform, false);
            var barRt = bar.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.5f, 1);
            barRt.anchorMax = new Vector2(0.5f, 1);
            barRt.pivot = new Vector2(0.5f, 1);
            barRt.anchoredPosition = new Vector2(0, -110);
            barRt.sizeDelta = new Vector2(640, 48);

            AddBarButton(bar.transform, "刷新", new Vector2(-180, 0), () => OnRefreshServerListClicked?.Invoke(), new Color(0.3f, 0.45f, 0.7f));
            AddBarButton(bar.transform, "创建", new Vector2(0, 0), () => OnCreateRoomClicked?.Invoke(), new Color(0.2f, 0.65f, 0.4f));
            AddBarButton(bar.transform, "邀请", new Vector2(180, 0), () => OnInviteClicked?.Invoke(), new Color(0.25f, 0.55f, 0.9f));

            // 状态行
            _serverListStatus = CreateAnchoredLabel(_serverListRoot.transform, "", 13,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -165), new Vector2(800, 28));
            _serverListStatus.alignment = TextAnchor.MiddleCenter;
            _serverListStatus.color = new Color(0.65f, 0.7f, 0.78f);

            // 整页主体：ScrollView 房间列表
            var scrollGo = new GameObject("RoomScroll");
            scrollGo.transform.SetParent(_serverListRoot.transform, false);
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.5f, 0);
            scrollRt.anchorMax = new Vector2(0.5f, 1);
            scrollRt.pivot = new Vector2(0.5f, 0.5f);
            scrollRt.offsetMin = new Vector2(-420, 40);
            scrollRt.offsetMax = new Vector2(420, -200);

            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0.1f, 0.11f, 0.14f, 1f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.AddComponent<RectTransform>();
            StretchFull(vpRt);
            viewport.AddComponent<RectMask2D>();
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(1, 1, 1, 0.02f);

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0, 0);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vpRt;
            scroll.content = contentRt;
            _roomContent = content.transform;

            // 默认空列表提示
            SetRoomList(Array.Empty<RoomListItem>());
        }

        public void Show()
        {
            if (_root != null) _root.SetActive(true);
            ShowServerList(false);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void ShowServerList(bool show)
        {
            if (_serverListRoot != null) _serverListRoot.SetActive(show);
            if (_menuRoot != null) _menuRoot.SetActive(!show);
            if (show && _roomWaitingRoot != null) _roomWaitingRoot.SetActive(false);
        }

        public void SetVersions(string clientVersion, string resourceVersion)
        {
            if (_versionLabel == null) return;
            _versionLabel.text = $"客户端 {clientVersion ?? "?"}  |  资源 {resourceVersion ?? "?"}";
        }

        public void SetUserName(string name)
        {
            if (_userLabel == null) return;
            _userLabel.text = string.IsNullOrEmpty(name) ? "未登录" : name;
        }

        public void SetStatus(string text)
        {
            if (_status != null) _status.text = text ?? "";
        }

        public void SetServerListStatus(string text)
        {
            if (_serverListStatus != null) _serverListStatus.text = text ?? "";
        }

        public void ShowProfile(PlayerProfile profile)
        {
            if (profile == null)
            {
                SetUserName("游客");
                return;
            }
            var name = !string.IsNullOrEmpty(profile.nickname) ? profile.nickname : profile.id;
            if (profile.level > 0)
                name = $"{name}  Lv.{profile.level}";
            SetUserName(name);
            SetStatus("资料已加载");
        }


        void BuildRoomWaiting(Transform parent)
        {
            _roomWaitingRoot = new GameObject("RoomWaiting");
            _roomWaitingRoot.transform.SetParent(parent, false);
            StretchFull(_roomWaitingRoot.AddComponent<RectTransform>());

            var dim = _roomWaitingRoot.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.06f, 0.09f, 1f);

            var panel = CreatePanel(_roomWaitingRoot.transform, new Vector2(640, 420));
            var panelT = panel.transform;

            _roomTitle = CreateLabel(panelT, "房间等待中", 22, new Vector2(0, 170));
            _roomIdLabel = CreateLabel(panelT, "ID: -", 13, new Vector2(0, 135));
            _roomIdLabel.color = new Color(0.6f, 0.65f, 0.7f);

            var membersBg = new GameObject("MembersBg");
            membersBg.transform.SetParent(panelT, false);
            var mrt = membersBg.AddComponent<RectTransform>();
            mrt.sizeDelta = new Vector2(560, 160);
            mrt.anchoredPosition = new Vector2(0, 20);
            var mimg = membersBg.AddComponent<Image>();
            mimg.color = new Color(0.12f, 0.13f, 0.16f, 1f);

            _roomMembers = CreateLabel(membersBg.transform, "成员…", 15, Vector2.zero);
            _roomMembers.alignment = TextAnchor.UpperLeft;
            var mr = _roomMembers.GetComponent<RectTransform>();
            if (mr != null)
            {
                StretchFull(mr);
                mr.offsetMin = new Vector2(16, 12);
                mr.offsetMax = new Vector2(-16, -12);
            }

            _roomWaitStatus = CreateLabel(panelT, "等待玩家加入…", 13, new Vector2(0, -90));
            _roomWaitStatus.color = new Color(0.7f, 0.85f, 1f);

            AddBarButton(panelT, "邀请", new Vector2(-160, -150), () => OnRoomInviteClicked?.Invoke(), new Color(0.25f, 0.55f, 0.9f));
            _roomStartBtn = CreateButton(panelT, "开始", new Vector2(0, -150), new Color(0.2f, 0.65f, 0.4f), new Vector2(140, 42));
            _roomStartBtn.onClick.AddListener(() => OnRoomStartClicked?.Invoke());
            AddBarButton(panelT, "离开", new Vector2(160, -150), () => OnRoomLeaveClicked?.Invoke(), new Color(0.55f, 0.3f, 0.3f));

            _roomWaitingRoot.SetActive(false);
        }

        public void ShowRoomWaiting(bool show)
        {
            if (_roomWaitingRoot != null) _roomWaitingRoot.SetActive(show);
            if (show)
            {
                if (_serverListRoot != null) _serverListRoot.SetActive(false);
                if (_menuRoot != null) _menuRoot.SetActive(false);
            }
        }

        public void SetRoomWaitingInfo(string roomName, string roomId, string[] memberLines, bool isHost)
        {
            if (_roomTitle != null)
                _roomTitle.text = string.IsNullOrEmpty(roomName) ? "房间" : roomName;
            if (_roomIdLabel != null)
                _roomIdLabel.text = "ID: " + (roomId ?? "-");
            if (_roomMembers != null)
            {
                if (memberLines == null || memberLines.Length == 0)
                    _roomMembers.text = "（暂无成员）";
                else
                    _roomMembers.text = string.Join("\n", memberLines);
            }
            if (_roomStartBtn != null)
                _roomStartBtn.gameObject.SetActive(isHost);
        }

        public void SetRoomWaitingStatus(string text)
        {
            if (_roomWaitStatus != null) _roomWaitStatus.text = text ?? "";
        }

        public void SetRoomList(RoomListItem[] rooms)
        {
            foreach (var go in _roomRows)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            _roomRows.Clear();

            if (_roomContent == null) return;

            if (rooms == null || rooms.Length == 0)
            {
                var empty = CreateRoomRow(_roomContent, null, "暂无房间", "点击【创建】开一局，或【刷新】拉取列表", false);
                _roomRows.Add(empty);
                return;
            }

            foreach (var room in rooms)
            {
                if (room == null) continue;
                var sub = string.IsNullOrEmpty(room.subtitle)
                    ? $"{room.players}/{room.maxPlayers}"
                    : $"{room.subtitle}  ·  {room.players}/{room.maxPlayers}";
                var row = CreateRoomRow(_roomContent, room.id, room.title ?? room.id, sub, true);
                _roomRows.Add(row);
            }
        }

        GameObject CreateRoomRow(Transform parent, string roomId, string title, string subtitle, bool joinable)
        {
            var go = new GameObject("Room_" + (roomId ?? "empty"));
            go.transform.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 64;
            le.preferredHeight = 64;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.18f, 0.22f, 1f);

            var titleT = CreateLabel(go.transform, title, 16, new Vector2(-80, 10));
            titleT.alignment = TextAnchor.MiddleLeft;
            var tr = titleT.GetComponent<RectTransform>();
            if (tr != null)
            {
                tr.anchorMin = new Vector2(0, 0.5f);
                tr.anchorMax = new Vector2(1, 0.5f);
                tr.pivot = new Vector2(0, 0.5f);
                tr.anchoredPosition = new Vector2(16, 10);
                tr.sizeDelta = new Vector2(-160, 28);
            }

            var subT = CreateLabel(go.transform, subtitle, 12, new Vector2(-80, -14));
            subT.alignment = TextAnchor.MiddleLeft;
            subT.color = new Color(0.6f, 0.65f, 0.7f);
            var sr = subT.GetComponent<RectTransform>();
            if (sr != null)
            {
                sr.anchorMin = new Vector2(0, 0.5f);
                sr.anchorMax = new Vector2(1, 0.5f);
                sr.pivot = new Vector2(0, 0.5f);
                sr.anchoredPosition = new Vector2(16, -14);
                sr.sizeDelta = new Vector2(-160, 22);
            }

            if (joinable && !string.IsNullOrEmpty(roomId))
            {
                var joinBtn = CreateButton(go.transform, "加入", Vector2.zero,
                    new Color(0.25f, 0.55f, 0.85f), new Vector2(88, 36));
                var jr = joinBtn.GetComponent<RectTransform>();
                jr.anchorMin = jr.anchorMax = jr.pivot = new Vector2(1, 0.5f);
                jr.anchoredPosition = new Vector2(-16, 0);
                var captured = roomId;
                joinBtn.onClick.AddListener(() => OnJoinRoomClicked?.Invoke(captured));
            }

            return go;
        }

        void AddMenuButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, Color color)
        {
            var btn = CreateButton(parent, label, pos, color, new Vector2(280, 48));
            btn.onClick.AddListener(onClick);
        }

        void AddBarButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, Color color)
        {
            var btn = CreateButton(parent, label, pos, color, new Vector2(140, 42));
            btn.onClick.AddListener(onClick);
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Text CreateLabel(Transform parent, string text, int fontSize, Vector2 pos)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700, 40);
            rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.font = BuiltinFont();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Text CreateAnchoredLabel(Transform parent, string text, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = BuiltinFont();
            t.text = text;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Button CreateButton(Transform parent, string label, Vector2 pos, Color color, Vector2 size)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            StretchFull(trt);
            var t = textGo.AddComponent<Text>();
            t.font = BuiltinFont();
            t.text = label;
            t.fontSize = 18;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return btn;
        }

        static GameObject CreatePanel(Transform parent, Vector2 size)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.14f, 0.15f, 0.19f, 1f);
            return go;
        }

        static Font BuiltinFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
