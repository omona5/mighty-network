# WORK_LOG

## 2026-07-18 13:16 (UTC+9)

- **작업 요청**: 마이티(트럼프 카드 게임) 개발 시작. Unity WebGL 클라이언트 + Node.js 서버, 로컬호스트 테스트 후 최종적으로 라즈베리파이 배포. 개발 계획 md 파일 확인 요청.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **확인/열람한 파일**:
  - `README.md`
  - `md_files/00_project_overview.md` ~ `md_files/13_cursor_workflow.md` (총 14개 계획 문서)
- **실행 명령**: `ls -la`, `git log --oneline -10`
- **생성/갱신 파일**: `WORK_LOG.md` (신규 생성)
- **결과 요약**:
  - 프로젝트는 서버 authoritative 방식(서버가 게임 상태 관리, Unity는 렌더링/입력만) 원칙으로 설계됨.
  - 12단계 개발 로드맵 확인. 첫 목표는 Unity 버튼 클릭 → 서버 ping → pong 응답 표시.
  - 폴더 구조 제안: `client-unity/`, `server-node/`.
  - 현재 저장소 상태: `md_files/` 계획 문서와 기본 `README.md`만 존재. 실제 코드는 아직 없음(Initial commit 1개).
- **미해결 이슈/주의사항**: 없음. 다음 단계(서버 초기화) 진행 여부 사용자 확인 필요.
- **다음 단계**: 사용자 선택에 따라 `server-node/` Node.js + socket.io 서버(01단계) 구현 시작.

## 2026-07-18 13:22 (UTC+9)

- **작업 요청**: 01단계 - Node.js + socket.io 서버 초기화 및 ping-pong 테스트 구현.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network/server-node`
- **환경**: Node v26.5.0, npm 11.17.0
- **생성/수정 파일**:
  - `server-node/package.json` (npm init + start 스크립트, main=server.js)
  - `server-node/server.js` (http 서버 + socket.io, ping_from_client → pong_from_server)
  - `server-node/public/test.html` (브라우저용 ping-pong 테스트 페이지)
  - `server-node/README.md` (실행/테스트 방법, 이벤트 약속)
  - `server-node/src/` (빈 폴더, 이후 로직 배치용)
- **실행 명령**:
  - `npm init -y`
  - `npm install socket.io` (22개 패키지 추가, 취약점 0)
  - `node server.js` (백그라운드 실행 중, PID 93826, 포트 3000)
  - `npm install --no-save socket.io-client` + 임시 테스트 클라이언트로 ping-pong 검증 후 파일 삭제
- **결과/검증**:
  - HTTP 페이지 200, `/socket.io/socket.io.js` 200 응답 확인.
  - ping-pong end-to-end 성공 (`PING-PONG OK`), 서버 콘솔에 connect/ping/disconnect 로그 확인.
- **미해결 이슈**: 없음. (서버는 백그라운드에서 계속 실행 중)
- **다음 단계**: 02단계 - Unity 프로젝트 생성 및 NetworkManager로 서버 접속.

## 2026-07-18 13:25 (UTC+9)

- **작업 요청**: 개발 방향 결정 (스프라이트 스타일 / 다음 단계).
- **결정 사항**:
  - 스프라이트 스타일: **픽셀 아트**. 카드 규격 추천 64×96px(2:3), 앞면 53장 + 뒷면 1장 동일 크기, Unity 임포트 시 Point 필터/무압축 예정.
  - Unity 프로젝트: **사용자가 직접 생성** 예정. 위치는 `client-unity/`, 템플릿 2D(Core), 이후 WebGL 스위치.
  - WebGL socket.io 연결 방식: 브라우저 socket.io.js를 .jslib 브릿지로 사용하는 방식 채택 예정(서버 변경 불필요).
- **생성/수정 파일**: `WORK_LOG.md`만 갱신.
- **다음 단계**: 사용자가 `client-unity/`에 Unity 프로젝트 생성 완료하면, NetworkManager.cs + WebGL socket.io .jslib 브릿지 + 테스트 UI 추가.

## 2026-07-18 13:27 (UTC+9)

- **작업 요청**: 맥/윈도우 오가며 git clone/push 방식으로 개발 가능한지 문의 + 협업 환경 정비.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **생성/수정 파일**:
  - `.gitignore` 수정: `node_modules/`, npm 로그, `.env`, `.DS_Store`, `Thumbs.db` 추가.
  - `.gitattributes` 신규 생성: text=auto eol=lf, 이미지/오디오/Unity 에셋 binary 지정 (LF/CRLF 문제 방지).
- **실행 명령**: `git rm --cached .DS_Store` (추적된 적 없어 no-op), `git status --short`.
- **결과/확인**:
  - 현재 git 상태: `.gitignore` 수정됨, `.gitattributes`/`WORK_LOG.md`/`md_files/`/`server-node/` 미추적. (초기 커밋에는 README.md, .gitignore만 포함되어 있었음)
  - 결론: 맥↔윈도우 크로스 개발 가능. node_modules/Library는 gitignore, 각 PC에서 `npm install` 및 Unity가 자동 복원. Unity 버전 통일 + .meta 커밋 필수 안내.
- **미해결 이슈**: 없음.
- **다음 단계**: (선택) 현재 상태 첫 커밋 정리 및 GitHub 연결. 이후 Unity 프로젝트 생성 대기.

## 2026-07-18 13:33 (UTC+9)

- **작업 요청**: 현재까지 작업 내용을 커밋 정리 후 GitHub push.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **확인 사항**:
  - 사용자가 Unity 프로젝트를 `mighty-network-unity/` 폴더에 직접 생성함 (계획상 명칭은 client-unity였으나 실제 명칭은 mighty-network-unity).
  - 루트 `.gitignore`의 Unity 규칙이 루트 기준 anchored(`/[Ll]ibrary/`)라 하위 폴더에 미적용 → `git add` 대상이 34,491개로 폭증하는 문제 발견.
- **생성/수정 파일**:
  - `mighty-network-unity/.gitignore` 신규 생성 (Unity 표준 ignore, 프로젝트 폴더 기준). 적용 후 추적 대상 34,491 → 70개로 정상화.
- **실행 명령**: `git status`, `git check-ignore`, `git add -A --dry-run | wc -l`.
- **결과**: `mighty-network-unity/Library/` 등 자동 생성 폴더 정상 무시 확인. Remote: origin=https://github.com/omona5/mighty-network.git, branch=main.
- **다음 단계**: 아래에서 add/commit/push 진행.
