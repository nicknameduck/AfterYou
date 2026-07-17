# 하네스 최종 결과 — Phase 2b 레벨=프리팹 교체 + 동적 캐릭터 생성

- 일자: 2026-07-18
- 판정: **PASS (99/100)**
- 워크트리: OFF (씬/프리팹 수정 포함 → 규정상 반드시 OFF)

## 스펙

Phase 2b — 레벨을 **씬이 아니라 프리팹**으로 만들고 교체한다. 씬의 `LevelManager`가 레벨 정의를 읽어 캐릭터를 인원수만큼 동적 생성하고 정체성을 주입하며, `RoundManager`는 씬 직렬화 대신 **런타임 주입**(`Initialize`/`Teardown`)으로 구동된다.

- 레벨 = 프리팹. 씬 전환 없음. `LevelManager`가 프리팹 인스턴스를 교체한다.
- 레벨이 캐릭터를 생성한다(2~4 가변). 프리팹이 `SpawnPoint`를 자식으로 포함.
- `RoundManager._characters`/`_spawnPoint`는 `[SerializeField]` 제거 → 런타임 주입.
- `Initialize`(주입→상태 리셋→구동) / `Teardown`(안전 정리) + `_initialized` 가드로 레벨 전환 중 NRE 차단.
- `Cleared` 상태에서 **N키**로 다음 레벨 순환((idx+1)%len).
- UI 슬롯 버튼은 **3개 유지 + 초과 숨김**(4번째 추가 금지).
- 검증 레벨 3종: 1_1 기본협력[Heavy,Light] / 1_2 클론밟기[Light,Light] / 1_3 ⭐순서강제[Heavy,Light,Light].
- 스코프 외(카메라 추적·세이브·운반형 등) 구현 금지.

## 동작 조건

| # | 조건 | 결과 | 근거 |
|---|---|---|---|
| 1 | 보호 블록(ResolveCrush/Push/중앙 틱/IgnoreCollision/RestoreAllSpawnOverlaps) 불변 | ✓ | `git diff` — 가드 라인 추가·`Awake→Initialize`·`Start→Teardown`·필드 선언 변경 외 본문 diff 0 |
| 2 | Initialize 순서: 주입→3컬렉션 Clear+Restore→틱/인덱스 리셋→originalParents→Override→_initialized=true→EnterSelecting | ✓ | `RoundManager.cs:107-131` diff 순서 일치 |
| 3 | Teardown: `_liveIndex=-1`이 `_characters=null`보다 먼저 + 3컬렉션 Clear | ✓ | `RoundManager.cs:140-152` — liveIndex 리셋 후 마지막 줄에서 `_characters=null` |
| 4 | 가드 완전성 (Update/FixedUpdate/Select/Confirm/Restart/Rewind/PostPhysics) | ✓ | 7개 진입점 전부 `if(!_initialized) return`. OnLevelCleared는 `_state`만 만지고 자체 Cleared 가드 有 → 불필요(정당) |
| 5 | InjectIdentity 필드 대입만 + CharacterActor.Awake 불변 | ✓ | `CharacterActor.cs:52-58` `_identity=identity` 단일 대입, Awake diff 0 |
| 6 | 비활성 부모 트릭: SetActive(false)→Instantiate→Inject→SetActive(true)→Bind→Initialize | ✓ | `LevelManager.cs:71-90` — Initialize가 SetActive(true) **후** |
| 7 | N키: Keyboard.current null 가드 + Cleared 상태에서만 + 신규 Input System | ✓ | `LevelManager.cs:95-100` |
| 8 | 캐릭터 파괴 리스트 기반(부모 무관) + 이전 레벨 인스턴스 Destroy | ✓ | `LevelManager.cs:50-58` `_spawnedActors` 순회 + `_currentLevel` Destroy |
| 9 | Play → Level_1_1 자동 로드, 캐릭터 정확히 2기, 에러 0, SlotCount=2, 3번째 버튼 비활성 | ✓ | 실측: actors=2, SlotCount=2, btn[2] activeSelf=False, 콘솔 에러 0 |
| 10 | 주입 실측: Heavy jF=10/mask=256, Light jF=14/mask=768, mass 전원 1 | ✓ | 실측: Heavy `jumpForce=10 _groundMask=256`, Light `14/768`, `mass=1` |
| 11 | Level_1_1 클리어 인과: Heavy 판→door.IsOpen. 오답 반증: Light 단독 무게<10 | ✓ | 실측 Heavy `weight=10 IsPressed=True IsOpen=True` / Light `weight=1 False/False` |
| 12 | 레벨 전환: 이전 캐릭터/레벨 잔존 0(다음 프레임), SlotCount 갱신 | ✓ | 로드 직후 4/2 → 다음 프레임 **2/1**. `[Light,Light]` SlotCount=2 |
| 13 | Level_1_2 렛지 1.2 게이트 양방향 (지면 apex<1.2 / 클론 위>1.2) | ✓ | Script 스테핑 실측: 지면 max feet **0.706** < 1.20 / 클론 위 **1.726** > 1.20 |
| 14 | Level_1_3 순서 강제 (a)Light 선행 문 통과 불가 (b)Heavy Exit 도달 불가 | ✓ | (a) 판 Light `weight=1 IsOpen=False`/Heavy `10 True`. (b) Heavy 지면 **-0.886**·클론 위 **0.134** 둘 다 <1.20 |
| 15 | 순환: 1_3 → 다음 → 1_1 클린 재로드 | ✓ | `(2+1)%3=0` → Level_1_1, 다음 프레임 actors=2/levelDef=1, `[Heavy,Light]` |
| 16 | 컴파일 에러·워닝 0 + Cleared 시 UI "N" 안내 | ✓ | `read_console` 세션 전후 0건. statusText=`"클리어! N키로 다음 레벨…"` |

