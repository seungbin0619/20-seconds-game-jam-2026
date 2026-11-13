# 포팅 노트

`prototype.html`을 Unity로 옮길 때 그대로 복사하면 틀어지는 지점과 그 해결 방향.

## 1. 오디오 (가장 큰 리스크)

게임의 핵심인 **거리/방향 + 덮임에 따른 먹먹함**이 Unity 오디오만으로는 Web에서 보장되지 않는다.

- 공식 문서 [Audio in Web](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/develop/audio) 기준으로 AudioMixer는 볼륨만 지원하고 효과는 지원하지 않는다.
- `AudioLowPassFilter`, `OnAudioFilterRead`, `spatialBlend`는 지원 목록에 없다.

**결정: 벨소리는 `.jslib` 플러그인에서 브라우저 Web Audio API로 직접 구현한다.** 프로토타입의 `initAudio`, `playNote`, `playBuzz`, `scheduleRing`, `oneShot`, `updateListener`를 거의 그대로 옮길 수 있고, 프로토타입과 같은 소리가 난다.

```
Feature/Ringtone/
├─ IRingtoneBackend.cs      Init, Start, Stop, SetCutoff, SetGain, SetPhonePosition, SetListener, OneShot
├─ WebAudioBackend.cs       #if UNITY_WEBGL && !UNITY_EDITOR, DllImport
├─ EditorAudioBackend.cs    에디터 테스트용. AudioSource + AudioLowPassFilter 근사
└─ RingtoneWebAudio.jslib   Web Audio 그래프
```

- 그래프: `ringBus(gain) → muffle(lowpass) → panner(HRTF, inverse) → master → destination`
- 좌표는 jslib 경계에서 Unity → Web Audio 오른손 좌표계로 변환한다 (z 반전).
- 음원은 여러 개를 다룰 수 있게 만든다: 폰 벨소리 1개 + 미끼 소리 0~1개. 음원마다 `ringBus → muffle → panner` 체인을 하나씩 둔다.
- 벨 박자 예약은 Unity 프레임이 아니라 JS 타이머에서 한다. 프레임이 끊겨도 박자가 흔들리지 않는다.
- 벽 먹먹함과 문 쪽 음원 위치는 C#에서 계산해 cutoff와 음원 위치로 넘긴다. jslib은 값을 적용만 한다.
  - `SoundPath.ThroughDoor`가 가상 음원 위치와 소수 벽 수를 만든다. `RoomGraph`는 첫 문 위치, 문 다음 지점(다음 문 또는 음원), 첫 문부터 음원까지 경로 길이, 벽 수를 넘겨야 한다.
  - `RingtonePlayer`는 음원 위치를 듣는 사람 기준 상대 위치로 보간한다. 걸을 때 음원이 끌려오지 않고, 경로가 바뀔 때만 부드럽게 옮겨간다.
- 게임 연결은 `PhoneRinger`가 한다. `GameFlow.StateChanged`를 받아 `Playing`에서 울리고(진동 모드 판은 울리지 않음), `Answer`에서는 먹먹함 없이 계속 울리며 `Grab` 효과음을 내고, `Won`/`Missed`에서 멈추고 결과 효과음을 낸다. 매 `LateUpdate`(RingtonePlayer보다 먼저) `RoundContext`의 플레이어·폰 위치와 방, 덮임 수로 `RoomGraph.Route` → `SoundPath`를 계산해 넘긴다. 플레이어가 어느 방에도 없으면(문턱) 마지막 방을 쓴다.
  - 확인 (11/12, 에디터, 시드 100~129, 방마다 가운데에서): 폰 방은 벽 0, 다른 방은 1~2. 받기 화면에서 cutoff 7000(맑음), 판이 끝나면 멈춤. R 40회 연타에도 음원 1개 유지.
  - **이어폰으로 실제 집에서 방향감 확인은 남았다** (Chrome에서 직접).

### 검증 결과 (2026-11-10, 테스트 씬 `Feature/Ringtone/Sandbox`)

| 항목 | 결과 |
|---|---|
| Web Audio 동작, 첫 클릭 후 재생 | 통과. 콘솔 에러 없음 |
| HRTF 좌우 방향 | 통과. 몸을 돌리면 소리 방향이 따라 바뀜 |
| 앞뒤 구분 | 이어폰으로는 판정이 어려움. 통과로 간주 |
| 덮임에 따른 먹먹함 | 통과 |
| 음원 2개 동시 재생 (벨 + 미끼) | 통과 |
| 문 통과 | 1차 실패 (문 앞에서 커졌다가 통과 순간 작아지고 방향이 확 바뀜). 경로 길이 기반 거리와 문 근처 보간으로 수정 후 통과 |
| 벽 먹먹함 체감 | 약함. cutoff 배율만으로는 기본음이 남는다. 벽당 볼륨 감소(`wallGainScale`)를 추가. 임시값 0.5 / 0.8로 두고 폴리시 단계에서 조정 |

### 녹음된 사운드 파일(.wav 등)을 쓸 때

소리 경로, 먹먹함, 볼륨, HRTF, 미끼 음원은 음원 종류와 무관하므로 그대로 쓴다. **소리를 만드는 부분만** 추가한다.

