# Web 빌드

## 환경

- Unity 6000.3.25f1, URP, Linear, WebGL 2
- Web Build Support 모듈이 필요하다.

```bash
unity install-modules -e 6000.3.25f1 -m webgl
```

설치 후 에디터를 다시 열고 `File > Build Profiles`에서 Web으로 전환한다.

## 적용한 설정

| 설정 | 값 | 이유 |
|---|---|---|
| Compression Format | Gzip | 서버 설정이 없는 호스팅에서도 동작 범위가 넓다 |
| Decompression Fallback | 켬 | 호스팅이 `Content-Encoding` 헤더를 주지 않아도 로드된다 |
| Data Caching | 켬 | 재방문 로딩 단축 |
| Run In Background | 켬 | 탭을 벗어나도 벨과 타이머가 실제 시간으로 흐른다 |
| Active Input Handling | Input System | 프로젝트 기본값 |
| Quality (Web) | Mobile (`Mobile_RPAsset`) | 프로젝트 기본값. Render Scale 0.8 |
| Graphics > Lightmap Modes | Manual, Baked Non-Directional만 | 라이트맵을 런타임에 복원하므로 빌드 씬에 구운 데이터가 없어도 변형을 남긴다 |

`Build Profiles`와 Player Settings의 변경은 에디터 안에서 한다.

## 빌드

1. 씬 목록에 `Assets/_Project/Core/Scenes/Main.unity`만 있는지 확인한다.
2. 출력 폴더는 `Builds/Web`으로 한다 (git 제외).
3. 제출본은 Development Build를 끈다.

## 로컬 실행

`file://`로는 열 수 없다. 로컬 서버로 연다.

```bash
npx http-server Builds/Web -p 8080 -c-1
```

## 제출 체크리스트

- [ ] 콘솔 에러 없음 (브라우저 개발자 도구)
- [ ] Chrome, Edge, Firefox에서 플레이 가능
- [ ] 첫 클릭 후 벨소리가 들림
- [ ] 이어폰으로 좌우 방향과 먹먹함 변화가 구분됨
- [ ] 포인터 잠금, Esc 후 재잠금
- [ ] 모든 글자가 표시됨 (네모 글자 없음), 한국어 문구가 남아 있지 않음
- [ ] 라운드를 여러 번 반복해도 폰이 보이고 메모리가 안정적임
- [ ] 제출처 용량 제한 이내
- [ ] `index.html`이 압축 파일 최상위에 있음
- [ ] Development Build가 꺼져 있음

## 알려진 로그

- `Hidden/CoreSRP/CoreCopy`, `StencilDitherMaskSeed`, `HDRDebugView` 셰이더의 "not supported on this GPU" 로그는 빈 씬에서도 나온다. 화면이 정상이면 무시한다.
- 빌드하면 테스트 패키지가 빈 `Assets/Resources`를 만든다. git에서 제외해 두었다.

## 크기 줄이기 (필요할 때만)

- 폰트 아틀라스 글자 수와 해상도를 줄인다.
- Managed Stripping Level을 올리고 IL2CPP Code Generation을 Optimize Size로 둔다. 올린 뒤에는 Web 빌드에서 동작을 다시 확인한다.
- 쓰지 않는 패키지를 제거한다 (`com.unity.visualscripting`, `com.unity.multiplayer.center` 등). `com.unity.pipeline`은 CLI 연동에 필요하므로 유지한다.
