# 하네스 계획 — 클리어 리플레이 연출 1차분 (협력 고리 타임라인 + 리플레이 모드)

> 실행 형태: **고위험형** (RoundManager 코어 인접 + 공개 API 추가 + 성공 패턴 없는 신규 유형) — 독립 Critic 검증 완료, 결과 머지본.
> 스냅샷 커밋: `777e34a` (clean). 시작 21:59.

## 스펙 요약

GDD §9.3 클리어 리플레이 1차분 (음악 스텝 2차 제외):
- 기믹 C# 이벤트 노출: OnPressed(PressurePlate) / OnOpened(Door) / OnLandedOnClone(PlayerController) / OnCleared(LevelExit) — 이미 구현된 상태 변화 지점에서 발화.
- 클리어 시 리플레이 자동 진입: 전 클론 + 라이브(히어로) 궤적을 처음부터 동시 재생.
- 히어로는 원본 룩(원본 머티리얼·알파 1), 클론은 스캔라인 룩 그대로.
- 탈출 완료(히어로 궤적 끝) 시 2초 뒤 반복 재생.
- 스킵: 아무 키/클릭 → 즉시 종료 → 클리어 흐름(Next) 복귀. "아무 키나 눌러 건너뛰기" 힌트. Next 버튼은 리플레이 중에도 유지.
- 종료(완주/스킵 공통) 후 undo 스택 리셋 + 기존 클리어 흐름 복귀.

## 아키텍처

성공 패턴 #7(코어 밖 오버레이 모드): RoundManager는 Cleared로 휴면(FixedUpdate가 state!=Recording이라 return), 신규 **ReplayDirector**가 자체 FixedUpdate로 중앙 틱을 대행 구동. RoundManager 수정은 public 메서드 1개 추가뿐(보호 블록 diff 0).

확인된 핵심 사실:
- PressurePlate/Door는 자체 FixedUpdate 구동(항상 활성) — 리플레이 중에도 자동 감지·발화. ITickGimmick(토글/타임도어/이동발판/붕괴/포탈/점프대/킬존)은 ReplayDirector가 DriveGimmickTick으로 구동.
- 클리어 순간 히어로의 미확정 녹화는 hero.Recorder.Recording에 잔존(ConfirmClone 미호출). 라이브 라운드 박스는 Record 모드로 recording 보유, 소유 박스는 Replay 모드.
- 캐스트 식별은 공개 API만: RoundManager.LiveCharacter(106) + Mode==Clone 액터.
- 압력판 마스크 513은 Clone 레이어 포함 → 리플레이 히어로(Clone 레이어)도 판을 누른다. KillZone·포탈·점프대는 마스크 1(라이브 전용)이라 리플레이 캐스트에 무해.

## 수정 파일 (전부 100줄± — **Edit 전용**, 이벤트는 System.Action)

