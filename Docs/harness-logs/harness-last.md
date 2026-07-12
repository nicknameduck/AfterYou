# 하네스 최종 결과 — Phase 1 녹화·재생 코어

- 일자: 2026-07-12
- 판정: **PASS (91/100)**
- 워크트리: OFF (본체 직접 수정)

## 스펙

Phase 1 — 타임 클론 녹화·재생 코어.

- 캐릭터 3기 중 하나를 선택 → 조작하며 궤적 녹화 → Enter(또는 15초 상한)로 확정 → 클론으로 승격
- 확정된 클론은 다음 라운드부터 **중앙 틱(RoundManager)** 에 맞춰 궤적을 동시 재생
- 라이브 캐릭터만 출구 도달 시 클리어 (클론 접촉 무시)
- 재녹화(R) = 현재 테이크만 폐기 / 되감기(Backspace) = 확정 스택 최상단 1개 pop
- 아키텍처 규약: 클론별 자체 재생 인덱스 금지. RoundManager.FixedUpdate가 유일한 틱 소유자
- 정체성(무거운/가벼운/운반/벽타기)·carry는 Phase 2 이후 — 이번 범위 밖 (스코프 준수 확인됨)

## 동작 조건

| # | 조건 | 결과 | 근거 |
|---|---|---|---|
| 1 | RoundManager가 `=== MANAGERS ===` 하위, `_characters`(3)·`_clonesParent` 참조 연결 | ✓ | MCP: `_characters.arraySize=3` (Character_1/2/3 전부 할당), `_clonesParent = === CLONES ===` |
| 2 | 캐릭터 3기가 이격 스폰, 미선택 시 Idle 비활성 | ✓ | MCP: (-4,-2) / (0,-2) / (4,-2) → 4유닛 간격. 플레이 진입 직후 3기 모두 `activeSelf=False, mode=Idle` |
| 3 | 선택 시 라이브화 (PlayerController enabled / Dynamic / layer 0) | ✓ | MCP 런타임: `C0 active=True layer=0 body=Dynamic pc=True mode=Live` |
| 4 | 확정 시 클론화 (PC disabled / Kinematic / layer 8 / Playback enabled) | ✓ | MCP 런타임: `C0 layer=8 body=Kinematic playback=True parent==== CLONES === mode=Clone` |
| 5 | 2라운드 이후에도 조작 가능 (치명 1) | ✓ | 3라운드 진입 시점 실측: `pcEnabled=True move.enabled=True jump.enabled=True`, 3기 모두 서로 다른 `InputSystem_Actions(Clone)` 인스턴스 |
| 6 | 라이브가 클론 위에서 점프 가능 (치명 2) | ✓ | 실측: 클론 위 라이브 `_isGrounded=True`, `OverlapBox → Character_2 (layer 8)`. 점프 도달고: v₀=14, g=29.43 → 상승 3.33. 클론 위(중심 -0.97) → 정점 하단 **1.86 > Platform top 1.30** 도달 ✓ / 지면(중심 -2.0) → 정점 하단 **0.83 < 1.30** 도달 불가 ✓ (클론 필수 레벨 설계) |
| 7 | 라운드 리셋 → tick=0, 확정 클론 frames[0] 복귀 | ✓ | RoundManager.cs:233-252 `EnterSelecting()` → `ResetToStart()` + `_tick = 0`. 실측 확정 직후 `tick=0 state=Selecting` |
| 8 | 재생 종료 클론은 마지막 프레임에서 정지 | ✓ | CloneRecording.cs:41 `Mathf.Clamp(tick, 0, Count-1)` + ClonePlayback.cs:53 클램프 홀드 |
| 9 | 15초 자동 확정 | ✓ | RoundManager.cs:63 `MaxTicks = CeilToInt(15 / 0.02) = 751`. 실측: 방치 시 `frames=751`로 자동 확정, state=Selecting 복귀 |
| 10 | 재녹화(R): 현재 테이크만 폐기, 확정 스택 유지 | ✓ | RoundManager.cs:181-192 `RestartTake()` — `_confirmedSlots` 미변경 후 `SelectCharacter(index)` 재진입 |
| 11 | 되감기(Backspace): 스택 최상단 1개만 pop | ✓ | RoundManager.cs:206-218 말단 `RemoveAt` 1회 + 원부모 복구 |
| 12 | LevelExit는 라이브만 클리어 (참조 동일성) | ✓ | LevelExit.cs:28 `actor != _roundManager.LiveCharacter` 참조 비교. 실측: 클론을 Exit에 넣어도 `state=Recording` 유지 / 라이브를 넣으면 `state=Cleared` |
| 13 | Unity 콘솔 컴파일 에러 0 | ✓ | `read_console` → 에러/워닝 0건. 플레이 전 구간(3라운드·자동확정·되감기) 러닝 후에도 `[RoundManager] LEVEL CLEARED` 로그 1건만 |

