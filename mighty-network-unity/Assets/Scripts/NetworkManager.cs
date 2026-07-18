using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NativeWebSocket; // 무료 패키지: 에디터/WebGL 모두에서 WebSocket 사용 가능

// ============================================================================
// NetworkManager (03단계: 방 생성/입장)
//  - 서버(ws://localhost:3000)에 순수 WebSocket으로 접속
//  - 닉네임 입력 후 방 만들기 / 방 코드로 입장
//  - 방 안 플레이어 목록을 실시간 표시
//  - 서버가 준 reconnectToken을 저장(PlayerPrefs) - 재접속(11단계) 대비
//
//  사용법: 빈 GameObject에 이 스크립트를 붙이고 Play. (UI는 OnGUI로 자동 표시)
// ============================================================================
public class NetworkManager : MonoBehaviour
{
    [Header("서버 주소 (개발용: localhost)")]
    public string serverUrl = "ws://localhost:3000";

    private WebSocket websocket;
    private string status = "대기 중...";
    private readonly List<string> logLines = new List<string>();
    private Vector2 scroll;

    // 로비 입력값
    private string nickname = "";
    private string roomIdInput = "";
    private string passwordInput = "";

    // 방 상태
    private bool inRoom = false;
    private string myRoomId = "";
    private GameState currentState;

    // ---------- 서버와 주고받는 메시지 형식 (JSON) ----------
    // 공통: { "type": ..., "data": {...} }

    [System.Serializable] private class TypeOnly { public string type; }

    // 보내는 메시지들
    [System.Serializable] private class PingData { public string message; }
    [System.Serializable] private class PingMsg { public string type = "ping_from_client"; public PingData data; }

    [System.Serializable] private class CreateRoomData { public string nickname; public string password; }
    [System.Serializable] private class CreateRoomMsg { public string type = "create_room"; public CreateRoomData data; }

    [System.Serializable] private class JoinRoomData { public string roomId; public string nickname; public string password; }
    [System.Serializable] private class JoinRoomMsg { public string type = "join_room"; public JoinRoomData data; }

    [System.Serializable] private class LeaveRoomMsg { public string type = "leave_room"; public string data = ""; }

    // 받는 메시지들
    [System.Serializable] private class RoomAckData { public string roomId; public string reconnectToken; }
    [System.Serializable] private class RoomAckMsg { public string type; public RoomAckData data; }

    [System.Serializable] private class PlayerInfo { public string nickname; public bool isReady; public bool connected; }
    [System.Serializable] private class GameState { public string roomId; public string status; public PlayerInfo[] players; }
    [System.Serializable] private class GameStateMsg { public string type; public GameState data; }

    [System.Serializable] private class ErrorData { public string message; }
    [System.Serializable] private class ErrorMsg { public string type; public ErrorData data; }

