# 02. Unity WebGL 클라이언트와 서버 연결 테스트

## 목표

Unity에서 Node.js socket.io 서버에 접속하고, 버튼을 눌러 이벤트를 보낸 뒤 서버 응답을 화면에 표시한다.

## 구현 범위

- Unity 프로젝트 생성
- socket.io 또는 WebSocket 클라이언트 패키지 적용
- NetworkManager 스크립트 생성
- 서버 접속
- Ping 버튼 UI 생성
- 버튼 클릭 시 `ping_from_client` 전송
- 서버 응답 `pong_from_server` 수신
- Text UI에 서버 응답 표시

## Unity 씬 구성

```text
MainScene
├─ Canvas
│  ├─ Button_Ping
│  └─ Text_Status
└─ NetworkManager
```

## Cursor 프롬프트

```text
Unity에서 Node.js socket.io 서버에 접속하는 NetworkManager C# 스크립트를 만들어줘.

요구사항:
1. 서버 주소는 `http://localhost:3000`으로 둔다.
2. 게임 시작 시 서버에 접속한다.
3. 접속 성공 시 Debug.Log와 UI Text에 상태를 표시한다.
4. Ping 버튼을 누르면 `ping_from_client` 이벤트를 서버로 보낸다.
5. 서버에서 `pong_from_server` 이벤트를 받으면 메시지를 UI Text에 표시한다.
6. WebGL 빌드에서도 동작 가능한 방식을 우선 고려한다.
7. 코드에는 주석을 충분히 달아줘.
```

## 서버-클라이언트 이벤트 약속

### Unity → Server

```json
{
  "event": "ping_from_client",
  "data": {
    "message": "hello from unity"
  }
}
```

### Server → Unity

```json
{
  "event": "pong_from_server",
  "data": {
    "message": "pong",
    "serverTime": "2026-07-03T..."
  }
}
```

## 완료 기준

- Unity Play 버튼을 눌렀을 때 서버 접속 로그가 찍힌다.
- Unity 버튼을 클릭하면 서버 콘솔에 ping 로그가 찍힌다.
- Unity 화면에 서버 응답 메시지가 표시된다.

## 주의사항

- Unity Editor에서는 `localhost`가 내 PC를 의미한다.
- WebGL 배포 후에는 `localhost`를 쓰면 안 된다.
- 운영용 주소는 나중에 `https://도메인` 또는 `wss://도메인/socket`으로 바꾼다.

## 다음 단계

방 생성과 방 입장 기능을 추가한다.