## 참조 패턴

없음 (신규 프로젝트)

## 점수

**총점: 91 / 100 — PASS**

| 섹션 | 배점 | 획득 |
|---|---|---|
| 코딩 컨벤션 | 18 | 17 |
| Unity 생명주기 | 13 | 11 |
| 성능 | 13 | 11 |
| Unity API 최신화 | 감점 | 0 |
| 안전성 | 13 | 11 |
| 기능 충족 | 23 | 23 |
| 회귀 방지 | 감점 | 0 |
| 파괴적 변경 | 감점 | 0 |
| MCP 런타임 검증 | 감점 | 0 |
| 단순성 | 10 | 8 |
| 완결성 | 10 | 10 |

### 치명적 결함 해결 검증
- **치명 1 (공유 InputActionAsset)**: ✓ 해결 — `PlayerController.cs:47` `_inputActions = Instantiate(_inputActions);` 가 `FindActionMap`(49행) **이전**에 위치. 런타임 실측에서 3기가 각각 다른 인스턴스 ID의 `(Clone)` 에셋 보유, 3라운드에서도 액션 enabled.
- **치명 2 (접지 마스크)**: ✓ 해결 — `CharacterActor.cs:83` `gameObject.layer = mode == Clone ? Ground(8) : Default(0)`. 역함정(라이브 자기 콜라이더 자가 검출)도 회피 — 라이브 layer 0 공중 실측 시 OverlapBox NULL.
- **틱 오프셋 규약**: ✓ 준수 — `ClonePlayback.cs:53` `Mathf.Clamp(tick + 1, 0, FrameCount - 1)`, 48~52행에 "FixedUpdate는 물리 시뮬 이전 → MovePosition은 스텝 끝에 도달" 근거 주석 명시. `RoundManager.cs:88-96` 호출 순서 `ApplyTick → CaptureTick → tick++` 준수. 실측 `tick == recorder.FrameCount` 항상 일치(288/288, 481/481, 751/751), `Debug.Assert` 미발화.

### 나머지 Critic 위험
| # | 항목 | 결과 | 근거 |
|---|---|---|---|
| 3 | 좌표 소스 = `Rigidbody2D.position` | ✓ | CharacterRecorder.cs:57 |
| 4 | bodyType 전환 전 속도 0 | ✓ | CharacterActor.cs:71-77 (1→2 순서) |
| 5 | IsFacingRight가 `linearVelocity.x` 부호 파생 | ✓ | CharacterRecorder.cs:52-54 → ClonePlayback.cs:57 `flipX` 소비 (죽은 필드 아님) |
| 6 | Enter 입력 = `Keyboard.current` | ✓ | RoundManager.cs:108-126 (구 `Input.*` 없음) |
| 7 | `frames.Add()` + 재생 인덱스 클램프 | ✓ | CloneRecording.cs:30, 41 / ClonePlayback.cs:53 |
| 8 | 시간 상한 = `CeilToInt(초/fixedDeltaTime)` | ✓ | RoundManager.cs:63 (하드코딩 없음, 실측 751) |
| 9 | EventSystem = `InputSystemUIInputModule` | ✓ | MCP: `UnityEngine.InputSystem.UI.InputSystemUIInputModule (enabled=True)` |
| 10 | onClick 클로저 지역 복사 | ✓ | CharacterSelectUI.cs:29-30 `int index = i;` |

