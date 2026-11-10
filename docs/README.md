# 문서 색인

| 경로 | 내용 | 언제 읽나 |
|---|---|---|
| `prototype.html` | 원본 프로토타입. 동작의 기준 | 동작이 애매할 때 해당 함수만 참고 |
| `design/game-design.md` | 게임 규칙, 상태 흐름, 튜닝 값, 콘텐츠 | 게임플레이를 만들거나 수치를 바꿀 때 |
| `architecture/project-structure.md` | 폴더 구조, 의존 규칙, 네이밍, Feature 목록 | 파일을 새로 만들 때 |
| `architecture/level-generation.md` | 집 생성(모듈 조립), 방 그래프, 조명 굽기 전략 | 집, 방, 조명을 만들 때 |
| `architecture/porting-notes.md` | three.js/cannon.js → Unity 변환과 Web 제약 | 프로토타입 코드를 옮길 때 |
| `plan/roadmap.md` | 단계별 작업과 진행 상태 | 작업을 시작하고 끝낼 때 |
| `build/web-build.md` | Web 빌드 설정, 빌드 절차, 제출 체크리스트 | 빌드하거나 제출할 때 |

## 문서 관리 규칙

- 한 사실은 한 문서에만 적고 나머지는 링크한다.
- 코드를 보면 알 수 있는 내용은 적지 않는다. 결정과 그 이유, 지켜야 할 제약을 적는다.
- 구현이 문서와 달라지면 같은 변경에서 문서도 고친다.
- `plan/roadmap.md`의 체크박스는 작업이 끝날 때마다 갱신한다.
