# 프로젝트 구조

## 폴더

```
Assets/_Project/
├─ Shared/   MissedCall.Shared   게임 규칙을 모르는 재사용 요소
│  └─ Input/                     InputSystem_Actions
├─ Core/     MissedCall.Core     게임 뼈대와 Feature 간 계약
│  ├─ Round/                     GameFlow, RoundContext, RoundSeed, RoundStep
│  ├─ Aim/                       조준 계약(IAimTarget, IThrowable, IGrabbable)
│  ├─ Rooms/                     방 모듈 계약(RoomModule, DoorSocket, HideSpot, 열거형)과 RoomGraph
│  ├─ Tests/                     EditMode 테스트 (MissedCall.Core.Tests, 에디터 전용)
│  ├─ Scenes/Main.unity
│  └─ Settings/                  URP 에셋, 볼륨 프로파일
└─ Feature/  MissedCall.Feature  게임 기능
   └─ <기능명>/                  기능 하나의 코드, 프리팹, 머티리얼, 사운드를 함께 둔다
```

타입별 폴더는 최상위에 두지 않는다. 한 기능 안에서 파일이 많아질 때만 `Art/`, `Prefabs/` 같은 하위 폴더를 쓴다.
서드파티 에셋은 패키지가 정한 위치에 둔다. 프로젝트 소유 에셋만 `_Project`에 넣는다.

## 어디에 둘까

| 질문 | 위치 |
|---|---|
| 다른 게임에서도 그대로 쓸 수 있는가? (수학, 확장 메서드, UI 테마) | `Shared` |
| 라운드 흐름이거나 여러 Feature가 함께 쓰는 계약인가? | `Core` |
| 특정 게임플레이 기능에 속하는가? | `Feature/<기능명>` |

## 의존 규칙

```
Feature ──▶ Core ──▶ Shared
```

- asmdef가 `Core → Feature`, `Shared → Core` 참조를 컴파일 단계에서 막는다.
- **Feature끼리는 직접 참조하지 않는다.** 잼 일정을 고려해 어셈블리는 Feature 전체에 하나만 두었으므로 이 규칙은 컴파일러가 잡아주지 않는다. 리뷰할 때 확인한다.
- 다른 Feature의 정보가 필요하면 Core의 계약을 쓴다.
  - 공용 상태: `RoundContext` (폰 위치, 덮임 수, 남은 시간, 현재 조준 대상)
  - 흐름: `GameFlow`의 상태 변경 이벤트
  - 동작: Core에 선언한 인터페이스 (예: `IThrowable`)
  - 판 준비: `RoundStep`을 상속해 `GameFlow`의 준비 단계 목록에 넣는다
- 씬 연결은 인스펙터 참조로 한다. `FindObjectOfType` 계열과 전역 싱글턴은 쓰지 않는다.

## Core 구성