    private async void Start()
    {
        Log("서버에 접속 시도: " + serverUrl);
        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () => { status = "접속됨"; Log("[open] 서버에 접속했습니다."); };
        websocket.OnError += (e) => { status = "오류"; Log("[error] " + e); };
        websocket.OnClose += (e) => { status = "끊김"; inRoom = false; Log("[close] 연결이 끊겼습니다."); };
        websocket.OnMessage += (bytes) => HandleMessage(Encoding.UTF8.GetString(bytes));

        await websocket.Connect();
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    private void HandleMessage(string json)
    {
        TypeOnly head = JsonUtility.FromJson<TypeOnly>(json);
        switch (head.type)
        {
            case "pong_from_server":
                Log("[pong] " + json);
                break;

            case "room_created":
            case "room_joined":
            {
                RoomAckMsg m = JsonUtility.FromJson<RoomAckMsg>(json);
                myRoomId = m.data.roomId;
                inRoom = true;
                // 재접속 토큰 저장 (11단계에서 사용)
                PlayerPrefs.SetString("reconnectToken", m.data.reconnectToken);
                PlayerPrefs.SetString("roomId", m.data.roomId);
                PlayerPrefs.Save();
                Log("[" + head.type + "] 방 코드: " + m.data.roomId + " (토큰 저장됨)");
                break;
            }

            case "game_state":
            {
                GameStateMsg m = JsonUtility.FromJson<GameStateMsg>(json);
                currentState = m.data;
                Log("[game_state] 인원 " + (m.data.players != null ? m.data.players.Length : 0) + "명");
                break;
            }

            case "error_message":
            {
                ErrorMsg m = JsonUtility.FromJson<ErrorMsg>(json);
                Log("[error_message] " + m.data.message);
                break;
            }

            default:
                Log("[recv] " + json);
                break;
        }
    }

    // ---------- 서버로 보내기 ----------
    private async void Send(string json)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Log("아직 접속되지 않았습니다.");
            return;
        }
        await websocket.SendText(json);
    }

    private void SendPing()
    {
        Send(JsonUtility.ToJson(new PingMsg { data = new PingData { message = "hello from Unity" } }));
        Log("[ping] 전송");
    }

    private void CreateRoom()
    {
        if (string.IsNullOrWhiteSpace(nickname)) { Log("닉네임을 입력하세요."); return; }
        Send(JsonUtility.ToJson(new CreateRoomMsg { data = new CreateRoomData { nickname = nickname, password = passwordInput } }));
        Log("[create_room] 전송: " + nickname);
    }

    private void JoinRoom()
    {
        if (string.IsNullOrWhiteSpace(nickname)) { Log("닉네임을 입력하세요."); return; }
        if (string.IsNullOrWhiteSpace(roomIdInput)) { Log("방 코드를 입력하세요."); return; }
        Send(JsonUtility.ToJson(new JoinRoomMsg { data = new JoinRoomData { roomId = roomIdInput.ToUpper(), nickname = nickname, password = passwordInput } }));
        Log("[join_room] 전송: " + roomIdInput.ToUpper());
    }

    private void LeaveRoom()
    {
        Send(JsonUtility.ToJson(new LeaveRoomMsg()));
        inRoom = false;
        currentState = null;
        Log("[leave_room] 전송");
    }

    private void Log(string line)
    {
        Debug.Log("[NetworkManager] " + line);
        logLines.Add(line);
        if (logLines.Count > 100) logLines.RemoveAt(0);
        scroll.y = float.MaxValue;
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null) await websocket.Close();
    }

    // ---------- 화면 UI (씬 세팅 없이 자동 표시) ----------
    private void OnGUI()
    {
        GUI.skin.label.fontSize = 15;
        GUI.skin.button.fontSize = 15;
        GUI.skin.textField.fontSize = 15;

        GUILayout.BeginArea(new Rect(20, 20, 500, 560), GUI.skin.box);

        GUILayout.Label("Mighty - 방 테스트");
        GUILayout.Label("연결 상태: " + status);
        GUILayout.Space(6);

        if (!inRoom)
        {
            // ---- 로비 화면 ----
            GUILayout.BeginHorizontal();
            GUILayout.Label("닉네임:", GUILayout.Width(70));
            nickname = GUILayout.TextField(nickname, 12, GUILayout.Width(180));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("방 코드:", GUILayout.Width(70));
            roomIdInput = GUILayout.TextField(roomIdInput, 4, GUILayout.Width(100));
            GUILayout.Label("비번:", GUILayout.Width(45));
            passwordInput = GUILayout.TextField(passwordInput, GUILayout.Width(120));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("방 만들기", GUILayout.Height(38))) CreateRoom();
            if (GUILayout.Button("입장", GUILayout.Height(38))) JoinRoom();
            GUILayout.EndHorizontal();
        }
        else
        {
            // ---- 방 안 화면 ----
            GUILayout.Label("방 코드: " + myRoomId
                + (currentState != null ? "   (상태: " + currentState.status + ")" : ""));

            int count = (currentState != null && currentState.players != null) ? currentState.players.Length : 0;
            GUILayout.Label("플레이어 (" + count + "/5):");
            if (currentState != null && currentState.players != null)
            {
                foreach (PlayerInfo p in currentState.players)
                {
                    GUILayout.Label("  - " + p.nickname
                        + (p.isReady ? " [준비]" : " [대기]")
                        + (p.connected ? "" : " (연결끊김)"));
                }
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ping")) SendPing();
            if (GUILayout.Button("방 나가기")) LeaveRoom();
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);
        GUILayout.Label("로그:");
        scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.Height(240));
        foreach (string line in logLines) GUILayout.Label(line);
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }
}
