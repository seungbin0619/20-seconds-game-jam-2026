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

CharacterController를 쓰지 않는다. 프로토타입의 AABB 판정(`blocked`, 반지름 0.26, 축별 분리 이동)을 그대로 옮긴다. 플레이어는 물건과 충돌하지 않으며, 이 동작이 조작감의 일부다.

## 5. 조준

- 레이어를 `Furniture`, `Clutter`, `Phone`으로 나눈다. Raycast는 도달 거리 안의 첫 충돌만 본다.
- 폰 판정은 레이캐스트 명중이 아니라 **중심 레이와 폰 사이 거리 ≤ 0.17m + 시야 차단 검사**다 (`phoneInSight`). 덮인 물건이 있으면 시야가 막힌다.
- `solid: false`이면서 시야를 막는 가구(탁자 다리, TV, 스탠드)는 플레이어 이동에는 영향이 없고 물건과는 충돌하지 않는다. 트리거 콜라이더 + `Furniture` 레이어로 처리하고 Raycast는 `QueryTriggerInteraction.Collide`를 쓴다.

## 6. 렌더링

- `MeshLambertMaterial`은 URP **Simple Lit**에 대응한다. 플랫 셰이딩 물건은 삼각형마다 정점을 분리한 메시로 만든다.
- 런타임 색 변경은 기준 머티리얼 에셋을 참조해 `MaterialPropertyBlock` 또는 복제본으로 처리한다. `Shader.Find`는 빌드에서 null이 될 수 있다.
- 강조 표시용 Emission은 기준 머티리얼에서 켜 두어 `_EMISSION` 변형이 빌드에 포함되게 한다.
- 천장 조명은 Spot + 그림자인데, Web이 쓰는 `Mobile_RPAsset`은 **Additional Light Shadows가 꺼져 있다** (Render Scale 0.8). 조명을 구현할 때 켜고 성능을 확인한다.
- 동시에 쓰는 실시간 조명은 Spot 1, Point 2. Additional Light 수는 제한(4) 안에 든다.

## 7. 입력과 웹 환경

- 커서 잠금은 `Cursor.lockState`를 쓴다. 브라우저는 사용자 제스처 뒤에만 허용하고, Esc로 풀리면 다음 클릭에서 다시 잠근다 (프로토타입의 `pointerdown` 처리와 동일).
- 터치 판정: 이동량 10px 미만 + 260ms 미만이면 탭, 260ms 이상 유지하면 걷기. Input System의 `Pointer`/`Touchscreen`으로 구현한다.
- 슬라이드 210px은 화면 픽셀 기준이다. Canvas Scaler 환경에서 `Screen.dpi`나 참조 해상도로 보정한다.
- 탭을 벗어나도 타이머는 계속 흐른다 (`Run In Background` 켬). 포커스를 잃으면 키 입력 상태를 비운다 (프로토타입의 `blur` 처리).

## 8. UI와 폰트

- uGUI + TextMeshPro로 구현한다. HTML 오버레이의 `handset` 패널이 프리팹 하나에 대응한다.
- 게임 텍스트는 영어만 쓴다. 프로토타입의 한국어 문구를 옮기지 말고 [`game-design.md`](../design/game-design.md)의 영어 문구를 쓴다.
- Web에는 OS 폰트가 없다. Black Han Sans와 IBM Plex Sans를 프로젝트에 넣고 ASCII 범위의 **정적 TMP 아틀라스**로 만든다. 한글 글리프가 없어 아틀라스가 작다.
- `backdrop-filter` 블러는 옮기지 않는다. 반투명 패널로 대체한다.
- 벨 박자에 맞춘 화면 깜빡임은 프레임 시간이 아니라 실제 시간 기준으로 계산한다.

## 9. 프로토타입에서 고칠 버그

- `answer` 상태에서 시간이 끝나면 `phone.visible = false`가 복구되지 않는다. 그 라운드의 실패 연출에서 폰이 안 보이고 다음 라운드 내내 폰이 보이지 않는다. **라운드 초기화에서 폰을 반드시 다시 표시한다.**