## 피드백

### Critical
없음.

### Major
없음.

### Minor
1. **[Minor] Update()에서 매 프레임 문자열 생성** — `CharacterSelectUI.cs:44-47`. 상태가 바뀌지 않아도 매 프레임 보간 문자열 + enum ToString 할당이 발생한다. 평가 기준 "문자열 연산을 Update에서 하는지" 위반. 개선: 이전 상태/카운트를 캐시해 변경 시에만 갱신하거나, RoundManager에 상태 변경 이벤트를 두고 구독.
2. **[Minor] Instantiate한 InputActionAsset이 파괴되지 않음** — `PlayerController.cs:47`. 런타임 사본은 씬 언로드 시 자동 회수되지 않는 `ScriptableObject` 계열이다. `OnDestroy`에서 `Destroy(_inputActions)` 필요. Phase 4 레벨 로딩 반복 시 누수.
3. **[Minor] onClick 구독 해제 없음** — `CharacterSelectUI.cs:26-31`. `Awake`에서 `AddListener`만 하고 `OnDestroy`에서 `RemoveAllListeners()`가 없다. 평가 기준 "이벤트/델리게이트 구독 후 OnDestroy에서 해제" 위반(실피해는 낮음 — 버튼이 UI와 함께 파괴됨).
4. **[Minor] Rewind 시맨틱 모호** — `RoundManager.cs:195-221`. 녹화 중 Backspace를 누르면 **현재 테이크 폐기 + 확정 스택 1개 pop**이 동시에 일어난다. 스펙("최상단 1개만 pop")대로면 녹화 중에는 테이크만 버리고 스택은 남기는 편이 의도에 가깝다. 의도된 동작인지 확인 필요.
5. **[Minor] 0프레임 확정 엣지케이스** — 선택 직후 물리 스텝 1회 전에 Enter를 누르면 `FrameCount == 0`인 클론이 확정된다. `ClonePlayback.ApplyTick/ResetToStart`가 조기 리턴하므로 클론이 그 자리에 굳는다(실측 재현됨). 실제 조작으로는 재현 난이도가 높으나 `ConfirmClone`에 `FrameCount == 0` 가드 권장.
6. **[Minor] 미사용 public 멤버** — `CharacterActor.SpawnPosition`(38), `CloneRecording.Frames`(20), `CloneRecording.SlotIndex`(18, 쓰기만 하고 읽는 곳 없음), `CharacterRecorder.FrameCount`(26). 단순성 감점 항목. Phase 3 undo 확장 대비라면 주석으로 의도를 남기거나 삭제.
7. **[Minor] Debug.Assert 미래핑** — `CharacterRecorder.cs:48`. 프로젝트 규칙상 디버그 코드는 `#if DEVELOPMENT_BUILD || UNITY_EDITOR` 래핑 대상이나, `Debug.Assert`는 `[Conditional("UNITY_ASSERTIONS")]`로 릴리스 빌드에서 인자 평가까지 제거되므로 실질 문제는 없음. 감점 최소화.

## MCP 실측 결과

**콘솔**: 컴파일 에러 0 / 워닝 0. 플레이 전 구간 러닝 후 로그 1건(`[RoundManager] LEVEL CLEARED — 사용한 클론 2기`).

**하이어라키**: 루트 7개 — `=== CAMERA === / === LIGHTING === / === ENVIRONMENT ===(3) / === PLAYER ===(3) / === CLONES ===(0) / === MANAGERS ===(1) / === UI ===(2)`. RoundManager는 `=== MANAGERS ===` 하위 ✓.

