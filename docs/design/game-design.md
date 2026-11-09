# 게임 디자인

기준 구현은 [`../prototype.html`](../prototype.html)이다.

## 한 줄 요약

폰이 방 어딘가에서 울린다. 소리를 따라가 쌓인 물건을 치우고 20초 안에 폰을 찾아 받는다.

## 핵심 재미

- **소리가 유일한 단서다.** 3D 방향과 거리, 그리고 폰 위에 덮인 물건 수로 먹먹해지는 정도를 듣는다.
- 물건을 던져 치울수록 소리가 또렷해진다. 던진 물건이 다른 곳을 덮으면 판단이 흔들린다.
- 시간이 갈수록 벨이 커져서 압박이 올라간다.

## 상태 흐름

```
title ──시작──▶ playing ──폰 집기──▶ answer ──슬라이드 완료──▶ won
                  │                   │
                  └──── 시간 종료 ─────┴──────────────────▶ missed

won / missed ──다시 찾기 또는 R──▶ playing
```

`answer` 중에도 타이머는 계속 흐른다. R은 `title`을 제외한 모든 상태에서 즉시 재시작한다.

## 규칙

| 항목 | 내용 |
|---|---|
| 라운드 | 시작 시 8개 은신처 모두에 물건 더미를 쌓고, 그중 하나의 맨 아래에 폰을 둔다 |
| 흩뿌림 | 바닥 중앙 영역에 책/셔츠/상자 6개를 추가로 흩뿌린다 |
| 안정화 | 시작 전에 물리를 90스텝 돌려 더미를 가라앉힌다 |
| 폰 집기 | 도달 거리 안, 화면 중심 레이와 폰의 거리 0.17m 이내, 사이에 가림막 없음 |
| 물건 던지기 | 조준한 물건을 시선 수평 방향으로 던진다. 좌우로 ±0.28 비율의 무작위 편차 |
| 바구니 안 물건 | 벽에 막히지 않도록 전방 1.6 / 위 6.2로 띄운다 |
| 덮임 수 | 폰 기준 높이 -0.03~+1.1m, 물건 로컬 XZ 범위 + 0.05m 안에 드는 물건 수 |
| 진동 | 벨 한 주기의 절반마다 폰 반경 0.5m, 높이차 0.6m 안의 물건을 살짝 튀게 한다 |
| 폰 흔들림 | 덮인 물건이 0개일 때만 폰이 좌우로 진동한다 |
| 받기 | 옆으로 밀기 슬라이드를 끝까지 민다 |
| 실패 연출 | 카메라가 폰을 향해 자동으로 돌아가고 폰 화면이 빨갛게 바뀐다 |

타이머는 **실제 시간**으로 줄어든다. 게임 시간 스케일이나 프레임 드랍 보정의 영향을 받지 않는다.

## 튜닝 값 (`GameTuning`)

| 키 | 값 | 설명 |
|---|---|---|
| `duration` | 20 | 게임 길이(초) |
| `walkSpeed` | 3.2 | 걷는 속도(m/s) |
| `lookSpeed` | 0.0023 | 마우스 감도(rad/px). 터치는 ×1.9 |
| `reach` | 3.1 | 손이 닿는 거리(m) |
| `throwForward` | 4.6 | 던지는 수평 속도 |
| `throwUp` | 3.6 | 던지는 위쪽 속도 |
| `throwCooldown` | 0.1 | 던지기 간격(초) |
| `clearCutoff` | 7000 | 덮이지 않았을 때 고음 한계(Hz) |
| `coveredCutoff` | 420 | 많이 덮였을 때 고음 한계(Hz) |
| `maxCovers` | 4 | 이 개수부터 가장 먹먹함 |
| `rolloff` | 2.2 | 거리 감쇠 |
| `jiggle` | 0.55 | 진동 시 튀는 속도 |
| `slideNeeded` | 210 | 받기 슬라이드 거리(px) |

먹먹함 공식: `cutoff = clear × (covered / clear) ^ (min(1, covers / maxCovers) ^ 0.6)`. `answer` 상태에서는 `clearCutoff`로 고정한다.
벨 볼륨: `0.75 + 0.45 × (1 - 남은시간/전체시간)`.

## 조작

| 입력 | 동작 |
|---|---|
| WASD / 방향키 | 걷기 |
| 마우스 (포인터 잠금) | 둘러보기 |
| 클릭 / Space | 물건 던지기, 폰 집기 |
| 터치 끌기 | 둘러보기 |
| 터치 길게 누르기 (260ms) | 앞으로 걷기 |
| 터치 탭 | 던지기, 폰 집기 |
| 마우스 또는 터치 가로 이동 (`answer`) | 슬라이드 |
| R | 즉시 재시작 |

## 콘텐츠

### 발신자

| 이름 | 받았을 때 | 놓쳤을 때 |
|---|---|---|
| Mom | "Have you eaten yet?" | 1 new text: "Why aren't you picking up?" |
| Delivery Driver | "I'll leave it at your door." | "Missed delivery. Returning to sender." |
| Job Interview | "We'd like to offer you the position." | "We couldn't reach you. We've moved on to the next candidate." |
| Fried Chicken Place | "Your address didn't come through. Which building?" | Your chicken is getting cold. |
| Unknown Number | "Hello! We have a low-interest loan offer for you." | Probably nothing important anyway. |

### UI 문구

**게임 안의 모든 텍스트는 영어만 쓴다.** 프로토타입의 한국어 문구 대신 아래 문구를 기준으로 한다.

| 위치 | 문구 |
|---|---|
| 타이틀 | Missed Call |
| 타이틀 설명 | Your phone is ringing somewhere. Find it and pick up before it stops. |
| 조작 안내 | WASD: Walk / Mouse: Look around / Click: Throw items, grab phone / Touch: Drag to look, hold to walk, tap to throw |
| 시작 버튼 | Ring It |
| 타이틀 하단 | Headphones make the direction much easier to hear. |
| 플레이 힌트 | Follow the sound and clear the piles |
| 조준 라벨 | Throw / Grab phone |
| 받기 화면 | Incoming call / Pick up! / Slide to answer |
| 성공 | Hello? / Answered in {0.0}s |
| 실패 | 1 Missed Call / Your phone was {0.0}m away |
| 재시작 | Try Again / Press R to restart |

### 색

| 용도 | 색 |
|---|---|
| 배경 | `#10161C` |
| 패널 | `#10161C` 84% |
| 본문 | `#F3EEE3` |
| 보조 텍스트 | `#B5B3A8` |
| 강조, 타이머 | `#FFD23F` |
| 실패, 5초 이하 타이머 | `#FF6B57` |
| 성공, 폰 조준 | `#57D68D` |

폰트: 제목 Black Han Sans(라틴 글리프), 본문 IBM Plex Sans.