## 참조 패턴

**[데이터주도설계] #2 Phase 2a 정체성+압력판 + [코어시스템] #1 Phase 1 녹화·재생 코어**

- #2의 "능력을 데이터로 분리 / ScriptableObject 주입은 필드 대입만" 원칙을 **레벨 데이터**(`LevelDefinition`) 축으로 확장했다. `InjectIdentity`가 필드 대입만 하고 CharacterActor.Awake가 그 값을 읽는 #2의 주입 규약을 그대로 계승.
- #2의 "Script 시뮬레이션 스테핑으로 점프 정점 실측 + 실패 케이스가 올바른 이유로 실패하는지 계측" 검증 기법을 재사용해 렛지 게이트(1_2)와 순서 강제(1_3)를 양방향 실측했다.
- #1의 `SetMode` 순서 규약·참조 동일성 판정(`LiveCharacter`)이 훼손 없이 유지됐고, Teardown의 `_liveIndex=-1` 선행이 이 참조 판정의 NRE를 정확히 막는다.
- **신규 축**: RoundManager를 씬 직렬화에서 떼어내 매니저 주입형 생명주기(`Initialize`/`Teardown`/`_initialized`)로 리팩터. 보호 블록을 가드 라인만으로 감싸 본문 무변경.

## 점수

### 총점: 99/100

| 섹션 | 배점 | 획득 | 근거 |
|---|---|---|---|
| 코딩 컨벤션 | 18 | 17 | `_initialized` bool이 is/has/can 접두사 미준수(→`_isInitialized`). 그 외 준수 |
| Unity 생명주기 | 13 | 13 | Awake/Start→Initialize/Teardown 리팩터 깔끔, 코루틴 가드로 안전 정지 |
| 성능 | 13 | 13 | Update 핫 경로 GetComponent·Find 없음. Instantiate/Destroy는 레벨 전환(비핫경로)만 |
| Unity API 최신화 | 감점 | 0 | Obsolete 사용 0. 신규 Input System |
| 안전성 | 13 | 13 | `_initialized` 가드·`_liveIndex` 선행·null 가드로 전환 중 NRE 전부 차단 |
| 기능 충족 | 23 | 23 | 16개 조건 전부 MCP 실측 충족 |
| 회귀 방지 | 감점 | 0 | 보호 블록 불변, 컴파일 0, 참조부(BindRoundManager/InjectIdentity) 정합 |
| 파괴적 변경 | 감점 | 0 | 코드 파일 순증. SampleScene -1030은 **의도적 프리팹 이관**(스펙 명시) |
| MCP 런타임 검증 | 감점 | 0 | 실측 수행, 배치/미연결/에러 문제 없음 |
| 단순성 | 10 | 10 | LevelManager 103줄 최소 구현, 과도 추상화 없음 |
| 완결성 | 10 | 10 | 신규 3파일 + 프리팹 3종 + meta 전부 존재, TODO 0 |