**SerializeField 참조 (SerializedObject 실측)**:
- `RoundManager._characters` = [Character_1, Character_2, Character_3] (None 없음), `_maxRecordSeconds = 15`, `_clonesParent = === CLONES ===`
- `LevelExit._roundManager` = OK
- `CharacterSelectUI._roundManager` = OK, `_slotButtons` = [SlotButton_1, SlotButton_2, SlotButton_3], `_statusText` = StatusText
- `PlayerController` (3기 동일): `_groundCheck` OK, `_groundCheckSize (0.5, 0.1)`, `_groundLayer = 256 (= 1<<8, Ground)`, `_inputActions = InputSystem_Actions`, `_jumpForce = 14`, `_moveSpeed = 7`

**Canvas / RectTransform**: Canvas 1920x1080 ScreenSpaceOverlay, ScaleWithScreenSize(ref 1920x1080), GraphicRaycaster ✓
- StatusText: pivot(0,1) world BL(30,960) TR(1130,1050) → 범위 내 ✓, Text alpha 1, font=LegacyRuntime
- SlotButton_1: anchoredPos(30,30) sizeDelta(150,70) → world BL(30,30) TR(180,100) ✓
- SlotButton_2: world BL(200,30) TR(350,100) ✓
- SlotButton_3: world BL(370,30) TR(520,100) ✓
- 전부 Canvas(0,0)~(1920,1080) 범위 내, Image alpha 0.9, raycastTarget=True
- EventSystem: `InputSystemUIInputModule` ✓ (StandaloneInputModule 아님)

**물리 / 레이어**:
- Exit: BoxCollider2D `isTrigger=True`, bounds (5.5,1.3)~(6.5,2.3), layer 0
- Platform: layer **8 (Ground)**, bounds (3.0,0.7)~(7.0,1.3)
- Ground: layer **8 (Ground)**, bounds (-12,-3.5)~(12,-2.5)
- Player.prefab layer = 0 (Default) — 코드가 런타임에 Clone일 때만 8로 전환 ✓
- 레이어 충돌 매트릭스: Default(0)↔Ground(8) = 충돌 ON ✓
- `Physics2D.queriesStartInColliders = True` → 라이브를 Default로 유지한 것이 필수였음 (실측으로 자가 검출 없음 확인)

**캐릭터 스폰 이격**: (-4,-2) / (0,-2) / (4,-2) — 4유닛 간격 (요구 1.5유닛+ 충족)

**점프 도달고 수치 검증**: mass=1, jumpForce=14(Impulse) → v₀=14. gravityScale=3, Physics2D.gravity=-9.81 → g=29.43. 최대 상승 = 14²/(2×29.43) = **3.330**
- 지면 위(중심 y=-2.0, 하단 -2.5) → 정점 하단 **0.83** < Platform top 1.30 → **도달 불가** ✓
- 클론 위(중심 y≈-0.97, 하단 -1.47) → 정점 하단 **1.86** > 1.30 → **도달 가능** ✓
→ 레벨이 "클론 밟기"를 강제하도록 설계됨. 코어 루프 검증 가능.

## 수정 파일

### 신규
- `Assets/Scripts/Core/RecordedFrame.cs`
- `Assets/Scripts/Core/CloneRecording.cs`
- `Assets/Scripts/Clone/CharacterRecorder.cs`
- `Assets/Scripts/Clone/ClonePlayback.cs`
- `Assets/Scripts/Clone/CharacterActor.cs`
- `Assets/Scripts/Managers/RoundManager.cs`
- `Assets/Scripts/Level/LevelExit.cs`
- `Assets/Scripts/UI/CharacterSelectUI.cs`

### 수정
- `Assets/Scripts/Player/PlayerController.cs` (106 → 111줄, +5 / -0 — Edit 전용, 파괴적 변경 없음)
- `Assets/Scripts/AfterYou.asmdef` (`UnityEngine.UI` 참조 추가)
- `Assets/Scenes/SampleScene.unity`
- `Assets/Prefabs/Player/Player.prefab`
