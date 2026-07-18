# HANDOFF — 현재 상태 스냅샷

> 이 파일은 **날짜 로그와 달리 항상 덮어쓴다.** 새 세션은 이 파일 하나로 "지금 어디인지"를 파악하고, 상세 이력은 날짜 파일을 참조한다.

**마지막 갱신**: 2026-07-18

## 현재 위치

- **Phase 3-2 완료** (벽타기형 정체성 — 벽만, 하네스 v2 첫 실행 90/100) — 커밋 직전. 코어 4종 중 3종(무거운/가벼운/운반/벽타기 — 천장 제외) 가동
- ⭐ 재미 검증 통과 (2026-07-18) → Phase 3 진행 중
- **Level_1_4/1_5 사용자 플레이테스트 대기** — 박스 밀기 감각 + 벽타기 조작감(부착/등반/이탈) 체감

## 다음 작업 (우선순위 + 차단 관계)

1. **[사용자 몫] Level_1_4/1_5 플레이테스트** — 감각 문제 시 수치 조정 후 진행
2. **벽점프 락아웃 수정** (Major 잔존) — 벽점프 수평 성분이 FixedUpdate 덮어쓰기로 무효 + 벽 방향 홀드 중 점프 무효(즉시 재부착). 수정안: 재부착 락아웃 ~0.15s + 락아웃 중 수평 덮어쓰기 유예. `PlayerController.cs:110-116` 부근
3. **환경 기믹 6~8종** (Phase 3-3) — 토글 스위치, 시간제한 문, 이동 발판, 가시, 낙하 구멍
4. **클리어 리플레이 연출(§9.3) → undo 스택 리셋** (Phase 3 잔여) / **천장 이동** — 가시·낙하 기믹 도입 후 확장
5. **박스 위 올라서기 레이어 정책 결정** — 박스 Default(0)는 접지 마스크에 없어 딛기 불가 (들기·전달 규칙과 함께 검토)
6. **잔여 Minor**: `CharacterSelectUI.cs` 매 프레임 `SetActive`(Phase 5 병합) / `PressurePlate.cs:113` 핫패스 트래버스 / 클라이머 이탈 블립 중 기둥탑 마운트(향후 Climbable 지형 설계 유의)

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
- **fable+하네스 병용 체제** (2026-07-18) — fable ON 상시(비정형 작업 라우팅), 구현은 `/harness`(진행 중 오케스트레이션 지침 미적용). 모델 배분: Planner·Critic·Evaluator=fable, Generator=opus 강제. "fable OFF 고정" 접근은 **폐기**
- **Evaluator 반증 채점 체계** (2026-07-18) — 독립 에이전트 채점(자기 채점 편향 절단) + 스펙 감사 + 적대 시나리오 3개 실측 의무 + 만점 방지 조항. 이전 회차(99/100)와 점수 직접 비교 불가 — 하락이 정상

- **박스는 밀기 전용 (현재)** — 박스 레이어 Default(0)는 접지 마스크(256/768)에 없어 박스 위 점프 불가. 딛기 허용은 레이어 정책 결정 후
- **박스 소유권 모델** — 미소유 박스는 운반형 라이브 라운드에만 Record(Dynamic), 그 외 Frozen(Kinematic). 확정 시 변위 있으면 OwnerSlot 귀속 → 이후 Replay(tick+1 재생). "운반형만 민다"는 물리 트릭이 아닌 `CanManipulateObjects` 데이터 플래그
- **클론은 닫힌 문을 통과한다** (확정 사양) — Kinematic MovePosition의 필연. 문 너머엔 Exit만 배치로 회피
- **무거운형은 클론 위에 서 있을 수 있으나 점프만 불가** — 접지 마스크에 Clone 없음. `excludeLayers`는 ResolveCrush와 모순되어 **폐기**
- **동적 캐릭터 생성 = 비활성 부모 트릭** — SetActive(false) 부모 아래 Instantiate로 Awake 지연 → InjectIdentity → SetActive(true). Critic 판정상 유일한 방법
- **레벨별 스폰** — 레벨 프리팹이 자기 SpawnPoint를 자식으로 포함, LevelManager가 로드 시 RoundManager에 주입

## 상세 이력

- [2026-07-18.md](2026-07-18.md) — 공유 스폰 포인트 + 스태거 페이드인 / Phase 2b 레벨 프리팹 시스템
- [2026-07-12.md](2026-07-12.md) — Phase 1 녹화·재생 코어 / Phase 2a 정체성(SO) + 압력판/문
- [2026-07-08.md](2026-07-08.md) — Phase 0 세팅 + 기본 이동·점프
