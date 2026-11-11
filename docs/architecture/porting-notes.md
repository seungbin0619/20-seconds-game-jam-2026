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
- 폰에는 물리 콜라이더를 달지 않는다. 물건 더미가 가라앉은 뒤 배치하는 논리 오브젝트다. 조준용 콜라이더는 트리거 레이어로 분리한다.

## 4. 플레이어 이동

방이 여러 개라 벽과 문이 생기므로, 프로토타입의 AABB 판정 대신 **CharacterController**(반지름 0.26)를 쓴다.

- 플레이어는 물건과 충돌하지 않는다. 이 동작이 조작감의 일부다. 레이어 충돌 매트릭스에서 `Player`와 `Clutter`의 충돌을 끈다.
- 벽, 가구 몸체, 닫힌 문만 플레이어를 막는다.
- 계단과 경사는 없다. 중력은 바닥에 붙이는 정도로만 준다.

## 5. 조준

- 레이어를 `Furniture`, `Clutter`, `Phone`, `Door`로 나눈다. 벽은 `Furniture`에 포함한다. Raycast는 도달 거리 안의 첫 충돌만 본다.
- 폰 판정은 레이캐스트 명중이 아니라 **중심 레이와 폰 사이 거리 ≤ 0.17m + 시야 차단 검사**다 (`phoneInSight`). 덮인 물건이 있으면 시야가 막힌다.
- `solid: false`이면서 시야를 막는 가구(탁자 다리, TV, 스탠드)는 플레이어 이동에는 영향이 없고 물건과는 충돌하지 않는다. 트리거 콜라이더 + `Furniture` 레이어로 처리하고 Raycast는 `QueryTriggerInteraction.Collide`를 쓴다.

## 6. 렌더링

- `MeshLambertMaterial`은 URP **Simple Lit**에 대응한다. 플랫 셰이딩 물건은 삼각형마다 정점을 분리한 메시로 만든다.
- 런타임 색 변경은 기준 머티리얼 에셋을 참조해 `MaterialPropertyBlock` 또는 복제본으로 처리한다. `Shader.Find`는 빌드에서 null이 될 수 있다.
- 강조 표시용 Emission은 기준 머티리얼에서 켜 두어 `_EMISSION` 변형이 빌드에 포함되게 한다.
- 조명은 모듈별 라이트맵과 그림자 1개 전략을 따른다. 상세는 [`level-generation.md`](level-generation.md#조명-전략).
- Web이 쓰는 `Mobile_RPAsset`은 현재 **Additional Light Shadows가 꺼져 있다** (Render Scale 0.8). 조명을 구현할 때 켠다.

## 7. 입력과 웹 환경

- 커서 잠금은 `Cursor.lockState`를 쓴다. 브라우저는 사용자 제스처 뒤에만 허용하고, Esc로 풀리면 다음 클릭에서 다시 잠근다 (프로토타입의 `pointerdown` 처리와 동일).
  - 구현: `Feature/Player/PointerLook`. 잠기지 않은 상태의 클릭은 잠금 요청에만 쓰고, 버튼을 뗄 때까지 `CanAct`를 false로 둔다 (첫 클릭이 던지기가 되지 않게). 잠긴 직후 2프레임의 이동량은 버린다.
  - 확인용 씬: `Feature/Player/Sandbox/PointerSandbox.unity`. 잠금, 해제 횟수, 잠금 클릭과 던지기 수, 이동량을 TMP로 표시한다.
  - Unity의 `Cursor.lockState`는 **Esc로 풀린 것도, 브라우저가 거부한 것도 반영하지 않는다** (Chrome과 앱 내장 브라우저에서 확인). 그래서 `PointerLockWeb.jslib`로 `document.pointerLockElement`를 읽어 실제 잠금 상태로 쓴다. 브라우저가 풀었거나 요청 후 1초 안에 잠그지 않으면 Unity 쪽도 `None`으로 돌려 다음 클릭에서 다시 요청한다.
  - 프레임 하나 안에 눌렀다 뗀 클릭은 `wasPressedThisFrame`이 놓칠 수 있다. 사람의 클릭은 보통 여러 프레임에 걸치므로 문제없지만 저프레임에서 확인한다.
- 모바일 터치는 지원하지 않는다. 프로토타입의 터치 처리(탭, 길게 눌러 걷기)는 옮기지 않는다.
- 슬라이드 210px은 화면 픽셀 기준이다. Canvas Scaler 환경에서 `Screen.dpi`나 참조 해상도로 보정한다.
- 탭을 벗어나도 타이머는 계속 흐른다 (`Run In Background` 켬). 포커스를 잃으면 키 입력 상태를 비운다 (프로토타입의 `blur` 처리).

## 8. UI와 폰트

- uGUI + TextMeshPro로 구현한다. HTML 오버레이의 `handset` 패널이 프리팹 하나에 대응한다.
- 게임 텍스트는 영어만 쓴다. 프로토타입의 한국어 문구를 옮기지 말고 [`game-design.md`](../design/game-design.md)의 영어 문구를 쓴다.
- Web에는 OS 폰트가 없다. Black Han Sans와 IBM Plex Sans를 프로젝트에 넣고 ASCII 범위의 **정적 TMP 아틀라스**로 만든다. 한글 글리프가 없어 아틀라스가 작다.
  - 검증 (11/11): TMP 기본 LiberationSans로 ASCII 95자 정적 아틀라스(512², SDFAA, 48pt, Mobile Distance Field 셰이더)를 만들어 Web 빌드에서 모든 글자가 표시됨을 확인했다. 에셋은 `Shared/Fonts/UiFont_LiberationSans SDF` (빌드에서 266KB).
  - 만드는 법: `TMP_FontAsset.CreateFontAsset(ttf, 48, 5, SDFAA, 512, 512)` → `TryAddCharacters`(32~126) → 아틀라스와 머티리얼을 하위 에셋으로 저장 → `atlasPopulationMode = Static`.
  - 크기 주의: `Assets/TextMesh Pro/Resources`는 쓰지 않아도 빌드에 들어간다. 기본 폰트 `LiberationSans SDF`(1MB), `LiberationSans.ttf`(344KB), `EmojiOne.png`(341KB)가 포함된다. 최종 폰트를 정하면 TMP Settings의 기본 폰트를 바꾸고 이 세 개를 빼서 약 1.7MB를 줄인다.
- `backdrop-filter` 블러는 옮기지 않는다. 반투명 패널로 대체한다.
- 벨 박자에 맞춘 화면 깜빡임은 프레임 시간이 아니라 실제 시간 기준으로 계산한다.

## 9. 프로토타입에서 고칠 버그

- `answer` 상태에서 시간이 끝나면 `phone.visible = false`가 복구되지 않는다. 그 라운드의 실패 연출에서 폰이 안 보이고 다음 라운드 내내 폰이 보이지 않는다. **라운드 초기화에서 폰을 반드시 다시 표시한다.**
