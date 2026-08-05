# 최종 통합 계획 (Generator 입력본) — 클리어 리플레이 연출 1차분

> 2026-08-06. 스냅샷 커밋: `df2cf81` (clean). 워크트리 OFF (씬 수정 포함).

## 설계 핵심 판단

- **"사전 스캔" = 결정론 기반 이벤트 로깅**: 기하 재계산으로 타임라인을 만들면 기믹 판정 로직이 이중 소스가 되어 어긋날 위험. 대신 기믹 이벤트(압력판/문)는 "매 테이크 리셋 + 클론 재현" 규약 덕에 클리어 테이크에서 전부 재발화하므로 그 로그가 곧 리플레이 타임라인. 라이브 전용 이벤트(클론 착지)만 테이크별 기록 → 확정 시 슬롯 귀속. 리플레이 시작 전 병합·정렬로 타임라인 확정(고리 N개 = 고조 N단계).
- **리플레이 구동 = RoundManager 상태 머신 밖 독립 ReplayDirector** (성공 패턴 #7 오버레이 모드): 리플레이 중 상태는 Cleared 잔존 — RoundManager FixedUpdate/Update/Rewind 전부 휴면. 전원 Kinematic 재생이라 물리 간섭 0.

## 수정 대상

1. **`Assets/Scripts/Managers/RoundManager.cs`** (815줄, **Edit 전용**) — 수정 허용 구간: 프로퍼티 선언부(99~143 부근), SelectCharacter 말미 562~570, ConfirmClone 573~608, Rewind 649~668, OnLevelCleared 674~681, 신규 메서드는 클래스 말미. **보호(diff 0): FixedUpdate 258~321 / ResolveCrush / Push / RestoreAllSpawnOverlaps**
   - `public int CurrentTick => _tick;`
   - 이벤트 4종: `TakeStarted`(SelectCharacter — ⚠ `_state = Recording;` 569행 **이후** 발화), `TakeConfirmed(int slot)`(ConfirmClone), `SlotRewound(int slot)`(Rewind pop 시), `Cleared`(OnLevelCleared 말미)
   - OnLevelCleared에 클리어 테이크 박스 커밋 블록(ConfirmClone 590~604 로직 복제 — ⚠ `_liveIndex >= 0` 가드 + `actor` 지역 정의 필요)
   - `public void ResetUndoStack()`(Cleared 전용 가드, `_confirmedSlots.Clear()`)
   - `public CharacterActor GetConfirmedActor(int i)`
2. **`Assets/Scripts/Level/PressurePlate.cs`** (200줄, Edit 전용, 116~123 엣지 지점만) — `public event System.Action OnPressed;` false→true 전이에서 발화
3. **`Assets/Scripts/Level/Door.cs`** (62줄) — `OnOpened` — SetOpen 열림 전이에서 발화(Awake ApplyState(false)와 절연)
4. **`Assets/Scripts/Level/TimedDoor.cs`** (97줄) — `OnOpened` — ⚠ SetActivated(true)는 무가드 재호출 구조이므로 **`!_isOpen`일 때만 발화**(타이머 리셋 동작 불변)
5. **`Assets/Scripts/Player/PlayerController.cs`** (264줄, Edit 전용) — `OnLandedOnClone` — ⚠ 접지 판정(93행)은 **기존 그대로 두고**, 발화 조건용 클론 레이어 전용 마스크의 **별도 OverlapBox 1회 추가**(단일 반환 미보장 문제 회피). 공중→접지 전이 && 클론 검출 시 발화. `_wasGrounded` 필드 + Awake 레이어 캐시. 보호: FixedUpdate 속도 하드셋
6. **`Assets/Scripts/Level/LevelExit.cs`** (42줄) — `OnCleared` — OnLevelCleared 호출 직전 발화
7. **`Assets/Scripts/Managers/LevelManager.cs`** (328줄, Edit 전용) — SerializeField 2개(`ReplayDirector _replayDirector` / `ChainTimelineTracker _chainTracker`) + LoadLevel에서 tracker.BindLevel + ReplayDirector에 기믹/박스 주입 + CleanupCurrentLevel 맨 앞 `StopImmediate()`(⚠ pending 딜레이 코루틴도 취소) + Update의 Cleared Next 분기(318행) 직전 **`BlocksClearedInput` 게이트**(IsReplaying만으로는 스킵 프레임 Enter 누수 — 실행 순서 양방향 차단 필요). ESC·디버그 키는 비차단(CleanupCurrentLevel 경유 정리)
8. **`Assets/Scripts/UI/CharacterSelectUI.cs`** (151줄, Edit 전용, 100~108만) — Next 버튼(101행)·상태 텍스트(105행)에 리플레이 게이트
9. **`Assets/Scripts/UI/PaperHudUI.cs`** (43줄) — 클론 카운트(32행) Cleared 상태에서 갱신 스킵(리셋 후 0/N 튐 방지)
10. **SampleScene (MCP 조립)** — ReplayDirector+ChainTimelineTracker 오브젝트, Canvas 아래 리플레이 UI 3종. ⚠ **FadeOverlay가 Canvas 마지막 sibling(최상단) — 신규 UI는 전부 그보다 앞 인덱스(<7)에 삽입**. 펄스 Image `raycastTarget=false` 필수(안 하면 투명 상태에서도 Next 버튼 클릭 영구 차단)

## 신규 파일

1. **`Assets/Scripts/Replay/ChainTimelineTracker.cs`** — 고리 이벤트 수집기(씬 상주). `RoundManager.State == Recording`일 때만 기록. 기믹 로그는 TakeStarted마다 clear(클리어 테이크 로그 = 리플레이 타임라인), 착지 로그는 테이크 임시 → TakeConfirmed 시 슬롯 귀속 / SlotRewound 시 폐기 / ⚠ **클리어 시점(RoundManager.Cleared 수신)에 임시 착지 로그를 클리어 테이크분으로 확정 귀속**(클리어 테이크는 TakeConfirmed가 오지 않음). LevelExit.OnCleared로 클리어 고리 기록. `BuildTimeline()` = 병합 + 틱 정렬 (tick, type) 리스트. 틱 스탬프는 RoundManager.CurrentTick.
2. **`Assets/Scripts/Replay/ReplayDirector.cs`** — 리플레이 구동자(씬 상주). RoundManager.Cleared 구독 → `_startDelay`(Inspector) 후 Begin. ⚠ **Cleared 수신~Begin 사이 딜레이 구간에도 `BlocksClearedInput` true** — 이 구간에 N/Enter가 새면 LoadNextLevel이 액터를 파괴한 뒤 Begin이 파괴 참조를 잡는다. ⚠ 물리 콜백(OnTriggerEnter2D) 스택에서 즉시 SetMode 금지 — 딜레이 0이어도 최소 1프레임 지연 보장.
   - Begin: ① 확정 클론 + 라이브 액터를 자기 리스트로 복사(절연) ② ex-live: **SetMode(Clone) → SetRecording(Recorder.Recording) → ResetToStart 순서 엄수**(SetRecording 전엔 FrameCount 0이라 ResetToStart가 no-op) ③ 전 클론 ResetToStart ④ 기믹 전체 ResetGimmick(PressurePlate는 ITickGimmick 아님 — 자기 치유로 다음 FixedUpdate 자연 해제, 별도 리셋 불필요) ⑤ 박스: **EnterSelecting 737~742 패턴 복제** — 전부 Frozen+ResetToBase 후 소유 박스만 Replay(Discard된 미이동 박스가 Replay로 걸리면 안 됨) ⑥ 리플레이 길이 = 라이브 recording FrameCount
   - 자체 FixedUpdate: 박스 ApplyBoxTick → DriveGimmickTick → 전 클론 ApplyTick → 타임라인 발화 → `_replayTick++` → 완주 판정(`_replayTick >= FrameCount`)
   - ⚠ 타임라인 발화는 `이벤트 틱 <= _replayTick` **소급 방식**(`==`이면 ±1 스탬프 오차로 이벤트 유실 → 고리 N개=N단계 정합 붕괴). 리플레이 길이 초과 틱 이벤트는 BuildTimeline에서 필터 또는 완주 시 잔여 스킵
   - Update(스킵): 아무 키/클릭, 시작 후 그레이스 0.25s. **제외 키: ESC, leftAlt/rightAlt/tab/leftMeta/rightMeta**(Alt-Tab·Win키 유령 입력)
   - EndReplay(완주/스킵 공통): RoundManager.ResetUndoStack() + UI 숨김 + IsReplaying=false + `_endedFrame` 기록. `BlocksClearedInput => 딜레이 대기 중 || IsReplaying || Time.frameCount == _endedFrame`
3. **`Assets/Scripts/Replay/ReplayFlourishUI.cs`** — 고조 카운트 텍스트(단계마다 갱신 + 짧은 스케일 펀치), 풀스크린 흰 펄스(알파 0→0.12→0, 0.25s), 스킵 힌트("아무 키나 눌러 건너뛰기") 표시/숨김

## 구현 순서

1단계: 이벤트 노출 5파일(PressurePlate/Door/TimedDoor/PlayerController/LevelExit) + RoundManager 수정
2단계: ChainTimelineTracker + ReplayDirector + ReplayFlourishUI 신규 작성
3단계: LevelManager/CharacterSelectUI/PaperHudUI 연결 수정
4단계: 컴파일 확인(전 파일 완료 후 1회)
5단계: SampleScene MCP 조립(오브젝트 2개 + UI 3종 + 참조 연결) — execute_code 한 방 조립, FadeOverlay sibling 순서 준수
6단계: 씬 저장 + 콘솔 확인

## 참조처 맵 (Critic 실측)

- RoundState.State 독자 전수: CharacterSelectUI 74/101/105, PaperHudUI 35, RecIndicatorUI 32, LevelManager 210/318, RoundManager 내부 443/261/638/676 — 게이트 필요 지점은 계획 7·8이 전부 커버
- ConfirmedCount → CharacterSelectUI 75(Selecting 전용 무해), PaperHudUI 32(계획 9가 커버). LiveCharacter → LevelExit 37, KillZone 66. ⚠ OnLevelCleared는 _liveIndex를 리셋하지 않음 — 리플레이 중 ex-live 클론 출구 재통과 시 676행 가드로 무해
- 재사용 API 전부 실재 확인: Recorder.Recording / SetRecording / ResetToStart / ApplyTick(tick+1 클램프·FrameCount 0 가드 내장) / ApplyBoxTick / SetMode / ResetToBase / OwnerSlot / ResetGimmick / DriveGimmickTick
- 기믹 커버리지: 실제 레벨은 **Level_1_1~1_8** 8개(1_1·1_3·1_4·1_6에 PressurePlate/Door baked, 전 레벨 LevelExit, TimedDoor는 Gimmicks 프리팹 중첩 인스턴스). tracker 수집은 `_currentLevel.GetComponentsInChildren<T>(true)` 고정
- 마스크 1(라이브 전용) 기믹들(JumpPad/IdentityPortal/KillZone/MovingPlatform 캐리)은 리플레이 중 미발화가 정답 — 결과가 클론 궤적에 기록돼 자동 재현. CrumblingPlatform(513)은 클론이 붕괴 틱 재현(설계 의도)

## 과거 실패 패턴 (Critic)

- 순회 중 상태 전환(fail #3) — ReplayDirector 기믹 순회 중 KillZone.OnLiveDied는 state 가드로 no-op(안전). 타임라인 발화 콜백은 UI 연출만 유지
- 씬 조립 bake 복사 금지(fail #2) — 리플레이 UI는 MCP 도구로 생성

## 참조 패턴

- [카테고리: 시스템조립] #7 엔딩 오버레이 모드 — 코어 상태 enum 무추가, Cleared 휴면 가드 위 독립 시퀀스 + 상태 잔존값 State 독자 전수 조사 + 컬렉션 복사본 절연 + 단일 Stop 이탈 경로 전수
- [카테고리: 코어시스템] #1 녹화·재생 코어 — 중앙 틱 단일 소유(ReplayDirector가 자기 _replayTick 소유), 틱 오프셋 tick+1 규약(ApplyTick 재사용), 순서 규약 복제(박스→기믹→클론)

## 동작 조건 (Evaluator 체크리스트)

- [ ] 클리어 시 _startDelay 후 자동 리플레이 진입 — 전 클론 + ex-live가 시작 지점에서 동시 재생(ex-live도 Clone 모드로 궤적 재생)
- [ ] 리플레이 클론 궤적이 기록과 틱 단위 일치 — 실측: 특정 틱에서 클론 rb.position == recording 해당 프레임(diff 0.000000)
- [ ] 타임라인이 리플레이 시작 전 확정(Begin 시점 고리 수 N 확정) — 압력판/문/착지/클리어 고리 포함
- [ ] 리플레이 중 타임라인 틱 도달 시 고조 단계 발화 — 단계 카운트 UI 1..N 누적 갱신 + 화면 펄스, 발화 틱 == 타임라인 틱(소급 포함)
- [ ] 스킵 힌트가 리플레이 중 화면 구석 표시, 종료 시 숨김
- [ ] 리플레이 중 아무 키/클릭 → 즉시 종료, 같은 프레임 LoadNextLevel 미실행(Enter 스킵 시 레벨 전환 없음)
- [ ] 종료(완주/스킵 공통) 후: undo 스택 리셋(ConfirmedCount 0) + Next 버튼 재표시 + N/Enter 다음 레벨 정상
- [ ] 리플레이 중(딜레이 구간 포함) Next 버튼 숨김 + N/Enter 차단
- [ ] ESC/PageDown으로 이탈 시 리플레이 잔재 0(StopImmediate 경유)
- [ ] HUD 클론 카운트가 undo 리셋으로 0으로 튀지 않음(Cleared 홀드)
- [ ] 기존 회귀 없음: RoundManager 보호 블록 diff 0, 콘솔 에러 0
