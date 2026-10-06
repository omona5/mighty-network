# 12. Unity WebGL 빌드 및 Raspberry Pi 배포

## 목표

Unity WebGL 빌드 파일과 Node.js(순수 WebSocket) 서버를 Raspberry Pi에 배포해서 친구들이 인터넷 주소로 접속할 수 있게 한다.

## 지금 당장 (Pi 없이)

1. Unity 서버 URL 설정 분리 ✅ (`ServerUrlResolver` + 타이틀 자동 연결)
2. WebGL 빌드 체크리스트 (아래)
3. 로컬/LAN에서 WebGL ↔ `ws://...:3000` 접속 확인

Pi SSH/IP는 준비되면 알려주면 된다.

---

## 서버 URL 설정 (Unity)

우선순위:

1. **URL 쿼리** — `https://게임주소/?ws=wss://서버호스트`
2. **WebGL 같은 호스트** — 페이지가 `https://x.com`이면 기본 `wss://x.com` (리버스 프록시 전제)
3. **개발 기본값** — `ws://localhost:3000`

타이틀에서 자동 연결하고 연결 상태만 표시한다. 서버 연결 전에는 싱글/멀티플레이가
비활성화되며, 5초 간격 자동 재접속과 수동「재접속 시도」버튼을 지원한다.
연결은 게임 씬에서도 유지된다. 기존 `mighty.serverUrl` 저장값은 자동 연결에 사용하지
않으며, 멀티플레이 화면에는 서버 주소 입력 및 연결 상태가 표시되지 않는다.

게임 중「메뉴」에서 설정과 방 나가기를 선택한다. 설정의 마스터 볼륨과 한국어/English
선택은 즉시 반영되고 저장된다.

예:

```text
개발:     ws://localhost:3000
LAN:      ws://192.168.0.10:3000
운영:     wss://mighty.example.com
WebGL:    index.html?ws=wss://mighty.example.com
```

주의: **HTTPS 페이지는 `wss://`만** 된다 (`ws://`는 혼합 콘텐츠로 차단).

---

## WebGL 빌드 체크리스트

### 로딩 진행률과 캐시

- `Responsive` 템플릿의 `loading.js`가 `.data`, `.wasm`, framework의 용량을 합산해 파일 준비 진행률을 표시한다. 다운로드/캐시 읽기가 끝나면 100%와 실행 준비 문구로 전환한다. 실행 준비 시간은 용량 퍼센트에 포함하지 않는다.
- WebGL 빌드 후 `WebBuildSizes`가 `build-sizes.json`을 자동 생성한다. 배포 시 `index.html`, `loading.js`, `build-sizes.json`, `Build/`, `StreamingAssets/`를 같은 빌드의 파일로 함께 복사한다. GitHub Pages의 기존 상대 경로 base 설정은 유지한다.
- Unity Data Caching 및 Name Files As Hashes 설정을 유지한다. 로더는 해시 이름 파일에 `immutable`을 사용하고, 고정 이름 data/wasm 파일은 재검증한다. 용량 목록이 없거나 고정 파일명을 쓰는 구빌드는 HEAD의 Content-Length로 용량을 확인한다.
- Node 정적 서버는 해시 이름 빌드 파일에 1년 캐시를 적용한다. HTML, 용량 목록과 고정 이름 파일은 `no-cache` + ETag/Last-Modified로 재검증하며, 변경이 없으면 본문 없이 304를 반환한다. 서버 코드 배포 후 Node 프로세스를 재시작한다. Nginx가 정적 파일을 직접 제공한다면 같은 정책을 Nginx에도 적용해야 한다.
- GitHub Pages는 Node 서버를 사용하지 않으므로 HTTP 헤더 정책은 적용되지 않지만, 로더의 Unity 캐시 정책은 적용된다. 캐시 삭제/브라우저 저장 공간 회수 후에는 다시 다운로드할 수 있다.
- 확인: 개발자 도구 Network에서 Disable cache를 끄고 같은 버전을 재방문한다. 전송량과 UnityCache 로그를 확인한다. 단순히 로딩 화면이 다시 나타난다는 것만으로 재다운로드 여부를 판단하지 않는다.

