# 최종 통합 계획 (Generator 입력본)

> 2026-08-04 — 점프대(JumpPad) 기믹 하네스. 스냅샷 커밋: `14c40c6`

## 수정 대상
1. Assets/Scenes/SampleScene.unity (3226줄, MCP 프로퍼티 세팅만) — LevelManager `_levels` 배열(2793~2800줄)에 Level_1_8 1줄 append. 완료 후 git diff로 +1줄 외 변경 0 검증 필수 (씬에 미커밋 작업물 없음 — 스냅샷 커밋 완료 상태)
2. (코드 수정 없음 — RoundManager(815줄)/LevelManager(185줄)/PlayerController(264줄)/CharacterActor(230줄) 전부 diff 0 유지 대상. 설치형 계약이 신규 기믹을 자동 흡수)

## 신규 파일
1. Assets/Scripts/Level/JumpPad.cs — ITickGimmick 구현. 자체 Update/FixedUpdate 없음, DriveGimmickTick 구동. 검출부는 CrumblingPlatform.IsOccupied(116~140줄) 패턴 복제하되 **마스크 1(라이브 Default 전용)**: 상면 위 얇은 OverlapBox(ContactFilter2D useTriggers=false, 재사용 List) → GetComponentInParent&lt;CharacterActor&gt; → 발바닥 `bounds.min.y ≥ top − _standTolerance` && `vy ≤ ε`(하강/정지)이면 발사. vy 읽기는 `other.attachedRigidbody`(핫패스 GetComponent 금지). 발사 = `rb.linearVelocity = new Vector2(rb.linearVelocity.x, launchSpeed)` (y만 세팅). `launchSpeed = Mathf.Max(_minLaunchSpeed, _baseLaunchSpeed − (Weight−1) × _weightPenaltyPerUnit)`, Identity null 폴백 weight 1. 발사 시 스프라이트 플래시(틱 카운트다운, `Mathf.CeilToInt(초/fixedDeltaTime)` 환산, Time.time 금지). ResetGimmick = 플래시/색 복원.
2. Assets/Prefabs/Gimmicks/JumpPad.prefab — Ground 레이어(8), 비트리거 BoxCollider2D(접지 마스크 3840에 걸려 착지 가능), SpriteRenderer, JumpPad.cs 부착. CrumblingPlatform.prefab 구조 참고. 검출용 별도 트리거 콜라이더 추가 금지(OverlapBox 질의만).
3. Assets/Prefabs/Level/Level_1_8.prefab — 검증용 최소 레벨. Ground는 Level_1_7 패턴 복제(y=-8.55, 스케일 40.53×1.68, 레이어 8 → top −7.71). LevelDefinition 6필드 전부 채움: `_levelName`/`_identities`(Light+Heavy — 차등 검증용)/`_bannedIdentities`/`_spawnPoint`/`_levelExit`/`_boxes`(빈 배열 허용 — Initialize null 안전). JumpPad는 **create_child + source_prefab_path 중첩 설치**(bake 복사 금지), 완료 후 `.prefab`에서 `m_SourcePrefab` grep 증명.

## 구현 순서
1단계: JumpPad.cs 작성 → read_console 컴파일 확인
   ⚠ 주석 3종 필수: ① 마스크 1 고정 사유(클론은 기록 궤적이 발사를 재현 — 513 변경 금지) ② vy≤ε는 라이브 전용이라 "속도 판정 금지" 제약(클론 vy 항상 0) 미적용 ③ 패드 위 정지 시 매 틱 재발사(트램펄린)는 의도된 동작
2단계: JumpPad.prefab 생성 (MCP)
3단계: Level_1_8.prefab 생성 + JumpPad 중첩 설치 (MCP)
   ⚠ 기믹 바닥 매몰 재발 방지(과거 72점 원인): JumpPad 상면이 바닥 top −7.71 위에 오도록 좌표를 산술로 먼저 확정
   ⚠ Climbable 벽 배치 금지: 벽부착 branch(PlayerController.cs:141)가 y를 덮어써 발사 소멸 — JumpPad 인접에 Climbable 두지 않는다
   ⚠ 게이트 산술(다른 값 쓰면 재검산): 발사고 = launchSpeed²/58.86, 점프고 = jumpForce²/58.86. 기본값 base 18 / penalty 0.8 / min 5 → 발사고 Light 5.50 / Climber 5.03 / Carrier 4.57 / Heavy 1.98. 렛지 = 패드 top +4.0 → ① Light 점프고 3.33+0.3 초과 ✓ ② Light 발사고 −0.5 이하(이산 손실 방향) ✓ ③ Heavy 발사고 +0.3 초과 ✓
4단계: SampleScene `_levels` 등록 (MCP 프로퍼티 세팅만) → git diff로 1줄 삽입 외 변경 0 확인
5단계: 플레이 모드 실측 — ① 발사 틱 전후 `_tick` vs 클론 재생 위치 일치(재시작 2회 반복 동일성) ② Heavy가 렛지에 발사대 위에서도 못 닿음을 절대 좌표(bounds.min.y 최댓값)로 양방향 실측 ③ 기존 레벨 회귀 = Level_1_6/1_7 로드 후 기믹 구동+콘솔 0 ④ 측정은 점프 입력 없이(코요테 상쇄 회피) ⑤ `git status`로 신규 3파일+씬 1줄 외 변경 0 증명

