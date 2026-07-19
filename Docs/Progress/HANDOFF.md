# HANDOFF — 현재 상태 스냅샷

> 이 파일은 **날짜 로그와 달리 항상 덮어쓴다.** 새 세션은 이 파일 하나로 "지금 어디인지"를 파악하고, 상세 이력은 날짜 파일을 참조한다.

**마지막 갱신**: 2026-07-19

## 현재 위치

- **Phase 3-2 완료 · 커밋됨** (`191c90a` — 벽타기형 정체성 벽만, 하네스 v2 첫 실행 90/100). 코어 4종 전부 가동(무거운/가벼운/운반/벽타기 — 천장 제외)
- **벽점프 락아웃 수정 · 커밋됨** (`2300a0e`, 2026-07-19) — `PlayerController.cs` 재부착 락아웃 0.15s + 락아웃 중 수평 덮어쓰기 유예 + 접지 시 즉시 해제
- **접지 통일 · 커밋됨** (`b7e8f2e`, 2026-07-19) — 전 정체성 접지 마스크 3840(Ground|Clone|Climbable|Box), Box 레이어(11) 신설, PushableBox 레이어 이동(프리팹 + Level_1_4 내장 박스), 압력판 마스크 513→2561
- **클리어 Next 버튼 · 커밋됨** (`b3796d7`, 2026-07-19) — 화면 중앙 Next 버튼(Cleared 상태에서만 표시) → `LevelManager.LoadNextLevel()`. N키·Enter/NumpadEnter도 동작. 같은 커밋에 **[임시 디버그] PageDown/PageUp 강제 스테이지 이동** 포함(`#if DEVELOPMENT_BUILD || UNITY_EDITOR` 래핑 — **테스트 끝나면 제거 예정**)
- ⭐ 재미 검증 통과 (2026-07-18) → Phase 3 진행 중
- **Level_1_4/1_5 사용자 플레이테스트 대기** — 박스 밀기 감각 + 벽타기 조작감 + 벽점프 궤적 + 박스/벽/클론 위 점프 + 박스 압력판 회귀 체감

## 다음 작업 (우선순위 + 차단 관계)

1. **[사용자 몫] Level_1_4/1_5 플레이테스트** — 박스 밀기 + 벽타기 + 벽점프(수정분) 체감. 감각 문제 시 수치 조정 후 진행
2. **환경 기믹 6~8종** (Phase 3-3) — 토글 스위치, 시간제한 문, 이동 발판, 가시, 낙하 구멍
3. **클리어 리플레이 연출(§9.3) → undo 스택 리셋** (Phase 3 잔여) / **천장 이동** — 가시·낙하 기믹 도입 후 확장
4. **잔여 Minor**: `CharacterSelectUI.cs` 매 프레임 `SetActive`(Phase 5 병합) / `PressurePlate.cs:113` 핫패스 트래버스 / 클라이머 이탈 블립 중 기둥탑 마운트(향후 Climbable 지형 설계 유의)

## 유효 제약 (건드리면 깨지는 것들)

- **Light JumpForce 상한 14.96** — 초과 시 혼자 플랫폼에 올라가 코어 게이트 붕괴
- **Weight ≠ mass** — Weight는 압력판 판정 전용 순수 데이터. `Rigidbody2D.mass`에 대입하면 도달고 1/4 붕괴
- **틱 오프셋 규약** — 클론 재생 목표는 `frames[tick+1]`. `tick` 그대로 쓰면 1틱 밀려 압력판/발판 타이밍 버그
- **정체성 적용은 Awake에서만, 필드 대입만** — Start에 두면 영영 미호출, 캐시 참조 접근 시 NRE
- **압력판은 기하 판정만** — 클론은 MovePosition이라 `linearVelocity` 항상 0. 속도 기반 착지 판정 금지
- **bodyType 전환 순서 엄수** — 속도 0 → bodyType → layer → 컴포넌트 → SetActive
- **좌표 소스는 `Rigidbody2D.position`** — Interpolate 때문에 `transform.position`은 보간값
- **Teardown 순서** — `_liveIndex=-1`을 `_characters=null`보다 먼저 + 3컬렉션(`_confirmedSlots`/`_originalParents`/`_ignoredSpawnPairs`) 전부 Clear
- **스태거 연출은 알파만 지연** — 스폰 타이밍을 늦추면 궤적 전체가 밀려 협력 타이밍 붕괴
- **RoundManager 보호 블록** (ResolveCrush/Push/중앙 틱/IgnoreCollision 관리) — 하네스 작업 시 diff 0 유지 대상
- **IgnoreCollision 복구 4개 진입점** — EnterSelecting/Rewind/ConfirmClone/RestartTake 전부에서 전체복구. 누락 시 클론 밟기가 영영 불가

