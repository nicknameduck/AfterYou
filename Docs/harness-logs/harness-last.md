# 하네스 최종 결과 — Phase 2a 정체성 + 압력판

- 일자: 2026-07-17
- 판정: **PASS (97/100)**
- 워크트리: OFF (씬 수정 포함 → 규정상 반드시 OFF)

## 스펙

Phase 2a — "무거운형/가벼운형" 정체성을 도입해 **각자 못 하는 일**을 만들고, 압력판(홀드 스위치)으로 클론 간 의존 사슬을 만든다.

- 정체성은 **ScriptableObject**(`IdentityData`)로 정의한다. enum 하드코딩 금지.
- "높이 못 간다"는 특성은 **오직 `jumpForce`로만** 표현한다. `Rigidbody2D.mass`는 건드리지 않는다.
- "남의 등을 밟을 수 있는가"는 **접지 LayerMask**로 표현한다. 가벼운형 = Ground|Clone(768) / 무거운형 = Ground(256).
- `Clone` 레이어(9) 신설. 클론 = Clone(9), 라이브/대기 = Default(0).
- 압력판 = **홀드 스위치**. 라이브든 클론이든 Weight 합 ≥ 요구 무게면 눌리고, 떼면 즉시 닫힌다.
- 크기 차등 없음(색상만). `RoundManager._characters` 구조 유지, RoundManager **미수정**.
- 운반형/벽타기형/물체밀기는 Phase 3 (이번 스코프 아님).

## 동작 조건

| # | 조건 | 결과 | 근거 |
|---|---|---|---|
| 1 | Identity 에셋 2종 존재, 3기에 연결 (None 아님) | ✓ | MCP: Character_1=Identity_Heavy / Character_2·3=Identity_Light |
| 2 | 캐릭터 색상이 정체성별로 다름 | ✓ | 런타임 color: Heavy `RGBA(0.12,0.16,0.45)` / Light `RGBA(0.45,0.80,1.00)` |
| 3 | "Clone" 레이어(9) 신설 | ✓ | `LayerMask.NameToLayer("Clone")=9`, TagManager.asset diff |
| 4 | Clone 모드 layer=9 / Live·Idle=Default(0) | ✓ | MCP: Clone→`layer=9`, Live→`layer=0`, Idle→`layer=0` |
| 5 | 가벼운형 마스크=768 / 무거운형=256 | ✓ | 런타임 `pc.groundLayer`: Light=768, Heavy=256 |
| 6 | (A) 가벼운형 단독 점프로 플랫폼에 **못 닿음** | ✓ | 실측 정점 발바닥 **0.71** < Platform top **1.30** (여유 0.59) |
| 7 | (B) 가벼운형이 클론 위에서 점프하면 **닿음** | ✓ | 실측 정점 발바닥 **1.70** ≥ **1.30** (여유 0.40) |
| 8 | (C) 무거운형은 클론 위에서 **점프 불가** | ✓ | `_isGrounded=False`, OverlapBox=NULL. 단 y=-0.985·v=0으로 **물리적으로는 서 있음** (확정 사양) |
| 9 | (D) 무거운형이 압력판 → 문 열림 | ✓ | `IsPressed=True / IsOpen=True / doorCollider.enabled=False` |
| 10 | 가벼운형은 안 열림 (무게 1 < 10) | ✓ | Light×2 = `MeasureWeightOnPlate()=2` < 10 → `IsOpen=False` |
| 11 | **재생 중인 클론이 밟아도 문 열림** (라이브 전용 아님) | ✓ | Heavy를 **Clone 모드**로 판 위 배치 → `IsOpen=True` |
| 12 | 압력판에서 떼면 문 **즉시 닫힘** (홀드) | ✓ | 클론 이탈 → `IsPressed=False / IsOpen=False / collider.enabled=True` |
| 13 | 문 닫힘 시 통행 불가, Exit는 문 너머, **3기 스폰 모두 문 앞쪽** | ✓ | Door x[1.30,1.70] / Exit x[5.50,6.50] / 스폰 x = -8, -3, -1 (전부 < 1.30) |
| 14 | 클론 등에서 점프해 **문을 뛰어넘을 수 없음** | ✓ | Door top **3.50** vs 클론 위 정점 발바닥 **1.70**. 2단 클론 스택(발 -0.5)도 정점 2.83 < 3.50 |
| 15 | Phase 1 회귀 없음 (2라운드/재생/압사해소/라이브만 클리어) | ✓ | RoundManager.cs **미수정 확인** (429줄, git status 클린) |
| 16 | 컴파일 에러 0 | ✓ | `read_console` 에러·워닝 **0건** (플레이 세션 전후) |

