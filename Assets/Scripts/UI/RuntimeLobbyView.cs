using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameAct.UI
{
    /// <summary>
    /// 多人大厅 / ServerList：返回、刷新、创建、房间列表。
    /// 已移除大厅「邀请」按钮（邀请仅在房间内）。
    /// </summary>
    public class RuntimeLobbyView : MonoBehaviour, ILobbyView
    {
        public event Action OnBackClicked;
        public event Action OnRefreshClicked;
        public event Action OnCreateRoomClicked;
        public event Action<string> OnJoinRoomClicked;

        GameObject _root;
        Text _status;
        Transform _roomContent;
        readonly List<GameObject> _roomRows = new List<GameObject>();

        public void Build(Transform parent)
        {
            _root = new GameObject("Lobby");
            _root.transform.SetParent(parent, false);
            UiUtil.StretchFull(_root.AddComponent<RectTransform>());

            var dim = _root.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.06f, 0.09f, 1f);

            // 左上：返回
            var backBtn = UiUtil.CreateButton(_root.transform, "返回", Vector2.zero,
                new Color(0.35f, 0.38f, 0.45f), new Vector2(100, 40));
            var backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = backRt.anchorMax = backRt.pivot = new Vector2(0, 1);
            backRt.anchoredPosition = new Vector2(24, -56);
            backBtn.onClick.AddListener(() => OnBackClicked?.Invoke());

            // 标题
            var title = UiUtil.CreateAnchoredLabel(_root.transform, "多人大厅", 22,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -58), new Vector2(300, 40));
            title.alignment = TextAnchor.MiddleCenter;

            // 上方操作：刷新 / 创建（已去掉邀请）
            var bar = new GameObject("ActionBar");
            bar.transform.SetParent(_root.transform, false);
            var barRt = bar.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.5f, 1);
            barRt.anchorMax = new Vector2(0.5f, 1);
            barRt.pivot = new Vector2(0.5f, 1);
            barRt.anchoredPosition = new Vector2(0, -110);
            barRt.sizeDelta = new Vector2(400, 48);

            AddBarButton(bar.transform, "刷新", new Vector2(-100, 0), () => OnRefreshClicked?.Invoke(), new Color(0.3f, 0.45f, 0.7f));
            AddBarButton(bar.transform, "创建", new Vector2(100, 0), () => OnCreateRoomClicked?.Invoke(), new Color(0.2f, 0.65f, 0.4f));

            // 状态行
            _status = UiUtil.CreateAnchoredLabel(_root.transform, "", 13,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -165), new Vector2(800, 28));
            _status.alignment = TextAnchor.MiddleCenter;
            _status.color = new Color(0.65f, 0.7f, 0.78f);

            // ScrollView 房间列表
            var scrollGo = new GameObject("RoomScroll");
            scrollGo.transform.SetParent(_root.transform, false);
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
            UiUtil.StretchFull(vpRt);
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

            SetRoomList(Array.Empty<RoomListItem>());
            _root.SetActive(false);
        }

        void AddBarButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, Color color)
        {
            var btn = UiUtil.CreateButton(parent, label, pos, color, new Vector2(140, 42));
            btn.onClick.AddListener(onClick);
        }

        public void Show()
        {
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void SetStatus(string text)
        {
            if (_status != null) _status.text = text ?? "";
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

            var titleT = UiUtil.CreateLabel(go.transform, title, 16, new Vector2(-80, 10));
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

            var subT = UiUtil.CreateLabel(go.transform, subtitle, 12, new Vector2(-80, -14));
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
                var joinBtn = UiUtil.CreateButton(go.transform, "加入", Vector2.zero,
                    new Color(0.25f, 0.55f, 0.85f), new Vector2(88, 36));
                var jr = joinBtn.GetComponent<RectTransform>();
                jr.anchorMin = jr.anchorMax = jr.pivot = new Vector2(1, 0.5f);
                jr.anchoredPosition = new Vector2(-16, 0);
                var captured = roomId;
                joinBtn.onClick.AddListener(() => OnJoinRoomClicked?.Invoke(captured));
            }

            return go;
        }
    }
}