| 위치 | 할 일 |
|---|---|
| jslib | 음원마다 오실레이터 대신 `AudioBufferSourceNode`(loop)를 `bus`에 연결하는 경로 추가. 효과음도 버퍼 재생 |
| 파일 로딩 (Web) | 파일을 `StreamingAssets`에 두고 jslib에서 `fetch` + `decodeAudioData`로 읽는다. Unity `AudioClip.GetData`는 Web에서 제한적이라 쓰지 않는다 |
| 에디터 백엔드 | `AudioClip`을 `AudioSource`에 그대로 넣는다 |
| `RingtonePattern` | 합성 패턴과 파일 중 하나를 고르게 한다. 합성 벨과 파일 효과음을 섞어 써도 된다 |

- 원본은 .wav로 관리하되 Web 배포용은 .mp3나 .m4a(AAC)로 둔다. .wav는 용량이 크고, .ogg는 오래된 Safari에서 재생이 안 될 수 있다.
- 벨소리 파일은 루프 지점에서 끊기지 않게 만든다.
- 진동 연출(`jiggle`)은 벨 한 주기의 절반마다 일어나므로, 파일이면 주기 길이를 패턴에 적어 둔다.
- 파일 디코딩이 끝나기 전에는 재생할 수 없으므로 판 시작 전에 로딩을 끝낸다.
| 시간에 따른 볼륨 증가 | 약하게 느껴짐. 0.75→1.2를 0.5→1.2로 넓힘 |
| 효과음 | 들림. 소리는 임시 |

에디터 백엔드(`UnityAudioBackend`)는 HRTF가 아니므로 방향감 판단에 쓰지 않는다.
- `AudioContext`는 사용자 제스처가 있어야 시작된다. Unity 버튼 콜백은 브라우저 이벤트 호출 스택 밖에서 실행되므로, jslib에서 `pointerdown`, `touchend`, `keydown` 리스너를 등록해 그 안에서 `resume()`을 호출한다.
- **최종 음향 판단은 Web 빌드에서 한다.** 에디터 백엔드는 근사치다.

## 2. 좌표계

three.js는 오른손, Unity는 왼손 좌표계다. 방이 대칭이 아니므로 변환을 일관되게 적용한다.

| 프로토타입 | Unity |
|---|---|
| 위치 `(x, y, z)` | `(x, y, -z)` |
| 크기 `(w, h, d)` | 그대로 |
| 전방 `-Z` | 전방 `+Z` |
| `yaw` (rad) | `-yaw` → degree |
| `pitch` (rad, 위가 +) | `-pitch` → degree (아래가 +) |
| pitch 제한 `[-1.42, 1.1]` | `[-1.1, 1.42]` rad |
| 플레이어 시작 `(0, 2.2)`, pitch `-0.4` | `(0, -2.2)`, pitch `+0.4` rad |

색상은 `0xRRGGBB` sRGB 값이다. 프로젝트가 Linear이므로 `Color`를 헥스에서 만들 때 sRGB로 취급한다 (`ColorUtility` 또는 인스펙터 입력).

`game-design.md`의 수치는 프로토타입 기준이다. 코드에 옮길 때 위 표를 적용한다.

## 3. 물리

| 프로토타입 | Unity |
|---|---|
| gravity -9.82 | Physics 기본값 사용 (-9.81) |
| friction 0.55, restitution 0.06 | PhysicsMaterial 하나를 기본으로 지정 |
| solver iterations 7 | `Physics.defaultSolverIterations = 7` |
| linearDamping 0.22, angularDamping 0.45 | `Rigidbody.linearDamping`, `angularDamping`. 공식이 달라 플레이하며 재조정 |
| sleepSpeedLimit 0.18 | `Rigidbody.sleepThreshold`. 단위가 에너지이므로 재조정 |
| step 1/60 | Fixed Timestep 0.01667 |
| 시작 전 90스텝 | `Physics.simulationMode = Script`로 두고 `Physics.Simulate(1/60f)` 90회 후 `FixedUpdate`로 복귀 |

- 물건 수는 약 40개다. Web의 PhysX는 단일 스레드이므로 Sleep이 잘 동작하는지 확인한다.
- 설정 (적용됨): 기본 재질 `Core/Settings/PhysicsMaterial_Default`(마찰 0.55, 반발 0.06, Average), 솔버 반복 7, 고정 스텝 1/60, Enhanced Determinism 켬. 레이어 8~12는 `Player`, `Clutter`, `Furniture`, `Phone`, `Door`이고 `Player`↔`Clutter` 충돌은 끔.
- 감쇠와 잠들기 값은 `GameTuning`의 Clutter 항목이다. Unity `sleepThreshold`는 질량당 운동 에너지(½v²)라서 프로토타입 0.18m/s는 0.016이 된다.

### 물건 배치 (`Feature/Clutter`)