1. **PressurePlate.cs** (201줄) — `public event System.Action OnPressed;`(IsPressed 79 인근) + FixedUpdate 엣지 블록(116~123)에서 눌림 전환 시 Invoke. 사전 스캔용 부수효과 없는 질의 `public bool EvaluatePressed() => MeasureWeightOnPlate() >= _requiredWeight;` — **기존 MeasureWeightOnPlate(133) 재사용만, 로직 복제 금지**. 이벤트 독스트링에 "핸들러에서 상태 전환 메서드 호출 금지(연출·큐잉 전용)" 명문화 — FixedUpdate 내부 발화라 fail#3 재발 방지.
2. **Door.cs** (63줄) — `public event System.Action OnOpened;` + ApplyState(51~60)에서 isOpen==true 전환 시 Invoke(Awake의 ApplyState(false)는 미발화). 동일 독스트링.
3. **PlayerController.cs** (265줄) — `public event System.Action OnLandedOnClone;` + `public bool IsCloneUnderfoot()`: 기존 _groundCheck.position/_groundCheckSize 기하 재사용, Clone 레이어 전용 ContactFilter + 재사용 List, **자기 콜라이더 제외**(방어). Update(87~134)에서 접지 엣지(false→true) && IsCloneUnderfoot() 시 Invoke. 필드: _wasGrounded, Clone 마스크 캐시, 오버랩 버퍼.
4. **LevelExit.cs** (43줄) — `public event System.Action OnCleared;`. OnTriggerEnter2D(29~40): **Invoke만** `_roundManager.State != Cleared` 가드(상태 전환 **이전**, 아직 Recording일 때 발화), OnLevelCleared() 호출(39)은 무조건 유지 — 리플레이 중 트리거 재발화 중복 방지.
5. **RoundManager.cs** (815줄) — OnLevelCleared(674~681) 아래에 `public void ResetUndoStack()` 추가: **첫 줄 `if (_state != RoundState.Cleared) return;` 가드**(Critic ③-2 — Recording 중 오호출 시 재생 목록 desync 방지) + `_confirmedSlots.Clear();`. 보호 블록(ResolveCrush 345~408 / Push 419~438 / FixedUpdate 중앙 틱 258~321 / RestoreAllSpawnOverlaps 703~713) **diff 0**.
6. **LevelManager.cs** (329줄) — `[SerializeField] private ReplayDirector _replayDirector;` + LoadLevel 6단계(201~205) 직후 `_replayDirector.Bind(_roundManager, _spawnedActors.ToArray(), _currentLevel.Boxes, gimmicks)` + CleanupCurrentLevel(114~132) 선두 `_replayDirector.Unbind()`(ESC 타이틀/다음 레벨/엔딩 전부 커버, 호출부 3곳: 106/137/242). **Update의 Cleared 블록(318) 앞에 `if (_replayDirector != null && _replayDirector.IsReplaying) return;` 가드**(Critic ③-1 — 리플레이 중 Enter/N이 스킵과 LoadNextLevel을 같은 프레임 동시 발동하는 누수 차단. IsReplaying은 스킵된 프레임까지 true를 유지하도록 프레임 스탬프(Time.frameCount) 비교로 구현). 미할당 null 가드 폴백(리플레이 없이 기존 흐름).
7. **PushableBox.cs** (142줄) — `public bool TryGetRecordedPosition(int tick, out Vector2 position)` (ClonePlayback.TryGetFuturePosition과 동일 클램프).
8. **ClonePlayback.cs** (138줄) — `private float _displayAlpha = CloneAlpha;` + `public void SetDisplayAlpha(float)` + UpdateReveal(112~125)의 CloneAlpha 사용처를 _displayAlpha로 교체 + SetRecording에서 기본값 복원.
9. **CharacterActor.cs** (231줄) — `public void ApplyReplayOriginalLook()`: **sharedMaterial=_defaultMaterial 복원 + _playback.SetDisplayAlpha(1f)만. 스프라이트 알파 직접 대입 금지**(알파 소유권은 ClonePlayback.UpdateReveal 단독 — Critic ③-3: 직접 대입하면 리플레이 시작 순간 스폰에서 1프레임 번쩍임).

## 신규 파일