## 참조 패턴

**[코어시스템] #1 Phase 1 녹화·재생 코어 — 레이어 전환 기법 + 점프 도달고 수치 검증 재사용**

- #1의 "레이어 전환으로 밟을 수 있는 오브젝트 만들기 (+ 라이브는 원래 레이어로 되돌려 무한 점프 역함정 회피)"를 계승하되, 클론을 Ground(8)가 아닌 **전용 Clone(9)** 로 분리해 정체성별 차등을 가능하게 확장했다.
- #1의 "점프 도달고 = v₀²/(2·g·gravityScale) 수치 검증으로 레벨 설계 판정" 기법을 재사용했고, 이번에는 `Physics2D.simulationMode = Script` + `Physics2D.Simulate` 수동 스테핑으로 **실측 정점까지 확보**해 공식과 교차 검증했다.
- #1의 `SetMode` 순서 규약(①활성화 →②속도0 →③bodyType →④layer →⑤컴포넌트 →⑥비활성화)이 훼손 없이 유지됐다.

## 점수

### 총점: 97/100

| 섹션 | 배점 | 획득 |
|---|---|---|
| 코딩 컨벤션 | 18 | 18 |
| Unity 생명주기 | 13 | 13 |
| 성능 | 13 | 12 |
| Unity API 최신화 | 감점 | 0 |
| 안전성 | 13 | 12 |
| 기능 충족 | 23 | 23 |
| 회귀 방지 | 감점 | 0 |
| 파괴적 변경 | 감점 | 0 |
| MCP 런타임 검증 | 감점 | 0 |
| 단순성 | 10 | 9 |
| 완결성 | 10 | 10 |

### 치명 위험 해결 검증 (Critic 지목 9건)

| 위험 | 결과 | 근거 |
|---|---|---|
| C-3 무게/mass 분리 | ✓ | `.mass =` 대입 **코드베이스 전역 0건**. 런타임 3기 모두 `rb.mass=1`. `IdentityData.cs:47`, `PlayerController.cs:116-117` 주석으로 근거 명시 |
| C-1 Awake | ✓ | `CharacterActor.cs:52-88` Awake에 위치. Idle(SetActive(false)) 상태에서도 `pc.jF=10/14` 주입 완료 확인 → Start였다면 불가능했을 증거 |
| C-2 필드 대입만 | ✓ | `PlayerController.cs:119-126` `_moveSpeed/_jumpForce/_groundLayer` 3개 대입만. `_rigidbody/_moveAction/_jumpAction/_inputActions` 미접근. `CharacterActor.Awake`에 `enabled`/`SetMode()` 호출 없음 |
| A-1 마스크 768 | ✓ | 런타임 Light `pc.groundLayer=768`. 클론 위 `_isGrounded=True`, overlap=`Character_2(layer 9)` |
| D-1 마스크 513 + 2차 필터 | ✓ | `_detectionMask=513` 실측. `useTriggers=false`(`PressurePlate.cs:69-72`)로 Exit 트리거 배제 + `GetComponentInParent<CharacterActor>()`(`:113`) 2차 필터. `MeasureWeightOnPlate()=2`로 라이브(0)+클론(9) **동시 검출** 증명 |
| G 기하 판정만 | ✓ | `PressurePlate.cs:121` `other.bounds.min.y < plateTop - _standTolerance` 기하 조건. `linearVelocity` 참조 **0건**. Kinematic 클론이 실제로 판을 눌러 문을 연 것이 결정적 증거 |
| H 레이어 가드 | △ | `CharacterActor.cs:68-69`에 `_cloneLayer < 0` 검사 + LogError 존재. 단 **보고만 하고 대입을 막지는 않음** → Minor 1 참조 |
| SetMode 순서 유지 | ✓ | `CharacterActor.cs:93-140` ①활성화(:100) →②속도0(:106) →③bodyType(:110) →④layer(:121) →⑤컴포넌트(:124) →⑥비활성화(:138) 순서 그대로 |
| 성능(non-alloc) | ✓ | `PressurePlate.cs:102` `Physics2D.OverlapBox(..., _contactFilter, _overlapBuffer)` — `readonly List` 재사용. `OverlapBoxAll` 미사용 |