| 요소 | 내용 |
|---|---|
| `ClutterCatalog_Default` | 종류 6개(크기, 질량, 부드러움, 색), 더미 구성(`pileMix`, `enclosedMix`, 흔들림), 흩뿌림 종류, 기준 머티리얼(`Clutter_Item`, URP Simple Lit) |
| `ClutterLook` | 메시와 머티리얼 캐시. 부드러운 물건은 종류마다 변형 4개를 만들어 돌려 쓴다: 쿠션은 윤곽이 둥근(초타원) 불룩한 베개, 셔츠와 담요는 모서리가 둥근 천 2~3겹을 쌓은 개어 놓은 빨래 (11/13, 프로토타입의 물결 상자 대신). 잡을 때 휘도록 CPU 쪽 정점을 남겨 둔다. 색마다 머티리얼을 하나 복제한다 |
| `ClutterItem` 휘기 | 천(셔츠, 담요)은 처음 잡거나 던질 때 펼친 천 한 장(두께 2~3cm, 테두리 띠로 앞뒤를 이음)으로 바꿔 작은 천 시뮬레이션(`ClothDrape`, 12 x 12 점 Verlet, 거리 제약)을 돌린다. 잡혔거나 던져서 처음 닿기 전까지는 가운데 2 x 2 점을 물건 중심에 붙이고, 그 뒤에는 약하게만 당겨 천이 충돌체(조준 판정) 근처에 머물게 한다 (다시 붙이면 발작하듯 튀었다). 감쇠는 물건 속도 기준이라 천이 손을 따라 움직이고 흔들림만 조금 남는다. 점마다 이번 스텝에 지나간 길로 레이를 쏘아 가구, 바닥, 다른 물건 위에 멈추게 해서(자기 충돌체 무시, 들고 있을 때는 다른 물건 무시) 떨어진 곳 모양대로 걸친다. 가라앉으면 모양을 물건 로컬로 굳히고, 다시 집으면 그 모양에서 이어 간다. 그릴 때는 Catmull-Rom으로 23 x 23으로 늘리고 법선을 직접 계산해 꺾인 곳에서 삼각형이 드러나지 않게 한다. 쿠션은 원호로 조금 굽히기만 한다. 충돌체는 모두 상자 그대로다 |
| `ClutterPool` | `ClutterItem` 64개를 미리 만들고 판마다 재사용한다 |
| `ClutterStep` | 판 준비 단계 (Layout 다음, Phone 앞). 더미 쌓기, 흩뿌리기, `Physics.Simulate` 90스텝 |
| `ClutterItem` | Rigidbody(보간, ContinuousSpeculative) + BoxCollider. `IThrowable` 구현 |

- **자리 맞추기:** 회색 박스 모듈의 숨는 자리 15곳 중 7곳에서 맨 아래 물건이 벽, 등받이, 욕조 테두리와 겹쳤다. 겹친 채 시작하면 물리가 밀어내다 바닥이나 얇은 욕조 바닥 아래로 빠뜨린다. 그래서 놓기 전에 맞춘다.
  - 윗면이 물건 가운데보다 낮은 것(러그, 욕조 바닥, 좌석)은 그 위로 올린다.
  - 그 밖(벽, 등받이, 테두리)은 옆으로 민다. 밀 거리는 위아래로 길게 늘인 상자로 `ComputePenetration`을 해서 잰다. 원래 크기로 재면 가장 짧은 탈출 방향이 아래쪽이 되어 옆으로 밀리지 않는다.
  - 무작위로 돌린 채 안 들어가면 돌리지 않고 다시 맞춘다 (바구니는 작은 셔츠와 폭이 거의 같다).
  - 그래도 안 들어가는 맨 아래 물건은 `fallbackBase`(작은 셔츠)로 바꾼다. 지금은 욕조의 담요만 해당한다. 위로 쌓는 물건은 빠뜨린다.
  - 주의: `ComputePenetration`은 꺼진 콜라이더에서 항상 false다. 탐침은 켜 둔 트리거로 Clutter 레이어에 두고, 맞닿기만 한 경우(거리 0)도 true가 나오므로 1mm 넘게 박힌 것만 겹침으로 센다.
- 검증 (11/11, 에디터, 시드 1~100): 판당 물건 31~66개. 바닥 아래로 빠짐, 집 밖, 벽·가구에 박힘 0건. 대체 물건 83건(욕조 담요). 안정화 평균 8.6ms, 최대 11.8ms (첫 판은 메시 생성 포함 약 57ms). 안정화 후 깨어 있는 물체 평균 0.2개. 같은 시드를 다른 판 사이에 다시 만들면 물건 수와 종류는 같고 위치는 최대 3.6mm 다르다 (PhysX 해석 오차). 던진 물건은 1.4초 뒤 다시 잠든다.
- 확인용 씬: `Feature/Clutter/Sandbox/ClutterSandbox.unity`. `Interactor`를 붙여 잡아 던지기를 해 본다 (5장). 잡기·던지기 값을 슬라이더로 바꾸고(Tab으로 접기), 마지막으로 던진 속도와 놓친 횟수를 보여준다. 첫 클릭은 마우스 잠금에만 쓴다. R은 새 판.
- Web 안정화 시간 (11/12, Development 빌드, 앱 내장 Chrome, 8판): 3.5~17.8ms, 물건 33~57개. 에디터와 비슷하다. 개발 빌드에서만 `[ClutterStep] Settle` 로그를 남긴다.
- 폰에는 콜라이더를 달지 않는다. 물건 더미가 가라앉은 뒤 배치하는 논리 오브젝트다. 조준도 콜라이더 없이 레이와의 거리로 한다 (5장).

