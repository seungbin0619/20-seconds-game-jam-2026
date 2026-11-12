# 로드맵

- **제출 마감: 2026-11-16**
- **목표 완료: 2026-11-15** (16일은 제출과 예비일)

작업을 끝낼 때마다 체크한다. 순서는 **가장 불확실한 것부터** 검증하도록 잡았다. 날짜가 밀리면 각 날의 "선택" 항목부터 뺀다.

## 0. 사전 정리 (완료)

- [x] `Assets/_Project/{Core,Shared,Feature}` 구조와 asmdef
- [x] 템플릿 에셋 정리, 씬을 `Core/Scenes/Main.unity`로 이동
- [x] 컨텍스트 문서 작성 (`CLAUDE.md`, `docs/`)
- [x] Web 플레이어 설정 (압축, 백그라운드 실행, 제품명)
- [x] Unity Web Build Support 모듈 설치
- [x] 활성 빌드 타깃을 Web으로 전환
- [x] 빈 씬 Web 빌드가 브라우저에서 뜨는지 확인 (약 16MB, WebGL 2)
- [x] 확장 기획 확정 (판 독립, 무작위성, 여러 방, 기록 저장)

## 1. 기술 검증 — 11/10 ~ 11/11

Web 빌드에서 확인한다. 실패하면 문서에 적힌 대안으로 바로 전환한다.

- [x] `Ringtone`: jslib Web Audio로 벨 재생, HRTF 방향감, 저역 통과 필터, 음원 2개 동시 재생
- [x] `Ringtone`: 문 쪽 음원 이동과 문 통과가 자연스러운지
- [x] `Ringtone`: 벽 먹먹함 동작 확인 (값 미세 조정은 5단계 수치 튜닝으로 이동)
- [x] `Room`: 회색 박스 모듈 3개 (욕실 S, 침실 M, 거실 L). `Missed Call/Room/Build Graybox Modules` 메뉴로 다시 만든다
- [x] `Room`: 모듈 라이트맵 굽기와 런타임 복원. Web 빌드에서 확인 (`Room/Sandbox/LightmapSandbox`, 빌드 약 16.7MB)
- [x] `Layout`: 회색 박스 모듈로 조립과 `RoomGraph` 확인 (템플릿 5개, 시드 300개 검증, `Layout/Sandbox/LayoutSandbox`)
- [x] 포인터 잠금 동작 (데스크톱만 지원). Chrome에서 잠금, Esc 해제, 재잠금, 첫 클릭 무시 확인. Esc 감지는 jslib로 브라우저 상태를 읽는다. 앱 내장 브라우저는 잠금을 거부하므로 Chrome으로 확인한다
- [x] 영문 TMP 폰트 표시와 빌드 크기 (ASCII 정적 아틀라스 266KB. TMP Resources 기본 에셋 약 1.7MB는 나중에 제거)

## 2. 코어 루프 — 11/12

- [x] `Core`: `GameFlow`, `RoundContext`, `GameTuning`, 시드 (`RoundSeed` 용도별 스트림, `RoundStep` 준비 단계, `Aim` 계약. EditMode 테스트 통과. 씬 연결은 한 판 조립 때)
- [x] `Player`: CharacterController 이동, 둘러보기 (가속/감속, 방향별 속도, 머리 흔들림과 `Stepped` 이벤트, `PlayerStep` 시작 위치, R 재시작. `LayoutStep` 포함. `WalkSandbox`에서 확인. Web 빌드에서 느낌 확인은 남음)
- [x] `Clutter`: 물건 종류, 더미 생성, 물리 안정화, 풀링 (자리 맞추기, 물리·레이어 설정, `ClutterSandbox`. 시드 100개 검증. Web 안정화 시간 확인은 한 판 조립 때)
- [x] `Interaction`: 조준, 잡아 던지기 (프로토타입과 다른 조작: 누르면 잡고 휘두르며 떼면 던짐. `ClutterSandbox`에서 확인. 폰 우선 조준, `AimTarget` 기록, 문 열기, 효과음은 Phone·문·오디오 단계에서)
- [ ] `Phone`: 배치, 덮임 계산, 진동, 집기
- [ ] 회색 박스 집에서 처음부터 끝까지 한 판이 돌아감

## 3. 집 생성과 UI — 11/13

- [ ] `Layout`: 템플릿 3개 이상 (일자, ㄱ자, T자)
- [ ] `Room`: 방 종류 3종 이상, 종류당 가구 세트 1~2개, `HideSpot` 배치
- [ ] `Ringtone`: 벽 먹먹함, 문 쪽 음원을 실제 집에 연결
- [ ] `Ui`: 타이틀, HUD, 조준점과 라벨, 받기 슬라이드, 결과 화면
- [ ] R 재시작, 실패 연출

## 4. 변수, 칭호, 기록 — 11/14

- [ ] `Ringtone`: 발신자별 벨소리 (녹음 파일을 쓰면 파일 재생 경로 추가, `porting-notes.md` 참고)
- [ ] `Modifiers`: 미끼 소리, 진동 모드, 정전
- [ ] `Result`: 칭호 판정, 기록 저장과 기록 화면
- [ ] 라이팅 굽기와 머티리얼, 분위기 맞추기
- [ ] 선택: 닫힌 문, 배터리 부족, 소음

## 5. 폴리시와 제출 준비 — 11/15

- [ ] 수치 튜닝 (특히 `walkSpeed`, 방 크기, 변수 확률, 벽 효과 `wallCutoffScale`/`wallGainScale`, `doorBlendRadius`, `rolloff`)
- [ ] 효과음 확정 (현재 프로토타입 소리는 임시)
- [ ] 버그 수정, 성능 확인 (Web에서 R 연타, 메모리)
- [ ] 저성능 대응: 소프트웨어 렌더러(`Basic Render Driver`, SwiftShader) 감지 시 최저 렌더 스케일과 타이틀 안내 문구, 시작 후 FPS를 재서 렌더 스케일 자동 조절
- [ ] [`web-build.md`](../build/web-build.md) 체크리스트 수행, 제출용 빌드
- [ ] 선택: 후순위 숨겨진 요소 (희귀 발신자부터)

## 6. 제출 — 11/16

- [ ] 제출처에 업로드하고 실제 페이지에서 플레이 확인

## 미정 사항

- 제출처와 용량 제한
- Web 템플릿: 기본 템플릿 사용 또는 전체 화면 커스텀 템플릿
