# TankAttack (Real-time Multiplayer Tank Battle)

**TankAttack**은 Unity 클라이언트와 순수 C# UDP 전용 서버를 직접 구현하여 연동한
**실시간 멀티플레이어 대전 게임**입니다.

기존 상용 네트워크 엔진(Photon 등)에 의존하지 않고, **순수 UDP 소켓 통신**을
기반으로 **자체적인 UDP 구조**를 설계했습니다.
UDP의 낮은 지연 특성을 유지하면서도, 중요한 게임 이벤트에 대해 
Sequence / Ack / Retry을 적용하여 신뢰성을 확보하는 것을
핵심 목표로 삼았습니다.

---

## Tech Stack

### Client (Unity)
- **Engine:** Unity 6
- **Architecture:** MVP Pattern, Single Scene Architecture
- **Dependency Injection:** `VContainer`
- **Reactive Programming:** `R3`
- **Asynchronous:** `UniTask`
- **Etc:** New Input System, Cinemachine, Object Pooling

### Server (.NET Console)
- **Framework:** .NET 9.0 (C# 13)
- **Network Protocol:** UDP (User Datagram Protocol)
- **Architecture:** Multi-threaded Job Queue Model
- **Memory Optimization:** `ArrayPool<byte>`
- **Thread Safety:** `ConcurrentDictionary`, `Interlocked`

---

## Key Features & Engineering Points

### 1. 자체 UDP 설계 및 구현
UDP의 패킷 유실과 중복 전송 문제를 고려해, 패킷의 중요도에 따라 전송 방식을 분리했습니다.

- **Unreliable 전송:** 초당 수십 번 전송되는 이동/회전(`PlayerUpdate`)은
  지연을 최소화하기 위해 순수 UDP로 전송합니다.
- **Reliable 전송:** 피격(`Hit`), 스폰(`Spawn`), 아이템 획득(`ItemPickup`),
  이모티콘(`Emoticon`) 등 중요한 이벤트 패킷에 대해서는
  Sequence(일련번호) 부여, Ack(수신 확인) 대기, 타임아웃 시 재전송(Retry Queue)을
  적용하여 신뢰성을 높였습니다.

### 2. 중복 실행 방지 및 동시성 제어
- Reliable 패킷 재전송으로 인한 중복 스폰/중복 피격을 막기 위해,
  클라이언트와 서버 양측에 **시퀀스 기반 deduplication**을 적용하여
  재전송으로 인한 중복 실행을 방지했습니다.
- 다수의 스레드가 동시에 `Join` 요청을 처리할 때 발생할 수 있는 Race Condition을
  `lock`을 통해 원자적으로 제어하여 중복 Join 문제를 방지했습니다.

### 3. 멀티스레딩 & 메모리 최적화 서버
- **Job Queue Pattern:** 서버 수신 스레드와 패킷 처리 워커를 분리하여 수신 병목을
  줄였습니다. 메인 수신 스레드는 패킷을 큐에 적재하고, 10개의 Worker Thread가
  큐에서 꺼내 병렬로 처리합니다.
- **메모리 할당 최소화:** 패킷 처리 시 GC 부하를 줄이기 위해
  `ArrayPool<byte>` 기반의 메모리 재사용을 적용했습니다.

### 4. 모던 유니티 아키텍처 (R3 + VContainer)
- **의존성 주입:** `VContainer`를 활용해 Presenter와 Manager 간의 결합도를 낮추고
  생명주기를 관리합니다.
- **반응형 UI:** `R3`를 이용하여 이벤트 발행/구독 패턴으로
  UI(데미지 텍스트, HP 바, 이모티콘)를 업데이트합니다.
- **오브젝트 풀링:** 전투 중 빈번하게 생성/파괴되는 UI 오브젝트들을 Pool로 관리하고,
  유저 접속 종료 시 `ClearAll` 및 딕셔너리 정리를 통해 메모리 누수와
  고아 객체를 방지합니다.

---

## Game Systems
- **실시간 동기화:** 플레이어 이동, 포탑 회전, 탄환 발사 동기화
- **전투 시스템:** 피격 판정, HP 동기화, 플로팅 데미지 연출, 사망 처리 및 세션 정리
- **상호작용:** 랜덤 아이템 스폰 및 선착순 획득 동기화,
  숫자 키(1~3)를 활용한 실시간 이모티콘 브로드캐스팅
- **세션 관리:** Heartbeat를 통한 클라이언트 접속 유지 확인 및 Timeout 자동 퇴장 처리

---

## Getting Started

### 1. Server 실행 방법 (Local)
1. `UDPServer` 프로젝트를 Visual Studio 또는 Rider로 엽니다.
2. `ServerConfig` 또는 진입점(Main)에서 IP를 `127.0.0.1`(또는 자신의 로컬 IP),
   Port를 설정합니다. (기본값: `7777`)
3. 프로젝트를 빌드하고 실행하면 콘솔 창에 `[서버] 수신 루프 시작` 메시지가 출력됩니다.

### 2. Client 실행 방법
1. Unity에서 `TankAttack` 프로젝트를 엽니다.
2. 메인 씬(Scene)을 실행합니다.
3. In-Game UI의 서버 IP 입력란에 서버가 실행 중인 IP(예: `127.0.0.1`)와 Port를 입력합니다.
4. **Connect** 버튼을 눌러 서버와 연결하고, **Join** 버튼을 눌러 전차를 스폰합니다.

---

## AWS EC2 Server Deployment

서버를 AWS에 띄워 원격지 플레이어와 멀티플레이를 하려면 다음 과정을 따릅니다.

### Step 1: EC2 인스턴스 생성 및 세팅
1. AWS 콘솔에서 **Ubuntu 22.04 LTS** (또는 Amazon Linux) EC2 인스턴스를 생성합니다.
2. **보안 그룹(Security Group) 설정:** 인바운드 규칙에 서버 포트(예: `7777`)의
   **UDP** 포트를 개방(0.0.0.0/0)해야 합니다.

### Step 2: EC2 SSH 접속
키 페어 파일 권한을 설정한 후 EC2 인스턴스에 접속합니다.

```bash
chmod 400 TankAttack.pem
ssh -i "TankAttack.pem" ubuntu@ec2-<퍼블릭 IPv4 주소>.ap-northeast-2.compute.amazonaws.com
```

### Step 3: .NET 9.0 Runtime 설치
SDK 전체가 아닌 Runtime만 설치합니다.

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --runtime dotnet --channel 9.0
```

환경변수에 경로를 추가하고 적용합니다.

```bash
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc
```

설치 여부를 확인합니다.

```bash
dotnet --list-runtimes
```

### Step 4: 서버 빌드 (로컬)
로컬 환경에서 리눅스 대상으로 퍼블리시합니다.

```bash
dotnet publish -c Release -r linux-x64 --self-contained false -p:PublishSingleFile=false -o ./publish
```

### Step 5: EC2로 빌드 파일 전송

```bash
scp -i ~/your-key.pem -r ./publish ubuntu@:~/udpserver
```

### Step 6: 실행

```bash
chmod +x ~/udpserver/UDPServer
dotnet ~/udpserver/UDPServer.dll
```

### Step 7: 클라이언트 접속
Unity 클라이언트에서 IP 주소를 `127.0.0.1` 대신 EC2 인스턴스의
'퍼블릭 IPv4 주소'로 변경하고 Connect를 누르면 연동됩니다.