### 폰 (`Feature/Phone`)

| 요소 | 내용 |
|---|---|
| `PhoneStep` | 판 준비 단계 (Clutter 다음, Player 앞. Player가 폰 방을 피해 시작한다). 집 전체 숨는 자리 중 하나를 `RoundStream.Phone`으로 고르고, 자리 방향 기준 좌우 ±0.06, 앞뒤 ±0.05m, 회전 ±28.6°로 둔다 (프로토타입 값) |
| `PhoneDevice` | `IGrabbable`. 원점은 표면이고 중심은 0.012m 위다. `RoundContext`의 `Phone`, `PhonePosition`, `PhoneRoom`, `Covers`를 쓴다. 프리팹 `Phone.prefab`(몸체 Simple Lit, 화면 Unlit) |

- 덮임 수: 폰 위 기둥 범위의 Clutter 콜라이더를 `OverlapBox`로 모은 뒤 프로토타입 `countCovers` 조건으로 센다. 물건 중심 높이가 폰 중심 -`coverBelow` ~ +`coverAbove`, 폰 중심을 물건 로컬로 옮겼을 때 가로세로가 반 크기 + `coverMargin` 안. 판 진행 중(`Playing`, `Answer`) 매 프레임 갱신한다.
- 진동: `Playing`에서 판 경과 `jiggleDelay`부터 `jiggleInterval`마다, 수평 `jiggleRadius`·높이차 `jiggleHeight` 안의 물건에 위로 `jiggle` 속도를 더하고 회전을 `RoundStream.Vibration`에서 뽑는다. 벨이 울리는 판에서는 속도와 회전에 `ringJiggleScale`(0.4)을 곱하고, `RoundContext.VibrationMode`(Modifiers가 정함)일 때만 원래 세기다. 프로토타입 세기 그대로는 더미가 눈에 띄게 들썩여 소리 없이도 찾을 수 있었다. 확인 (시드 11, 물건 3개): 벨 판 0.22m/s, 진동 모드 0.55m/s. 벨소리 주기를 Phone이 모르므로 간격은 튜닝 값이다 (기본 벨 2초의 절반). 발신자별 벨소리를 넣을 때 주기가 달라지면 맞춘다.
- 폰 흔들림과 화면: 덮임 0개일 때 시각 요소(`Visual`)만 좌우로 떤다. 화면은 울리는 동안 깜박이고, `Missed`에서 빨간색으로 바뀐다. 받기 화면(`Answer`)에서만 숨기고 매 프레임 상태로 다시 정하므로, 프로토타입처럼 다음 판까지 숨은 채 남지 않는다 (7장).
- 검증 (11/12, 에디터, 시드 1~100): 숨는 자리 15곳 중 14곳이 뽑혔다. 가구에 박힘, 받침 없이 뜸, 방 번호 불일치, 플레이어와 같은 방에서 시작 0건. 같은 시드를 다시 만들면 위치가 같다. 덮임 수는 1개 1판, 2개 29판, 3개 38판, 4개 32판. 진동 12번 뒤에도 덮임 수가 그대로였다 (더미가 무너지지 않음). 덮인 폰은 조준되지 않고(물건이 조준됨), 덮은 물건을 치우면 덮임 0, 폰 조준, 클릭으로 `Answer` 전환과 폰 숨김을 확인했다.
- 확인용 씬: `Feature/Phone/Sandbox/PhoneSandbox.unity` (ClutterSandbox 복사본). 샌드박스에는 받기 슬라이드가 없으므로 클릭이나 Space로 받는다. F는 폰 위치 표시, V는 진동 모드 켜기(Modifiers 전까지 대용), 패널 슬라이더로 `ringJiggleScale`을 바꾼다(에셋을 직접 고침, Reset으로 되돌림). 판이 끝나면 2초 뒤 새 판. 벨소리는 아직 연결하지 않았다 (한 판 조립 때).

## 4. 플레이어 이동

방이 여러 개라 벽과 문이 생기므로, 프로토타입의 AABB 판정 대신 **CharacterController**(반지름 0.26)를 쓴다.

- 플레이어는 물건과 충돌하지 않는다. 이 동작이 조작감의 일부다. 레이어 충돌 매트릭스에서 `Player`와 `Clutter`의 충돌을 끈다.
- 벽, 가구 몸체, 닫힌 문만 플레이어를 막는다.
- 계단과 경사는 없다. 중력은 바닥에 붙이는 정도로만 준다. 가구 모서리를 타고 오르지 않게 `stepOffset` 0.05, `slopeLimit` 20으로 둔다.
- 프로토타입은 x-z로 바로 움직였지만, 걷는 느낌을 위해 다음을 더했다. 값은 모두 `GameTuning`의 Walk Feel에 있다.
  - 가속과 감속. 방향을 틀거나 멈출 때는 감속 값을 쓴다. 벽에 막힌 만큼 속도를 버리고, 벽을 따라 미끄러지는 성분은 남긴다.
  - 앞, 뒤, 옆 속도를 다르게 한다. 뒤로 걸을 때는 느리다.
  - 머리 흔들림(`HeadBob`). 걸은 거리를 `stepLength`로 나눠 걸음 위상을 만든다. 발이 닿을 때마다 머리가 내려가고(`|sin|`), 두 걸음에 한 번 좌우로 옮겨가며 기운다. 크기는 속도를 따라가므로 걷기 시작과 멈출 때 서서히 생기고 사라진다. 발이 닿을 때 `Stepped` 이벤트가 나오며, 발소리에 쓸 수 있다.
