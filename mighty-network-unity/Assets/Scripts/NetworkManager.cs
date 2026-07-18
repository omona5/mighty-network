using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NativeWebSocket; // 무료 패키지: 에디터/WebGL 모두에서 WebSocket 사용 가능

// ============================================================================
// NetworkManager
//  - 서버(ws://localhost:3000)에 순수 WebSocket으로 접속한다.
//  - "Ping 보내기" 버튼을 누르면 서버에 ping 메시지를 보낸다.
//  - 서버가 pong 응답을 보내면 화면 로그에 표시한다.
//
//  사용법: 빈 GameObject에 이 스크립트를 붙이고 Play를 누르면 된다.
//         (화면 UI는 OnGUI로 자동으로 그려지므로 별도 씬 설정이 필요 없다.)
// ============================================================================
public class NetworkManager : MonoBehaviour
{
    [Header("서버 주소 (개발용: localhost)")]
    public string serverUrl = "ws://localhost:3000";

    private WebSocket websocket;
    private string status = "대기 중...";
    private readonly List<string> logLines = new List<string>();
    private Vector2 scroll;

    // ---- 서버와 주고받는 메시지 형식 (JSON) ----
    // { "type": "이벤트이름", "data": { ... } }
    [System.Serializable]
    private class PingData
    {
        public string message;
    }

    [System.Serializable]
    private class ClientMessage
    {
        public string type;
        public PingData data;
    }

    // 서버 메시지에서 type만 먼저 확인할 때 사용
    [System.Serializable]
    private class TypeOnly
    {
        public string type;
    }

    private async void Start()
    {
        Log("서버에 접속 시도: " + serverUrl);
        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () =>
        {
            status = "접속됨";
            Log("[open] 서버에 접속했습니다.");
        };

        websocket.OnError += (e) =>
        {
            status = "오류";
            Log("[error] " + e);
        };

        websocket.OnClose += (e) =>
        {
            status = "끊김";
            Log("[close] 연결이 끊겼습니다.");
        };

        websocket.OnMessage += (bytes) =>
        {
            string json = Encoding.UTF8.GetString(bytes);
            HandleMessage(json);
        };

        // 접속 시작 (await로 연결될 때까지 기다림)
        await websocket.Connect();
    }

    private void Update()
    {
        // WebGL이 아닌 환경(에디터/PC)에서는 수신 메시지를 수동으로 처리해줘야 한다.
        // WebGL에서는 브라우저가 자동으로 처리하므로 호출하지 않는다.
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    // 서버가 보낸 메시지를 종류(type)에 따라 처리한다.
    private void HandleMessage(string json)
    {
        TypeOnly head = JsonUtility.FromJson<TypeOnly>(json);
        switch (head.type)
        {
            case "pong_from_server":
                Log("[pong] 서버 응답: " + json);
                break;
            default:
                Log("[recv] " + json);
                break;
        }
    }

    // Ping 메시지를 서버로 보낸다.
    private async void SendPing()
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Log("아직 접속되지 않았습니다.");
            return;
        }

        ClientMessage msg = new ClientMessage
        {
            type = "ping_from_client",
            data = new PingData { message = "hello from Unity" }
        };
        string json = JsonUtility.ToJson(msg);
        await websocket.SendText(json);
        Log("[ping] 서버로 전송: " + json);
    }

    private void Log(string line)
    {
        Debug.Log("[NetworkManager] " + line);
        logLines.Add(line);
        if (logLines.Count > 100) logLines.RemoveAt(0);
        scroll.y = float.MaxValue; // 항상 맨 아래로 스크롤
    }

    // 앱 종료 시 연결을 깨끗하게 닫는다.
    private async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }

    // ---- 화면 UI (별도 씬 세팅 없이 자동으로 그려짐) ----
    private void OnGUI()
    {
        GUI.skin.label.fontSize = 16;
        GUI.skin.button.fontSize = 16;

        GUILayout.BeginArea(new Rect(20, 20, 460, 420), GUI.skin.box);

        GUILayout.Label("Mighty 서버 연결 테스트");
        GUILayout.Label("연결 상태: " + status);
        GUILayout.Space(6);

        if (GUILayout.Button("Ping 보내기", GUILayout.Height(40)))
        {
            SendPing();
        }

        GUILayout.Space(6);
        GUILayout.Label("로그:");
        scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.Height(280));
        foreach (string line in logLines)
        {
            GUILayout.Label(line);
        }
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }
}