### A. Unity에서 빌드

- [ ] File → Build Settings → Platform = **WebGL** → Switch Platform
- [ ] Player Settings
  - [ ] Resolution: 데스크톱 기본 해상도 / 리사이즈 허용(원하면)
  - [ ] Publishing Settings: Compression Format (Gzip 또는 Disabled로 먼저 테스트)
- [ ] `NetworkManager` Inspector 기본 URL은 `ws://localhost:3000` 유지해도 됨 (운영은 `?ws=` / PlayerPrefs)
- [ ] Build → 출력 폴더 예: `Builds/WebGL/`
- [ ] 결과물 확인: `index.html`, `Build/`, `TemplateData/`

### B. 로컬에서 서버 + WebGL 같이 테스트 (권장)

같은 머신에서:

1. `server-node`에서 `node server.js` (포트 3000)
2. WebGL을 **같은 오리진**으로 서빙하는 방법 중 하나:
   - **간단:** Unity Build 폴더를 임시로 `server-node/public/webgl/`에 복사하고 `http://localhost:3000/webgl/` 접속  
     (정적 파일은 이미 `public/`을 제공함)
   - 또는 아무 정적 서버 + `?ws=ws://localhost:3000`
3. 브라우저에서 방 생성/입장/한 판 플레이
4. 새로고침 → 재접속(봇 대타) 확인

```bash
# 예: 빌드 복사 후 서버 기동
cp -R Builds/WebGL/* server-node/public/webgl/
cd server-node && node server.js
# 브라우저: http://localhost:3000/webgl/
# (같은 host라 WebGL 기본 wss/ws 호스트 규칙 적용 — 포트 3000이면
#  http://localhost:3000/webgl/  → 기본 ws://localhost:3000  으로 Resolve됨)
```

### C. 흔한 실패

| 증상 | 원인 | 조치 |
|------|------|------|
| 연결 안 됨 (https 페이지) | `ws://` 사용 | `wss://` + HTTPS 서버/터널 |
| 로컬 WebGL만 실패 | file:// 로 index 염 | http 서버로 열기 |
| 접속 후 바로 끊김 | 방화벽/포트 | 3000 허용, URL 호스트 확인 |
| 압축/로딩 오류 | Gzip + nginx 미설정 | 일단 Compression Disabled로 빌드 |
| **한글이 안 보임/□** | WebGL 기본 폰트에 한글 없음 | `Resources/Fonts/NotoSansKR-Regular` 포함 후 **재빌드** |

---

## 배포 구조 (Pi — 이후)

```text
Raspberry Pi
├─ Nginx (또는 Node public/)  → WebGL 정적 파일
├─ Node.js WebSocket 서버     → ws / wss
└─ PM2                        → 서버 상시 실행
```

권장 접속:

```text
친구 브라우저
  → https://도메인 (Cloudflare Tunnel 등)
  → Pi Nginx + Node
```

### Raspberry Pi 준비 (나중에)

```bash
sudo apt update
sudo apt install -y nodejs npm nginx
sudo npm install -g pm2
```

```bash
git clone <이 저장소>
cd mighty-network/server-node
npm install
pm2 start server.js --name mighty-server
pm2 save
```

WebGL 파일 예:

```text
/var/www/mighty-webgl/
├─ index.html
├─ Build/
└─ TemplateData/
```

Nginx는 정적 파일 + `/` WebSocket 업그레이드(또는 별도 경로)를 Node로 프록시.  
상세 설정은 Pi IP/SSH 준비 후 맞춘다.

---

## 완료 기준

- [ ] WebGL 빌드가 브라우저에서 로드된다
- [ ] 설정한 서버 URL로 방 생성/입장이 된다
- [ ] (이후) Pi에서 PM2 + 외부 접속으로 친구 플레이 가능

## 이후 개선

- HTTPS / Cloudflare Tunnel
- Gzip + nginx `gzip_static`
- 카드 스프라이트·애니메이션
- 로그·전적 저장