- 구조: `Player`(CharacterController, `PlayerMotor`, `PointerLook` yaw) → `Gait`(`HeadBob`, 눈 높이와 흔들림) → `Head`(카메라, pitch). 흔들림과 시선 회전이 서로 덮어쓰지 않게 트랜스폼을 나눴다.
- `PlayerMotor`는 `Playing` 상태에서만 입력을 받는다. 그 외 상태에서는 미끄러지듯 멈춘다. 매 프레임 `RoundContext.PlayerPosition`(귀 높이)과 `PlayerRoom`을 쓴다.
- 확인용 씬: `Feature/Player/Sandbox/WalkSandbox.unity`. 바로 판을 시작하고, 걷기 튜닝 값을 슬라이더로 바꾼다. 슬라이더는 `GameTuning_Default` 에셋을 직접 고치므로, 에디터에서는 플레이를 멈춰도 값이 남는다(Reset 버튼으로 되돌림). R은 새 집, Tab은 패널 토글이다.

## 5. 조준

- 레이어를 `Furniture`, `Clutter`, `Phone`, `Door`로 나눈다. 벽은 `Furniture`에 포함한다. Raycast는 도달 거리 안의 첫 충돌만 본다.
- 폰 판정은 레이캐스트 명중이 아니라 **중심 레이와 폰 사이 거리 ≤ 0.17m + 시야 차단 검사**다 (`phoneInSight`). 덮인 물건이 있으면 시야가 막힌다.
- `solid: false`이면서 시야를 막는 가구(탁자 다리, TV, 스탠드)는 플레이어 이동에는 영향이 없고 물건과는 충돌하지 않는다. 트리거 콜라이더 + `Furniture` 레이어로 처리하고 Raycast는 `QueryTriggerInteraction.Collide`를 쓴다.

### 잡아 던지기 (`Feature/Interaction/Interactor`)

**프로토타입과 조작이 다르다.** 프로토타입은 클릭한 순간 정해진 속도로 던졌다 (`act()`). 이 게임은 누르면 잡고, 떼면 던진다. 값은 [`game-design.md`](../design/game-design.md)의 Hold, Throw 항목이다.

- 조준: 매 프레임 화면 중심 레이(도달 거리, 플레이어 레이어 제외)의 첫 충돌에서 `IAimTarget`을 찾아 `SetAimed`로 강조한다 (물건은 기본색에 0x3a3220을 더함, `MaterialPropertyBlock`). 잡고 있는 동안은 조준하지 않는다. 조준 대상은 `RoundContext.AimTarget`에 기록한다 (Ui 라벨용).
- 폰 우선: 물건 레이보다 먼저 `RoundContext.Phone`을 본다. 폰 중심(`GrabPoint`)이 도달 거리 안이고, 화면 중심 레이와의 거리가 `phoneAimRadius` 이하이고, 눈에서 폰 중심 0.02m 앞까지 레이가 아무것도(물건, 가구 트리거 포함) 맞히지 않으면 폰을 조준한다. 클릭하면 `IGrabbable.Grab()` 후 `GameFlow.PickUp()`을 부른다.
- 잡기: 물건(`IThrowable`)은 순간이동하지 않고 속도로 끌려온다. 매 물리 스텝 `(잡는 점 − 중심) × holdResponse`(손에 들어온 뒤에는 잡는 점의 이동 속도를 더함)를 `holdMaxSpeed`로 자른다. 그래서 벽은 뚫지 않고 다른 물건은 밀어낸다. 잡힌 동안 중력을 끈다.
- 깔린 물건: 물건의 회전을 따르는 윗면 바로 위에 얇은 상자(`buriedCheckHeight`, 가로세로는 `buriedCheckInset` 비율만큼 좁힘)를 두고, 닿으면서 중심이 더 높은 Clutter 물건을 후보로 본다. 후보의 중심이 윗면 안쪽 위에 있거나(이 물건에 기대 있음), 후보들이 윗면 격자 3×3의 `buriedCoverage` 이상을 덮으면 `Buried`다. 가장자리에 걸치기만 한 물건은 세지 않는다. 처음에는 월드 축 상자로 재서 돌아간 물건은 상자가 넓어져 옆 물건까지 셌다.
  - 처음 놓인 더미에서는 판정을 바꿔도 깔림이 248개에서 244개로 줄 뿐이다 (더미가 가지런하다). 완화는 플레이 중 흐트러진 더미에서 가장자리에 걸친 물건을 위한 것이다. 큰 담요가 작은 책 위에 걸치는 경우처럼 위 물건 중심이 밖이어도 절반 이상 덮였으면 깔린 것이다.
  - 깔린 물건은 잡지 않고 `tugTime` 동안 손 쪽 **수평** 방향으로 `tugDistance`만큼 끌어내다 멈춘다. 처음에는 깔린 물건도 끌어오고 질량을 키워 더미를 밀어내게 했는데, 위에 얹힌 물건이 함께 날아갔다. 손 쪽(대개 위)으로 당기면 얹힌 더미를 들어 올려 튀게 하므로 위로는 당기지 않는다.
  - 검증 (시드 7000~7007): 물건 422개 중 244개가 깔림. 깔린 물건을 잡으려 한 23번 모두 잡히지 않았고, 평균 3.3cm 끌려 나왔다. 주변 물건의 최고 속도는 평균 0.4m/s. 탁자 끝 더미에서는 위 물건이 미끄러져 떨어지기도 한다. 맨 위 물건은 24/24 잡혔다.
