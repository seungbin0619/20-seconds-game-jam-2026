# Missed Call (20초 게임잼)

20초 안에 어질러진 방에서 울리는 폰을 소리로 찾아 받는 1인칭 3D 게임.
`docs/prototype.html`(three.js + cannon.js)을 **Unity 6000.3 / URP**로 포팅하고 **Web(WebGL 2)** 빌드로 제출한다.

## 먼저 읽을 문서

`docs/README.md`가 전체 색인이다. 작업별로 필요한 것만 읽는다.

| 작업 | 문서 |
|---|---|
| 게임 규칙, 튜닝 값, 콘텐츠 | `docs/design/game-design.md` |
| 폴더, 의존성, 네이밍 | `docs/architecture/project-structure.md` |
| 프로토타입 포팅 (좌표계, 오디오, 물리) | `docs/architecture/porting-notes.md` |
| 진행 상황과 다음 작업 | `docs/plan/roadmap.md` |
| Web 빌드와 제출 | `docs/build/web-build.md` |

## 절대 규칙

- 모든 프로젝트 에셋은 `Assets/_Project/` 아래에 **기능별로** 둔다. `Scripts/`, `Prefabs/` 같은 타입별 최상위 폴더를 만들지 않는다.
- 최상위는 `Core`, `Shared`, `Feature` 세 개다. 추가는 최소화하고, 추가하면 구조 문서를 갱신한다.
- 의존 방향은 `Feature → Core → Shared`. 역방향 참조 금지. Feature끼리는 직접 참조하지 않고 Core의 계약을 거친다.
- 프로토타입과 수치가 달라지면 `GameTuning`을 고치고 `game-design.md`도 맞춘다. 튜닝 값을 코드에 하드코딩하지 않는다.
- Web에서 안 되는 기능에 의존하지 않는다. 특히 폰 벨소리의 저역 통과 필터와 HRTF는 Unity 오디오가 아니라 `.jslib` Web Audio로 구현한다 (`porting-notes.md`).
- 런타임에 쓰는 머티리얼은 `Shader.Find`로 만들지 않고 에셋을 참조한다. 빌드에서 셰이더가 빠진다.
- 게임 안의 모든 텍스트는 영어만 쓴다. 문구는 `game-design.md` 기준이며 프로토타입의 한국어 문구를 옮기지 않는다.
- 폰트는 OS 폰트를 쓸 수 없다. 프로젝트에 포함한 TMP 폰트 에셋(ASCII)으로 표시한다.

## Unity 에디터 다루기

- 에디터가 열려 있으면 `.unity`, `.prefab`, `.asset` YAML과 `ProjectSettings/`를 직접 고치지 않는다. `unity status`로 연결을 확인하고 `unity command`로 작업한다 (C# 실행은 `unity command eval '...'`).
- `com.unity.pipeline` 패키지가 이 연결을 제공하므로 제거하지 않는다.
- 에셋을 옮길 때는 `.meta`를 함께 옮긴다. 가능하면 `AssetDatabase.MoveAsset`을 쓴다.
- 코드 수정 후에는 콘솔에 컴파일 에러가 없는지 확인한다.
- 커밋에는 `.meta`를 포함한다. 빈 폴더는 만들지 않는다 (git에 안 남아서 `.meta`가 어긋난다).