| 타입 | 책임 |
|---|---|
| `Round/GameFlow` | 상태 머신 (`Title / Playing / Answer / Won / Missed`), 판 시작과 준비 단계 실행, 실제 시간 타이머. 입력은 읽지 않는다. Feature가 `StartRound`, `RequestRestart`(R), `PickUp`, `Answer`를 호출하고 `StateChanged`를 구독한다 |
| `Round/RoundContext` | 판 공용 상태: 시드, 상태, 경과/남은 시간, `RoomGraph`, 폰 위치·방·덮임 수, 플레이어 위치·방, 조준 대상, 판 통계(던진 수, 받은 시간). `GameFlow.Context`로 얻는다. 판 시작 때 통째로 초기화한다 |
| `Round/RoundSeed`, `RoundStream` | 판 시드 하나에서 용도별(`Layout`, `Phone`, `Clutter`, `Start`, `Caller`, `Modifier`) `System.Random`을 가른다. 한 기능의 뽑는 횟수가 바뀌어도 다른 기능의 결과는 그대로다. `RoundStream` 값은 바꾸지 않고 끝에만 추가한다 |
| `Round/RoundStep` | 판 준비 단계의 추상 MonoBehaviour. Feature가 상속하고, `GameFlow` 인스펙터의 순서대로 `Prepare(context)`가 불린다. 이전 판 오브젝트는 여기서 풀에 돌려놓는다 |
| `Aim/IAimTarget`, `IThrowable`, `IGrabbable`, `AimKind` | Interaction이 Clutter, Phone을 모르고 조준·던지기·집기를 하기 위한 계약. 속도 계산과 집기 조건 판정은 Interaction이 한다 |
| `GameTuning` (ScriptableObject) | 튜닝 값. [`game-design.md`](../design/game-design.md)와 일치시킨다 |
| `Rooms/RoomModule`, `DoorSocket`, `HideSpot` | 방 모듈 프리팹에 붙는 데이터 컴포넌트. Layout(배치, 문), Phone과 Clutter(숨는 자리)가 함께 읽으므로 Room이 아니라 Core에 둔다 |
| `Rooms/RoomGraph`, `RoomDoor`, `GraphRoute` | 판의 집 구조와 소리 경로. Layout이 만들고 Ringtone, Player가 읽는다 |

판은 서로 독립이다. 새 판을 시작할 때 `RoundContext`를 통째로 초기화하고, 게임플레이 상태를 다음 판으로 넘기지 않는다. 판 사이에 남는 것은 `Result`의 기록뿐이다.

## Feature 목록 (계획)

폴더는 구현을 시작할 때 만든다.

| 폴더 | 책임 | 프로토타입 대응 |
|---|---|---|
| `Layout` | 집 생성: 템플릿 선택, 모듈 배치, 문 소켓, `RoomGraph` | 신규 |
| `Room` | 방 모듈 프리팹, 가구, 조명, 라이트맵 굽기와 복원, 회색 박스 생성 도구 | `block`, `noWalk`, `SPOTS` |
| `Player` | 걷기, 둘러보기, 입력 처리 | `walk`, `blocked`, 포인터 입력 |
| `Clutter` | 물건 종류, 더미 생성, 물리, 풀링 | `KINDS`, `addItem`, `buildPile` |
| `Interaction` | 화면 중심 조준, 던지기, 문 열기 | `updateTarget`, `act` |
| `Phone` | 폰 배치, 덮임 계산, 진동, 집기 | `countCovers`, `vibrate`, `grabPhone` |
| `Ringtone` | 발신자별 벨소리, 공간 음향, 벽 먹먹함, 문 쪽 음원, 효과음 | `initAudio`, `scheduleRing`, `oneShot` |
| `Modifiers` | 판 변수 (미끼 소리, 진동 모드, 정전, 닫힌 문) | 신규 |
| `Result` | 칭호 판정, 기록 저장과 조회 | 신규 |
| `Ui` | HUD, 타이틀, 받기 슬라이드, 결과, 기록 화면, 발신자 데이터 | HTML 오버레이, `CALLERS` |

에디터 전용 코드(라이트맵 굽기 도구 등)는 해당 기능 폴더의 `Editor/` 하위에 둔다. `MissedCall.Feature.Editor` asmdef는 `Feature/Room/Editor/`에 있다. 다른 기능의 `Editor/` 폴더는 이 asmdef를 가리키는 `.asmref`를 둔다.

## 네이밍

- 네임스페이스는 폴더를 따른다: `MissedCall.Core`, `MissedCall.Feature.Clutter`.
- 파일 하나에 public 타입 하나. 파일 이름은 타입 이름과 같게 한다.
- ScriptableObject 에셋은 타입 이름으로 시작한다: `GameTuning_Default.asset`.

## 최상위 폴더를 추가할 때

정말 세 폴더에 들어가지 않을 때만 추가하고 (예: Web 전용 템플릿처럼 Unity가 위치를 강제하는 경우), 이 문서에 이유를 남긴다.