- 잡는 점: 눈과 잡는 점 사이를 레이로 보고 벽이나 가구가 있으면 그 앞으로 당긴다. 이것이 없으면 벽을 보고 서 있을 때 물건이 벽 끝(문틀)을 돌아 옆방으로 끌려갔다 (벽 앞 시험 29회 중 6회 → 31회 중 1회, 남은 1회도 문틀에 걸친 정도).
- 던지기: 떼면 `시선 수평 방향 × tapThrowForward + 위 × tapThrowUp + 휘두른 속도 × flickScale`, 최대 `maxThrowSpeed`. 휘두른 속도는 가로막힘을 따지지 않은 잡는 점의 최근 `flickWindow` 동안 평균 이동 속도라서 걷는 속도도 포함된다. 회전은 `RoundStream.Throw`에서 뽑는다.
- 놓치기: 손에 들어온 뒤 `holdBreakDistance`보다 멀어지거나, `holdPullTimeout` 안에 손에 들어오지 않으면 속도 0으로 놓는다.
- 둘러싸인 자리(바구니, 욕조): 먼저 `enclosedLift`만큼 수직으로 들고, 손에 들어올 때까지 그 높이 아래로 내리지 않는다. 내려다보고 있으면 잡는 점이 욕조 테두리보다 낮아 걸렸다.
- 클릭 입력: `PointerLook`이 구현한 `Core/Aim/IPointerGate.CanAct`로 잠금 클릭을 거른다. Interaction은 Player를 참조하지 않는다.
- 검증 (에디터, 시드 5000~5009, 판마다 물건 12개를 잡아 3가지 방식으로 던짐):

  | 항목 | 결과 |
  |---|---|
  | 잡기 | 120/120 성공, 바구니·욕조 16/16 |
  | 손에 들어오는 시간 | 평균 0.10초, 최대 0.19초 |
  | 탭 던지기 | 2.7m/s |
  | 휘두르기 (0.1초에 60° 옆, 40° 위) | 평균 5.6, 6.5m/s |
  | 바닥 아래·집 밖으로 빠진 물건 | 473개 중 0 |
  | 3초 뒤 깨어 있는 물건 | 0 |

  - 휘두르는 손맛은 직접 해 보고 맞춘다.

## 6. 렌더링

