using System;
using UnityEngine;
using UnityEngine.UI;
using GameAct.Network;

namespace GameAct.UI
{
    /// <summary>
    /// P1 Home：资料 + Steam Lobby + LiteNet Host/Join 简易按钮。
    /// </summary>
    public class RuntimeHomeView : MonoBehaviour, IHomeView
    {
        public event Action OnLogoutClicked;
        public event Action OnCreateLobbyClicked;
        public event Action OnInviteFriendsClicked;
        public event Action OnStartHostClicked;
        public event Action OnConnectLocalClicked;
        public event Action OnDisconnectNetClicked;

        GameObject _root;
        Text _status;
        Text _profile;
        Text _steamStatus;
        Text _netStatus;
        Text _lobbyIdLabel;

        public void Build(Transform canvasRoot)
        {
            _root = new GameObject("HomePanel");
            _root.transform.SetParent(canvasRoot, false);
            var rt = _root.AddComponent<RectTransform>();
            StretchFull(rt);

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

            // 更大面板容纳 P1 按钮
            var panel = CreatePanel(_root.transform, new Vector2(560, 520));
            var panelT = panel.transform;

            CreateLabel(panelT, "Home / 联机基础 (P1)", 22, new Vector2(0, 220));

            _profile = CreateLabel(panelT, "", 14, new Vector2(0, 150));
            _profile.alignment = TextAnchor.UpperLeft;
            var prt = _profile.GetComponent<RectTransform>();
            if (prt != null) prt.sizeDelta = new Vector2(500, 80);

            _steamStatus = CreateLabel(panelT, "Steam: …", 13, new Vector2(0, 70));
            _steamStatus.color = new Color(0.7f, 0.85f, 1f);
            _lobbyIdLabel = CreateLabel(panelT, "Lobby: -", 13, new Vector2(0, 40));
            _lobbyIdLabel.color = new Color(0.7f, 0.85f, 1f);

            _netStatus = CreateLabel(panelT, "Net: Idle", 13, new Vector2(0, 10));
            _netStatus.color = new Color(0.7f, 1f, 0.75f);

            // 按钮行
            var btnLobby = CreateButton(panelT, "创建Lobby", new Vector2(-160, -50), new Color(0.25f, 0.55f, 0.9f));
            btnLobby.onClick.AddListener(() => OnCreateLobbyClicked?.Invoke());

            var btnInvite = CreateButton(panelT, "邀请好友", new Vector2(0, -50), new Color(0.25f, 0.55f, 0.9f));
            btnInvite.onClick.AddListener(() => OnInviteFriendsClicked?.Invoke());

            var btnHost = CreateButton(panelT, "Host UDP", new Vector2(160, -50), new Color(0.25f, 0.7f, 0.4f));
            btnHost.onClick.AddListener(() => OnStartHostClicked?.Invoke());

            var btnJoin = CreateButton(panelT, "Join 本机", new Vector2(-80, -110), new Color(0.25f, 0.7f, 0.4f));
            btnJoin.onClick.AddListener(() => OnConnectLocalClicked?.Invoke());

            var btnDisc = CreateButton(panelT, "断网", new Vector2(80, -110), new Color(0.6f, 0.45f, 0.2f));
            btnDisc.onClick.AddListener(() => OnDisconnectNetClicked?.Invoke());

            _status = CreateLabel(panelT, "", 13, new Vector2(0, -170));
            _status.color = new Color(0.85f, 0.85f, 0.9f);
            var srt = _status.GetComponent<RectTransform>();
            if (srt != null) srt.sizeDelta = new Vector2(500, 50);

            var btnLogout = CreateButton(panelT, "登出", new Vector2(0, -220), new Color(0.7f, 0.25f, 0.25f));
            btnLogout.onClick.AddListener(() => OnLogoutClicked?.Invoke());

            _root.SetActive(false);
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

        public void ShowProfile(PlayerProfile profile)
        {
            if (_profile == null) return;
            if (profile == null)
            {
                _profile.text = "(无资料)";
                return;
            }
            _profile.text =
                $"nickname : {profile.nickname}   level : {profile.level}\n" +
                $"game_id  : {profile.game_id}   mp_id : {profile.mp_account_id}\n" +
                $"id       : {profile.id}";
            SetStatus("资料已加载");
        }

        public void SetSteamStatus(string text)
        {
            if (_steamStatus != null) _steamStatus.text = "Steam: " + (text ?? "");
        }

        public void SetNetStatus(string text)
        {
            if (_netStatus != null) _netStatus.text = "Net: " + (text ?? "");
        }

        public void SetLobbyId(ulong lobbyId)
        {
            if (_lobbyIdLabel != null)
                _lobbyIdLabel.text = lobbyId == 0 ? "Lobby: -" : "Lobby: " + lobbyId;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static GameObject CreatePanel(Transform parent, Vector2 size)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.16f, 0.2f, 1f);
            return go;
        }

        static Text CreateLabel(Transform parent, string text, int fontSize, Vector2 pos)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500, 36);
            rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null)
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Button CreateButton(Transform parent, string label, Vector2 pos, Color? color = null)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(140, 36);
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = color ?? new Color(0.7f, 0.25f, 0.25f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            StretchFull(trt);
            var t = textGo.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null)
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = label;
            t.fontSize = 15;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return btn;
        }
    }
}