## 피드백

Critical / Major: **없음.**

### Minor

1. **[Minor] Clone 레이어 가드가 보고만 하고 예외를 막지 않는다** — `CharacterActor.cs:68-69`
   `_cloneLayer < 0`일 때 `Debug.LogError`만 하고 그대로 진행한다. 이후 `SetMode(Clone)`의 `gameObject.layer = _cloneLayer`(`:121`)가 `-1`을 대입해 예외가 난다. 현재 Clone=9가 실재하므로 **실질 무해**하고, "설정 누락은 조용히 감추기보다 크게 터뜨린다"는 판단도 방어 가능하다. 다만 가드의 목적이 예외 회피라면 `_cloneLayer = _defaultLayer` 폴백 또는 조기 `return`이 필요하다.

2. **[Minor] FixedUpdate마다 `GetComponentInParent` 호출** — `PressurePlate.cs:113`
   겹친 콜라이더마다 매 물리 틱 부모 체인을 거슬러 올라간다. 겹침 수가 0~3개라 실측 부담은 무시할 수준이고 2차 필터로 필수 불가결하지만, 엄밀히는 핫 경로의 컴포넌트 조회다. 레벨당 압력판이 늘어나면 `Collider2D → CharacterActor` 캐시를 검토할 여지가 있다.

3. **[Minor] 미사용 public 멤버 `IdentityData.DisplayName`** — `IdentityData.cs:19, 42`
   `_displayName` / `DisplayName`이 코드베이스 어디에서도 소비되지 않는다(에셋에는 "무거운형"/"가벼운형" 값이 들어 있음). Phase 3 UI에서 쓸 여지는 있으나 현 스펙에는 없는 필드다.

### 특기 사항 (감점 아님)

- **받아들인 설계 귀결 2건 모두 문서화 확인**:
  - "클론은 닫힌 문을 통과한다" → `Door.cs:9-14`에 원인(Kinematic MovePosition)·회피책(문 너머엔 Exit만)·금지사항(문 너머 장치 배치 금지)까지 명시. 요구 수준 충족.
  - "무거운형은 클론 위에 물리적으로 올라설 수는 있으나 점프만 불가" → 실측으로 정확히 재현(y=-0.985 정지, `_isGrounded=False`). 확정 사양대로.
- **스코프 준수**: 운반/벽타기/물체밀기 관련 코드 **0건** (grep 확인. `RoundManager.Push`는 Phase 1 압사 해소 로직으로 무관).
- **레벨 설계 여유**: Light jF=14 → 도달고 3.33 (상한 14.96 대비 안전). 지면 정점 0.71 vs 필요 1.30, 클론 위 1.70 vs 1.30 — **양방향 모두 실측 마진 확보**. 코어 게이트가 수치상 견고하다.
- **파괴적 변경 없음**: PlayerController 111→137(+26/-0 순수 추가), CharacterActor 150→182(+38/-6). 줄 수 감소 파일 0건. Critic의 "Edit 전용" 지시 준수.

## 수정 파일

### 신규
- `Assets/Scripts/Core/IdentityData.cs` (53줄)
- `Assets/Scripts/Level/PressurePlate.cs` (150줄)
- `Assets/Scripts/Level/Door.cs` (62줄)
- `Assets/Data/Identities/Identity_Heavy.asset` (jF=10 / weight=10 / mask=256 / moveSpeed=5)
- `Assets/Data/Identities/Identity_Light.asset` (jF=14 / weight=1 / mask=768 / moveSpeed=7)
- 대응 `.meta` 전부 존재 (`Assets/Data.meta` 포함)

### 수정
- `Assets/Scripts/Player/PlayerController.cs` (+26/-0) — `ApplyIdentity` 추가
- `Assets/Scripts/Clone/CharacterActor.cs` (+38/-6) — `_identity` 필드, Awake 주입, Clone 레이어 전환
- `Assets/Scenes/SampleScene.unity` (+340) — Door / PressurePlate / Platform 배치, 3기 정체성 연결
- `ProjectSettings/TagManager.asset` — 레이어 슬롯 9에 `Clone` 추가
- `ProjectSettings/ProjectSettings.asset` — `runInBackground: 0 → 1` (MCP 실측 전제)

### 미수정 (확인 완료)
- `Assets/Scripts/Managers/RoundManager.cs` — 429줄 유지, git status 클린. `_characters` 구조 그대로.