- `MeshLambertMaterial`은 URP **Simple Lit**에 대응한다. 플랫 셰이딩 물건은 삼각형마다 정점을 분리한 메시로 만든다.
- 런타임 색 변경은 기준 머티리얼 에셋을 참조해 `MaterialPropertyBlock` 또는 복제본으로 처리한다. `Shader.Find`는 빌드에서 null이 될 수 있다.
- 강조 표시용 Emission은 기준 머티리얼에서 켜 두어 `_EMISSION` 변형이 빌드에 포함되게 한다.
- 조명은 모듈별 라이트맵과 그림자 1개 전략을 따른다. 상세는 [`level-generation.md`](level-generation.md#조명-전략).
- Web이 쓰는 `Mobile_RPAsset`은 현재 **Additional Light Shadows가 꺼져 있다** (Render Scale 0.8). 조명을 구현할 때 켠다.

## 7. 입력과 웹 환경

- 커서 잠금은 `Cursor.lockState`를 쓴다. 브라우저는 사용자 제스처 뒤에만 허용하고, Esc로 풀리면 다음 클릭에서 다시 잠근다 (프로토타입의 `pointerdown` 처리와 동일).
  - 구현: `Feature/Player/PointerLook`. 잠기지 않은 상태의 클릭은 잠금 요청에만 쓰고, 버튼을 뗄 때까지 `CanAct`를 false로 둔다 (첫 클릭이 던지기가 되지 않게). 잠긴 직후 2프레임의 이동량은 버린다.
  - `flow`가 연결되어 있으면(`Main.unity`) playing에서만 잠근다. 판이 시작되면 `StateChanged`에서 바로 잠금을 요청하고(시작 버튼을 누른 프레임, R을 누른 프레임), 그 밖의 상태(타이틀, 받기, 결과)로 바뀌면 풀어서 커서를 보여준다. playing 중 Esc로 풀리면 다음 클릭에서 다시 잠근다. 샌드박스는 `flow`가 비어 있어 예전처럼 아무 클릭으로 잠근다.
  - 확인용 씬: `Feature/Player/Sandbox/PointerSandbox.unity`. 잠금, 해제 횟수, 잠금 클릭과 던지기 수, 이동량을 TMP로 표시한다.
  - Unity의 `Cursor.lockState`는 **Esc로 풀린 것도, 브라우저가 거부한 것도 반영하지 않는다** (Chrome과 앱 내장 브라우저에서 확인). 그래서 `PointerLockWeb.jslib`로 `document.pointerLockElement`를 읽어 실제 잠금 상태로 쓴다. 브라우저가 풀었거나 요청 후 1초 안에 잠그지 않으면 Unity 쪽도 `None`으로 돌려 다음 클릭에서 다시 요청한다.
  - 프레임 하나 안에 눌렀다 뗀 클릭은 `wasPressedThisFrame`이 놓칠 수 있다. 사람의 클릭은 보통 여러 프레임에 걸치므로 문제없지만 저프레임에서 확인한다.
- 모바일 터치는 지원하지 않는다. 프로토타입의 터치 처리(탭, 길게 눌러 걷기)는 옮기지 않는다.
- 받기 슬라이드는 프로토타입(잠긴 채 마우스 가로 이동 210px)과 다르다. 포인터를 풀고 손잡이를 커서로 끈다 (8장). `slideNeeded`는 없앴다.
- 탭을 벗어나도 타이머는 계속 흐른다 (`Run In Background` 켬). 포커스를 잃으면 키 입력 상태를 비운다 (프로토타입의 `blur` 처리).

## 8. UI와 폰트

- uGUI + TextMeshPro로 구현한다. HTML 오버레이의 `handset` 패널이 프리팹 하나에 대응한다.
  - 구현 (11/12): `Feature/Ui/GameUi.prefab` 하나에 타이틀, HUD(`HudView`), 조준점(`AimView`), 받기(`AnswerSlide`), 결과(`ResultView`)가 들어 있다. `Missed Call/Ui/Build Game Ui` 메뉴(`Ui/Editor/GameUiBuilder`)가 프리팹, 스프라이트(원, 링, 둥근 패널과 테두리 9-slice), 글자 그림자 머티리얼을 다시 만든다. 씬에서는 `GameUi`의 `flow`만 연결한다.
  - Canvas Scaler는 1280×720 기준, 높이 맞춤이다. 기본 Web 캔버스 높이(600)에서 작아지므로 글자를 프로토타입 CSS보다 1~2pt 키웠다.
  - 타이틀의 `Ring It`과 결과의 `Try Again`은 커서로 버튼 위를 눌러야 시작한다. 버튼 밖 클릭은 무시한다. 결과 직후 `resultInputDelay` 동안은 막는다. 누름 판정은 `GameUi`가 버튼 이미지 사각형으로 직접 하고, **누르는 순간** 시작한다. uGUI 버튼은 뗄 때 눌린다. 그런데 브라우저는 사용자 입력 이벤트 안에서만 포인터 잠금을 허용하고, Unity는 잠금 요청을 다음 입력 이벤트에서 처리한다. 그래서 누르는 프레임에 시작해야 같은 클릭의 mouseup에서 잠긴다. 이 때문에 EventSystem을 두지 않는다. 커서가 버튼 위에 있으면 버튼이 밝아진다.
  - 커서: 포인터가 풀린 동안 가운데 조준점과 같은 링 모양 커서(`Sprites/Ui_Cursor.png`, 32px, 어두운 테두리)를 `Cursor.SetCursor`로 쓴다. Web에서는 CSS 커서라 프레임 지연이 없다.
  - 받기 슬라이드: 폰을 집으면 잠금이 풀린다. 손잡이를 커서로 누른 채 끌면 따라오고, 트랙 끝에 닿으면 받는다. 중간에 놓으면 0.2초에 걸쳐 처음 자리로 돌아간다. 폰을 집은 클릭이 아직 눌려 있어도 새로 눌러야 끈다. 트랙만 누르고 끌면 움직이지 않는다. 에디터에서 입력 이벤트를 넣어 끌기, 놓으면 복귀, 트랙 누름 무시, 끝까지 끌면 `Won`을 확인했다. Chrome에서 실제 감각 확인은 남았다.
  - 커서가 안 보이던 문제 (11/12): Unity Web의 `Cursor.visible = true`는 캔버스 커서를 `"default"`로 덮어써 전용 커서를 지운다 (`_JS_Cursor_SetShow`). 그래서 `GameUi.LateUpdate`가 커서가 다시 보인 프레임마다 `Cursor.SetCursor`를 다시 부르고, `PointerLook.Unlock`은 값이 바뀔 때만 `Cursor.visible`을 건드린다.
  - 일시정지 (11/12): `GameFlow.Pause/Resume/ReturnToTitle`, `IsPaused`, `PausedChanged`. `RoundState`에 넣지 않은 이유는 여러 기능이 Playing 전환을 새 판으로 보기 때문이다 (재개가 새 판이 되면 안 된다). 멈추면 `Time.timeScale = 0`(물리, `deltaTime` 기반 이동과 흔들림), 타이머 정지(재개 때 멈춘 시간만큼 `startTime`을 민다), Interactor는 든 물건을 놓고 입력을 무시, AnswerSlide는 끌기를 멈춘다. 벨소리는 jslib `MC_Audio_SetPaused`가 박자 예약을 멈추고 마스터 볼륨을 0으로 내린다. `AudioContext.suspend()`를 쓰지 않는 이유는 jslib의 resume 리스너가 메뉴 클릭(pointerdown)에서 다시 켜 버리기 때문이다. 에디터 근사치는 `AudioListener.pause`. 진입: `PointerLook`이 playing 중 잠겨 있다가 풀린 것을 보면(Esc, 탭 전환) `Pause()`를 부른다. 잠금을 얻기 전(요청 중, 거부)은 해당하지 않는다. 잠금이 없을 때의 Esc 키는 `GameUi`가 받는다. 에디터에서 잠금 해제, Resume(경과 시간이 멈춘 값부터 이어짐), Restart, 받기 화면 Esc, Title을 확인했다. Chrome에서 Esc 메뉴, Resume 재잠금, 탭 전환, 받기 화면 Esc를 확인했다.
  - 일시정지 중 Esc로 재개: HTML 명세상 Esc keydown은 사용자 활성화(user activation)가 아니라서 그 안에서 포인터 잠금을 요청할 수 없다. 그래서 playing에서는 Esc가 메뉴를 닫고 `ResumeHint`("Click to resume")를 띄우며, 다음 클릭(어디든)에서 `Resume()`과 잠금 요청을 함께 한다. 받기 화면은 잠금이 필요 없어 Esc로 바로 재개한다. 잠금을 푼 Esc가 같은 순간 키 입력으로도 들어오면 메뉴가 바로 닫힐 수 있어, 멈춘 뒤 0.25초(`escResumeDelay`)는 Esc 재개를 무시한다. Chrome은 Esc로 잠금을 푼 직후 짧은 시간 재잠금을 거부할 수 있다. 그 경우 PointerLook이 1초 뒤 요청을 거두고, 다음 클릭에서 다시 잠근다.
  - 발신자: `Core/Callers/CallerCatalog_Default`에서 `CallerStep`(판 준비 첫 단계)이 `Caller` 스트림으로 고르고 `RoundContext.Caller`에 넣는다. Ui(문구)와 Ringtone(벨소리, 4단계)이 함께 읽으므로 Core에 둔다.
  - 에디터 확인 팁: Editor가 포커스를 잃으면 Input System이 마우스를 꺼서, 스크립트로 넣은 입력 이벤트가 버려진다. 테스트하는 동안만 `InputSystem.settings`의 복사본에서 `editorInputBehaviorInPlayMode`와 `backgroundBehavior`를 바꾸고 `InputSystem.EnableDevice(Mouse.current)`를 부른다. 플레이 모드가 아닐 때 `StartRound`를 부르면 편집 중인 씬에 집이 생기므로 `EditorApplication.isPlaying`을 먼저 확인한다.
- 게임 텍스트는 영어만 쓴다. 프로토타입의 한국어 문구를 옮기지 말고 [`game-design.md`](../design/game-design.md)의 영어 문구를 쓴다.
- Web에는 OS 폰트가 없다. Black Han Sans와 IBM Plex Sans를 프로젝트에 넣고 ASCII 범위의 **정적 TMP 아틀라스**로 만든다. 한글 글리프가 없어 아틀라스가 작다.
  - 검증 (11/11): TMP 기본 LiberationSans로 ASCII 95자 정적 아틀라스(512², SDFAA, 48pt, Mobile Distance Field 셰이더)를 만들어 Web 빌드에서 모든 글자가 표시됨을 확인했다. 에셋은 `Shared/Fonts/UiFont_LiberationSans SDF` (빌드에서 266KB).
  - 만드는 법: `TMP_FontAsset.CreateFontAsset(ttf, 48, 5, SDFAA, 512, 512)` → `TryAddCharacters`(32~126) → 아틀라스와 머티리얼을 하위 에셋으로 저장 → `atlasPopulationMode = Static`.
  - 크기 주의: `Assets/TextMesh Pro/Resources`는 쓰지 않아도 빌드에 들어간다. 기본 폰트 `LiberationSans SDF`(1MB), `LiberationSans.ttf`(344KB), `EmojiOne.png`(341KB)가 포함된다. 최종 폰트를 정하면 TMP Settings의 기본 폰트를 바꾸고 이 세 개를 빼서 약 1.7MB를 줄인다.
- `backdrop-filter` 블러는 옮기지 않는다. 반투명 패널로 대체한다.
- 벨 박자에 맞춘 화면 깜빡임은 프레임 시간이 아니라 실제 시간 기준으로 계산한다.

## 9. 프로토타입에서 고칠 버그

- `answer` 상태에서 시간이 끝나면 `phone.visible = false`가 복구되지 않는다. 그 라운드의 실패 연출에서 폰이 안 보이고 다음 라운드 내내 폰이 보이지 않는다. **라운드 초기화에서 폰을 반드시 다시 표시한다.**
