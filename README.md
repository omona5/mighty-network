# Mighty Network

Unity WebGL 클라이언트와 Node.js WebSocket 서버로 만든 5인용 마이티 카드 게임이다.
게임 규칙과 상태는 서버가 판정하고 Unity 클라이언트는 입력과 화면 표시를 담당한다.

## 구조

- `mighty-network-unity/`: Unity 6 클라이언트
- `server-node/`: HTTP/WebSocket 게임 서버
- `scripts/`: 로컬 개발 서버 시작·종료 도구
- `md_files/`: 설계 및 단계별 구현 문서

## 로컬 개발

```powershell
.\scripts\Start-LocalServer.ps1
```

서버 상태는 `http://localhost:3000/health`, 브라우저 테스트 페이지는
`http://localhost:3000`에서 확인한다. Unity 프로젝트는
`mighty-network-unity/`를 Unity Hub에서 연다.

```powershell
.\scripts\Stop-LocalServer.ps1
```

서버 테스트는 `server-node/`에서 `npm test`로 실행한다.