## 피드백

Critical / Major: **없음.**

### Minor

1. **[Minor] `_initialized` bool 네이밍이 컨벤션 미준수** — `RoundManager.cs:69`
   프로젝트 컨벤션은 bool에 `is/has/can` 접두사를 요구한다(`_isAlive`, `_hasShield`). `_initialized`는 과거분사 관용구지만 규칙상 `_isInitialized`가 맞다. 동작 무해, 순수 컨벤션.

2. **[Minor] CharacterSelectUI가 매 프레임 버튼 SetActive 무조건 호출** — `CharacterSelectUI.cs:45`
   `_slotButtons[i].gameObject.SetActive(isInRange)`를 값 변화와 무관하게 매 프레임 호출한다. Unity SetActive는 값이 같으면 사실상 no-op이고 버튼이 3개뿐이라 실측 부담은 무시할 수준. 기존 즉시모드 UI 패턴의 연장이라 감점 대상 아님(참고).

### 특기 사항 (감점 아님)

- **레벨 게이트 수치가 견고**: 1_2 지면 정점 발바닥 0.706 vs 렛지 1.20 (여유 0.49 미달), 클론 위 1.726 (여유 0.53 초과) — 양방향 모두 0.5 마진. 1_3은 Heavy가 **클론 위에서도** 0.134로 렛지에 0.5 이상 못 미쳐 "Heavy는 절대 Exit 불가 → 반드시 판 담당"이 물리적으로 강제된다. 순서 강제 설계가 수치상 확실.
- **레벨 전환 잔존 검증을 "다음 프레임"으로 정확히 수행**: Destroy 지연 특성을 이해하고 로드 직후(4/2)와 다음 프레임(2/1)을 구분 측정.
- **OnLevelCleared 가드 미부착이 옳은 판단**: `_state`만 접근 + 자체 `Cleared` 가드가 있어 Teardown 후 호출돼도 무해. 불필요한 가드를 넣지 않은 절제.
- **스코프 준수**: 카메라 추적·세이브·운반형/벽타기형 관련 코드 0건.

## 수정 파일

### 신규
- `Assets/Scripts/Level/LevelDefinition.cs` (29줄) — 레벨 데이터 홀더(로직 없음)
- `Assets/Scripts/Managers/LevelManager.cs` (103줄) — 로드/언로드·캐릭터 동적 생성·정체성 주입·N키 순환
- `Assets/Prefabs/Level/Level_1_1.prefab` [Heavy,Light] — 판/문/Exit
- `Assets/Prefabs/Level/Level_1_2.prefab` [Light,Light] — 렛지 1.2 게이트
- `Assets/Prefabs/Level/Level_1_3.prefab` [Heavy,Light,Light] — 판+문 벽 + 렛지, ⭐순서강제
- 대응 `.meta` 전부 존재

### 수정
- `Assets/Scripts/Managers/RoundManager.cs` (504→550) — Initialize/Teardown/`_initialized` 가드. `_characters`/`_spawnPoint` 런타임 주입화([SerializeField] 제거)
- `Assets/Scripts/Clone/CharacterActor.cs` (190→199) — `InjectIdentity` 추가(필드 대입만)
- `Assets/Scripts/UI/CharacterSelectUI.cs` (50→64) — SlotCount 기반 버튼 가변 표시 + Cleared "N" 안내
- `Assets/Scripts/Level/LevelExit.cs` (33→42) — `BindRoundManager` 런타임 주입
- `Assets/Scenes/SampleScene.unity` (-1030) — 레벨 콘텐츠를 프리팹으로 이관, LevelManager 배선
