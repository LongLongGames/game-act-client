using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameAct.UI
{
    /// <summary>
    /// 房间等待界面：成员列表、邀请、开始（房主）、离开。
    /// </summary>
    public class RuntimeRoomView : MonoBehaviour, IRoomView
    {
        public event Action OnLeaveClicked;
        public event Action OnInviteClicked;
        public event Action OnStartClicked;

        GameObject _root;
        Text _title;
        Text _idLabel;
        Text _members;
        Text _status;
        Button _startBtn;

        public void Build(Transform parent)
        {
            _root = new GameObject("RoomWaiting");
            _root.transform.SetParent(parent, false);
            UiUtil.StretchFull(_root.AddComponent<RectTransform>());

            var dim = _root.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.06f, 0.09f, 1f);

            var panel = UiUtil.CreatePanel(_root.transform, new Vector2(640, 420));
            var panelT = panel.transform;

            _title = UiUtil.CreateLabel(panelT, "房间等待中", 22, new Vector2(0, 170));
            _idLabel = UiUtil.CreateLabel(panelT, "ID: -", 13, new Vector2(0, 135));
            _idLabel.color = new Color(0.6f, 0.65f, 0.7f);

            var membersBg = new GameObject("MembersBg");
            membersBg.transform.SetParent(panelT, false);
            var mrt = membersBg.AddComponent<RectTransform>();
            mrt.sizeDelta = new Vector2(560, 160);
            mrt.anchoredPosition = new Vector2(0, 20);
            var mimg = membersBg.AddComponent<Image>();
            mimg.color = new Color(0.12f, 0.13f, 0.16f, 1f);

            _members = UiUtil.CreateLabel(membersBg.transform, "成员…", 15, Vector2.zero);
            _members.alignment = TextAnchor.UpperLeft;
            var mr = _members.GetComponent<RectTransform>();
            if (mr != null)
            {
                UiUtil.StretchFull(mr);
                mr.offsetMin = new Vector2(16, 12);
                mr.offsetMax = new Vector2(-16, -12);
            }

            _status = UiUtil.CreateLabel(panelT, "等待玩家加入…", 13, new Vector2(0, -90));
            _status.color = new Color(0.7f, 0.85f, 1f);

            AddBarButton(panelT, "邀请", new Vector2(-160, -150), () => OnInviteClicked?.Invoke(), new Color(0.25f, 0.55f, 0.9f));
            _startBtn = UiUtil.CreateButton(panelT, "开始", new Vector2(0, -150), new Color(0.2f, 0.65f, 0.4f), new Vector2(140, 42));
            _startBtn.onClick.AddListener(() => OnStartClicked?.Invoke());
            AddBarButton(panelT, "离开", new Vector2(160, -150), () => OnLeaveClicked?.Invoke(), new Color(0.55f, 0.3f, 0.3f));

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

        public void SetInfo(string roomName, string roomId, string[] memberLines, bool isHost)
        {
            if (_title != null)
                _title.text = string.IsNullOrEmpty(roomName) ? "房间" : roomName;
            if (_idLabel != null)
                _idLabel.text = "ID: " + (roomId ?? "-");
            if (_members != null)
            {
                if (memberLines == null || memberLines.Length == 0)
                    _members.text = "（暂无成员）";
                else
                    _members.text = string.Join("\n", memberLines);
            }
            if (_startBtn != null)
                _startBtn.gameObject.SetActive(isHost);
        }

        public void SetStatus(string text)
        {
            if (_status != null) _status.text = text ?? "";
        }
    }
}