## 참조처 맵
- ITickGimmick 구현체(신규 JumpPad가 7번째, 인터페이스 무수정 — 기존 영향 0) → CrumblingPlatform.cs:23, IdentityPortal.cs:24, KillZone.cs:25, MovingPlatform.cs:22, TimedDoor.cs:19, ToggleSwitch.cs:22
- ITickGimmick 구동/리셋 경로 → 수집 LevelManager.cs:125 → 주입 RoundManager.cs:153 / 리셋 3경로 = Initialize(174~176)·RestartTake(626~629)·Rewind/EnterSelecting(744~746) / 구동 285 → DriveGimmicks(776~780). **JumpPad는 ResetGimmick만 구현하면 3경로 전부 자동 커버**
- CharacterActor.Identity(:58) → RoundManager.cs:510/592/792, IdentityPortal.cs:71, PressurePlate.cs:126 — JumpPad는 읽기만 추가, 기존 영향 0
- LevelDefinition 직렬화 필드(:13~28) → `_levelName`/`_identities`/`_bannedIdentities`/`_spawnPoint`/`_levelExit`/`_boxes`
- SampleScene `_levels` → SampleScene.unity:2793~2800 (현재 7개, Level_1_8이 8번째)
- Identity 실측: Light jumpForce 14·W1(점프고 3.33) / Carrier 12·W3(2.45) / Climber 10·W2(1.70) / Heavy 10·W10(1.70) — Light 상한 14.96 준수

## 과거 실패 패턴
- 기믹 바닥 매몰(Phase 3-3 1회차 72점) — 배치 좌표 산술 선행으로 방지
- 도달고 공식 과신(공식 대비 실측 −0.1~−0.15) — 마진 방향 규칙: 닿아야 하는 게이트 = 공식 −0.5 이하, 못 닿아야 하는 게이트 = 공식 +0.3 이상
- 속도 세팅 두 곳 경합(Phase 3-2 Major) — 이번엔 PlayerController.cs:150이 y 보존이라 발사 속도 생존 판정(Critic 확인). 벽부착 branch만 예외 → Climbable 배치 금지로 회피
- 트리거 레이어 오배치 무한 점프 — 별도 트리거 콜라이더 금지, OverlapBox 질의만

## 참조 패턴
- [카테고리: 시스템조립] #5 설치형 틱 기믹 시스템(88점, 반복 2회) — 반영한 핵심 접근: 인터페이스 2분할 계약 + GetComponentsInChildren 자동 수집 드롭인(매니저 0줄), 리셋 3경로 자동 커버, 중첩 프리팹 인스턴스 설치(m_SourcePrefab 증명), 씬 지오메트리 실측 선행, 도달고 산술 게이트 증명
- [카테고리: 데이터주도설계] #2 정체성+압력판(97점) — 반영한 핵심 접근: Weight는 순수 데이터(mass 금지), Kinematic 혼입 검출은 기하 판정(라이브 전용 vy 조건은 제약 범위 밖임을 Critic 판정), ContactFilter2D 3중 필터 + 재사용 List 할당 0

## 동작 조건 (Evaluator 체크리스트)
- [ ] JumpPad.cs가 ITickGimmick 구현, 자체 Update/FixedUpdate 없음 — DriveGimmickTick으로만 구동
- [ ] 라이브가 위에서 밟으면(발바닥 상면 tolerance 내 && vy ≤ ε) 그 틱에 `linearVelocity.y = launchSpeed`로 수직 발사
- [ ] 발사 속도 = max(min, base − (Weight−1)×penalty) — Light(W1) > Climber(W2) > Carrier(W3) > Heavy(W10) 순 도달고 차등이 플레이 모드 실측으로 확인됨
- [ ] base/penalty/min 배율 필드가 JumpPad 인스펙터에 노출됨
- [ ] 클론(Clone 레이어 9, Kinematic)은 발사 질의 대상이 아님(마스크 1) — 클론 재생 시 기록된 발사 궤적이 라이브와 동일하게 재현됨(실측)
- [ ] 재시작(RestartTake/EnterSelecting/Initialize) 시 ResetGimmick 경유로 동일 입력 → 동일 발사 틱·동일 궤적 재현(2회 반복 실측)
- [ ] JumpPad.prefab: Ground 레이어(8) + 비트리거 BoxCollider2D — 위에 착지 가능
- [ ] Level_1_8.prefab에 JumpPad가 중첩 프리팹 인스턴스(m_SourcePrefab)로 설치, LevelManager 자동 수집으로 매니저 코드 0줄 동작
- [ ] Level_1_8 게이트: 렛지가 Light 점프고 초과 + Light 발사고 이내 + Heavy 발사고 초과 (절대 좌표 양방향 실측)
- [ ] 기존 레벨 1_1~1_7 프리팹 + RoundManager/LevelManager/PlayerController/CharacterActor diff 0, Level_1_6/1_7 플레이 회귀 없음(콘솔 0)
- [ ] 컴파일 에러/워닝 0