10. **Assets/Scripts/Replay/ReplayDirector.cs** — 씬 상주 MonoBehaviour.
   - `Bind(rm, actors, boxes, gimmicks)` / `Unbind()`. `public bool IsReplaying`(스킵 프레임 포함 — Time.frameCount 스탬프).
   - Update: (a) 미재생 && rm.State==Cleared && 레벨당 1회 → StartReplay. (b) 재생 중 아무 키(Keyboard.current.anyKey.wasPressedThisFrame) 또는 마우스 버튼 클릭 → StopReplay(스킵).
   - **StartReplay 순서 고정**: ① 히어로=rm.LiveCharacter(null 또는 FrameCount 0이면 중단·폴백) ② **사전 스캔(모드 변경 전 — 히어로가 아직 Live/Default 레이어일 때 수행**, Critic ④-2) ③ hero.Playback.SetRecording(hero.Recorder.Recording) ④ 캐스트 전원 SetMode(Clone) ⑤ ResetToStart ⑥ 히어로만 ApplyReplayOriginalLook(**반드시 SetRecording 이후** — SetRecording이 _displayAlpha를 기본값으로 되돌림) ⑦ recording 보유 박스(TryGetRecordedPosition(0) true) SetMode(Replay)+ResetToBase(frames[0]==base) ⑧ ITickGimmick 전부 ResetGimmick ⑨ _replayTick=0, HUD 표시(총 고리 수 + 스킵 힌트).
   - **사전 스캔(헤드리스, 한 프레임)**: 총 틱 T = hero.Recorder.FrameCount. t=0..T-1: 캐스트를 **frames[t]로 배치**(CharacterActor.SetPosition — rb+transform 동시. **t+1 배치 금지** — 판이 보는 것은 스텝 시작 위치=frames[t], Critic ④-3 주석 필수), 박스는 TryGetRecordedPosition(t)+transform 배치, Physics2D.SyncTransforms() → 각 PressurePlate.EvaluatePressed() **엣지 기록은 t≥1부터**(t=0은 기준선만 — Critic ④-1) → OnPressed 틱, 연동 Door의 OnOpened 틱=같은 틱, 히어로 IsCloneUnderfoot() 엣지 → OnLandedOnClone 틱, OnCleared 틱=T-1. 스캔 중 기믹 이벤트 구독/발화 없음(부수효과 0 질의만).
   - FixedUpdate(재생 중 && 루프 대기 아님): RoundManager와 동일 순서 — ①박스 ApplyBoxTick(_replayTick) ②DriveGimmickTick(_replayTick) ③캐스트 Playback.ApplyTick(_replayTick)(frames[t+1] 규약은 ApplyTick 내부 소유) ④타임라인 틱 도달 시 HUD 고조 단계 +1 + 자체 `public event Action<int> OnRingReached` 발행 ⑤_replayTick++ ⑥ T 도달 시 1패스 완주 후처리(최초 1회 rm.ResetUndoStack()) + 2초 realtime 대기(**틱 구동 완전 정지 플래그와 병용 — 결정성 제약은 틱 구동 대상에만 적용, 연출 대기는 틱 밖임을 주석 명시**, Critic ④-4) 후 ResetForLoop(기믹 리셋+ResetToStart+ResetToBase+틱 0+HUD 단계 0).
   - StopReplay(스킵/Unbind 공용, 멱등): 구동 정지 + 루프 타이머 취소 + HUD 숨김 + 후처리(미수행 시 rm.ResetUndoStack()). 캐스트는 현 위치 동결(Kinematic). Cleared 유지 → Next 버튼/N/Enter/ESC 기존 흐름 복귀.
   - 발화 정리: OnPressed/OnOpened는 리플레이 중 판/문 자체 FixedUpdate에서 **자연 발화**(스캔 틱과 결정론적 일치 — MovePosition은 스텝 중 이동이라 판이 보는 위치는 항상 frames[t]). OnLandedOnClone/OnCleared는 리플레이 중 발화 주체가 없어(컨트롤러 꺼짐) 타임라인 기반 OnRingReached가 대행 — 주석 문서화. ReplayDirector는 기믹 이벤트를 구독하지 않는다(스캔 타임라인 단일 소스).

