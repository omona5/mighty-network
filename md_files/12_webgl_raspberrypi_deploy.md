# 12. Unity WebGL 빌드 및 Raspberry Pi 배포

## 목표

Unity WebGL 빌드 파일과 Node.js 서버를 Raspberry Pi에 배포해서 친구들이 인터넷 주소로 접속할 수 있게 한다.

## 배포 구조

```text
Raspberry Pi
├─ Nginx
│  └─ Unity WebGL 빌드 파일 제공
│
├─ Node.js 서버
│  └─ socket.io 게임 서버
│
└─ PM2
   └─ 서버 자동 실행/재시작
```

## 권장 접속 구조

```text
친구 브라우저
  ↓
https://mighty.example.com
  ↓
Cloudflare Tunnel
  ↓
Raspberry Pi Nginx / Node.js
```

## 서버 주소 관리

Unity 개발 단계와 운영 단계의 서버 주소는 다르다.

```text
개발용:
http://localhost:3000

라즈베리파이 내부 테스트:
http://raspberrypi.local:3000

운영용:
https://mighty.example.com
```

Unity 코드에서 서버 주소를 하드코딩하지 말고 설정값으로 관리한다.

## Raspberry Pi 준비 명령 예시

```bash
sudo apt update
sudo apt install -y nodejs npm nginx
sudo npm install -g pm2
```

## 서버 배포 예시

```bash
git clone https://github.com/yourname/mighty-server.git
cd mighty-server
npm install
pm2 start server.js --name mighty-server
pm2 save
```

## Unity WebGL 파일 위치 예시

```text
/var/www/mighty-webgl/
├─ index.html
├─ Build/
└─ TemplateData/
```

## Cursor 프롬프트

```text
Unity WebGL 빌드와 Node.js socket.io 서버를 Raspberry Pi에 배포하기 위한 README.md를 작성해줘.

요구사항:
1. Raspberry Pi OS 기준으로 Node.js, npm, nginx, pm2 설치 명령을 포함한다.
2. Node.js 서버를 git clone 후 npm install, pm2 start로 실행하는 절차를 포함한다.
3. Unity WebGL 빌드 파일을 /var/www/mighty-webgl에 복사하는 절차를 포함한다.
4. Nginx가 Unity WebGL index.html을 제공하도록 설정 예시를 작성한다.
5. socket.io 서버와 reverse proxy를 연결하는 설정 예시를 작성한다.
6. Cloudflare Tunnel을 사용할 경우의 개념 설명을 추가한다.
7. 개발용 localhost 주소와 운영용 도메인 주소를 분리하는 방법을 설명한다.
```

## 완료 기준

- Raspberry Pi에서 Node.js 서버가 PM2로 실행된다.
- Nginx가 Unity WebGL 페이지를 제공한다.
- 친구가 주소로 접속하면 게임 화면이 열린다.
- Unity WebGL 클라이언트가 서버에 접속한다.

## 이후 개선 사항

- HTTPS 적용
- Cloudflare Tunnel 적용
- 로그 파일 관리
- SQLite 전적 저장
- 관리자용 방 목록 페이지
