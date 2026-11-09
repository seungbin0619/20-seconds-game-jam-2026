# 로드맵

작업을 끝낼 때마다 체크한다. 순서는 **가장 불확실한 것부터** 검증하도록 잡았다.

## 0. 사전 정리

- [x] `Assets/_Project/{Core,Shared,Feature}` 구조와 asmdef
- [x] 템플릿 에셋 정리, 씬을 `Core/Scenes/Main.unity`로 이동
- [x] 컨텍스트 문서 작성 (`CLAUDE.md`, `docs/`)
- [x] Web 플레이어 설정 (압축, 백그라운드 실행, 제품명)
- [x] Unity Web Build Support 모듈 설치
- [x] 활성 빌드 타깃을 Web으로 전환
- [x] 빈 씬 Web 빌드가 브라우저에서 뜨는지 확인 (약 16MB, WebGL 2)

## 1. 기술 검증 (Web 빌드에서 확인)

- [ ] `Ringtone`: jslib Web Audio로 벨 재생, HRTF 방향감, 저역 통과 필터 변화
- [ ] 터치와 포인터 잠금 동작
- [ ] 영문 TMP 폰트 표시와 빌드 크기

## 2. 코어 루프

- [ ] `Core`: `GameFlow`, `RoundContext`, `GameTuning`
- [ ] `Room`: 방, 가구, 조명, 은신처 8곳
- [ ] `Player`: 걷기, 둘러보기, AABB 충돌
- [ ] `Clutter`: 물건 종류, 더미 생성, 물리 안정화
- [ ] `Interaction`: 조준, 던지기
- [ ] `Phone`: 배치, 덮임 계산, 진동, 집기
- [ ] 벨소리를 덮임 수와 플레이어 위치에 연결

## 3. UI와 라운드 완성

- [ ] 타이틀, HUD, 조준점과 라벨
- [ ] 받기 슬라이드
- [ ] 결과 화면과 발신자 문구
- [ ] R 재시작, 실패 연출 (카메라가 폰을 바라봄)

## 4. 폴리시

- [ ] 라이팅과 머티리얼, 프로토타입 분위기 맞추기
- [ ] 효과음
- [ ] 수치 튜닝

## 5. 제출

- [ ] [`web-build.md`](../build/web-build.md) 체크리스트 수행

## 미정 사항

- 제출처와 용량 제한, 마감 일시
- Web 템플릿: 기본 템플릿 사용 또는 전체 화면 커스텀 템플릿