11. **Assets/Scripts/UI/ReplayHudUI.cs** — 상시 활성 부모 + Content 래퍼 패턴(#6). `Show(totalRings)` / `SetStage(n)` / `Hide()`. 화면 구석 "아무 키나 눌러 건너뛰기" 힌트 + 고조 단계 pip 누적 표시(●○, 고리 수만큼).

## 씬 수정 (SampleScene — MCP execute_code 일괄 조립, 워크트리 OFF)

- "ReplayDirector" GO(Managers 계층) + 컴포넌트 + LevelManager._replayDirector 연결.
- Canvas 아래 ReplayHud(루트 상시 활성 + Content 자식): SkipHintText(우하단), EscalationText(상단 중앙). 기존 UI와 동일한 UGUI Text. ReplayDirector 참조 연결. 씬 저장.

## 구현 순서

① 이벤트 4종+접근자(파일 1~4, 7~9) → ② RoundManager.ResetUndoStack(5) → ③ ReplayDirector+ReplayHudUI 신규(10~11) → ④ LevelManager 배선(6) → ⑤ 컴파일 확인 1회(전체 수정 후) → ⑥ 씬 조립+저장.

## 참조처 확인 결과

- 전부 "추가"만 — 기존 시그니처/enum 변경 0 (CharacterMode·SetMode 불변) → 기존 호출부 영향 0.
- CleanupCurrentLevel 호출부 3곳(EnterTitle 106/LoadLevel 137/StartEnding 242) → Unbind 커버. 디버그 PageUp/Down도 LoadLevel 경유 커버.
- LoadNextLevel 가드(State==Cleared) 리플레이 중 성립 + IsReplaying 가드로 키 누수 차단. Next 버튼 클릭=스킵 동시 발동은 스펙상 무해(허용).
- CharacterSelectUI._nextButton은 State==Cleared에서 표시 유지(101). RoundManager.Rewind는 Cleared 차단(638) → Backspace 무해.

## 동작 조건 체크리스트 (Evaluator 채점 기준)

1. 클리어(라이브 출구 도달 → Cleared 전이) 시 ReplayDirector가 자동으로 리플레이 시작 — 전 클론+히어로가 각 궤적 frame0(스폰)에서 동시 재생된다.
2. 리플레이 궤적이 실제 플레이와 틱 단위 일치 — 리플레이 중 임의 틱에서 클론/히어로 rb.position이 recording frames 값과 일치(ApplyTick frames[t+1] 규약 그대로).
3. 사전 스캔 타임라인 (type, tick) 목록이 생성되고, 리플레이 중 PressurePlate.OnPressed/Door.OnOpened **실발화 틱 == 스캔 틱**(실측). OnLandedOnClone/OnCleared는 OnRingReached가 스캔 틱에 발행.
4. 고조 단계가 고리 수만큼 누적 표시 — ReplayHudUI pip이 각 고리 틱에 +1, 총합 = 타임라인 이벤트 수.
5. 리플레이 중 아무 키/클릭 → 즉시 스킵, 후처리(ResetUndoStack + HUD 숨김)가 완주와 동일. 스킵 프레임의 Enter/N이 LoadNextLevel로 새지 않는다(IsReplaying 가드).
6. 리플레이 후 Next(버튼/N/Enter) 다음 레벨 정상, ESC 타이틀 복귀 정상, 재시작 정상.
7. 기존 회귀 없음 — 일반 라운드(녹화/재생/되감기/기믹) 불변, RoundManager 보호 블록 diff 0, 기믹 수정은 이벤트 추가만.
8. 리플레이 중 "아무 키나 눌러 건너뛰기" 힌트 표시, 종료 시 숨김.
9. Next(완료) 버튼은 리플레이 중에도 계속 표시.
10. 히어로는 원본 머티리얼+목표 알파 1, 클론은 스캔라인+0.85 — 동시 재생 중 시각 구분.
11. 완주 시 2초 뒤 처음부터 반복 재생(기믹/박스/캐스트/HUD 단계 전부 초기화).
12. 라이브 플레이 중에도 이벤트 4종이 기존 상태 변화 지점에서 발화한다(구독자 없어도 발화 자체는 수행).

## 셀프 리뷰 (틀릴 수 있는 지점 → 대응)

1. 스캔-실재생 발화 틱 불일치 → 스텝 시작 위치 논증 + Evaluator 스모크 실측 필수 항목.
2. 스캔 중 히어로 자기검출 → 스캔은 SetMode 전(Default 레이어) 고정 + 자기 콜라이더 제외 방어 코드.
3. 클리어 직후 히어로가 잠깐 Dynamic으로 남음 → 같은 프레임 Update의 StartReplay가 즉시 회수(1프레임 미만).
4. ESC = 스킵+타이틀 동시 → StopReplay 멱등 + Unbind 재정지 무해.
5. 루프 대기 이중 진입 → 대기 플래그 + 타이머 취소 경로 단일화.
6. ResetUndoStack 후 HUD 클론 수 표시 0 가능 → 코스메틱, 다음 전환으로 소멸(허용).
7. 리플레이 중 LevelExit 트리거 재발화 → State==Cleared 가드로 Invoke 차단.

## Evaluator 스모크 제안 (플레이 모드 1회)

Level_1_1(압력판+문+클론 밟기 가능)에서 리플렉션 시나리오: 무거운형 판 위 확정 → 가벼운형 출구 도달 강제 → ①리플레이 자동 진입 확인 ②특정 틱 클론 rb.position vs recording 대조 ③스캔 타임라인 vs OnPressed 실발화 틱 대조(이벤트 구독 계측) ④anyKey 시뮬 스킵 → ResetUndoStack 반영(ConfirmedCount 0)·IsReplaying 가드·Next 동작 확인.