## 확정 사양 / 폐기한 접근

- **벽 부착은 Climbable 레이어(10) 전용** (2026-07-18) — Ground 벽 전체를 부착 대상으로 하면 지형이 사다리가 되어 게이트 붕괴(렛지 옆면 파훼). 부착 판정은 `_climbableLayer` 마스크로만. 천장 이동은 미구현(기믹 도입 후 확장)
- **부착 상태 규약** — 매 틱 재검증(자가 치유) + `Detach()`가 gravityScale 복원 단일 소유(이중 가드 필수, OnDisable에서도 호출). 이 규약을 깨면 "부착 중 확정→되감기→재선택"에서 중력 0 잔존 잠복 버그
- **벽점프 락아웃 규약** (2026-07-19) — 락아웃 카운터는 **부착 branch와 FixedUpdate 수평 덮어쓰기만** 차단한다. 이탈 경로는 절대 차단 금지(자가 치유 유지). 접지 시 카운터 즉시 0(락아웃 중 착지 시 입력 무시 방지)
- **fable 기본 OFF + 하네스 체제** (2026-07-18 최종) — "fable ON 상시 병용"은 **폐기** (일상 턴의 위임 오버헤드 대비 이득 작음). fable은 탐색 위주 세션에서만 일시적으로 켠다 (fable.md 하네스 예외 조항 유지로 켜도 충돌 없음). 하네스 모델 배분(Planner=메인 루프, Critic·Evaluator=fable, Generator=opus 강제)은 스킬에 명시되어 토글과 무관하게 유지
- **Evaluator 반증 채점 체계** (2026-07-18) — 독립 에이전트 채점(자기 채점 편향 절단) + 스펙 감사 + 적대 시나리오 3개 실측 의무 + 만점 방지 조항. 이전 회차(99/100)와 점수 직접 비교 불가 — 하락이 정상

- **접지 전면 통일** (2026-07-19, 사용자 결정) — 점프는 어디서든(박스/벽/클론 위) 전부 가능. 전 정체성 `_groundMask` = **3840 (Ground|Clone|Climbable|Box)**. 무거운형 차별화는 점프 높이(JumpForce 10 vs 14)로만. 이전 사양 "가벼운형만 클론 밟고 점프"·"무거운형 클론 위 점프 불가"·"박스는 밀기 전용" **전부 폐기**
- **Box 레이어(11) 신설** (2026-07-19) — 박스는 Default가 아닌 Box 레이어. Default를 접지 마스크에 넣으면 라이브 자신(Default)을 발밑 체크가 잡아 무한 점프가 되므로 금지. 압력판 `_detectionMask`는 **2561 (Default|Clone|Box)** — 박스 감지 유지용 Box 비트 필수
- **박스 소유권 모델** — 미소유 박스는 운반형 라이브 라운드에만 Record(Dynamic), 그 외 Frozen(Kinematic). 확정 시 변위 있으면 OwnerSlot 귀속 → 이후 Replay(tick+1 재생). "운반형만 민다"는 물리 트릭이 아닌 `CanManipulateObjects` 데이터 플래그
- **클론은 닫힌 문을 통과한다** (확정 사양) — Kinematic MovePosition의 필연. 문 너머엔 Exit만 배치로 회피
- **무거운형은 클론 위에 서 있을 수 있으나 점프만 불가** — 접지 마스크에 Clone 없음. `excludeLayers`는 ResolveCrush와 모순되어 **폐기**
- **동적 캐릭터 생성 = 비활성 부모 트릭** — SetActive(false) 부모 아래 Instantiate로 Awake 지연 → InjectIdentity → SetActive(true). Critic 판정상 유일한 방법
- **레벨별 스폰** — 레벨 프리팹이 자기 SpawnPoint를 자식으로 포함, LevelManager가 로드 시 RoundManager에 주입

## 상세 이력

- [2026-07-18.md](2026-07-18.md) — 공유 스폰 포인트 + 스태거 페이드인 / Phase 2b 레벨 프리팹 시스템
- [2026-07-12.md](2026-07-12.md) — Phase 1 녹화·재생 코어 / Phase 2a 정체성(SO) + 압력판/문
- [2026-07-08.md](2026-07-08.md) — Phase 0 세팅 + 기본 이동·점프
