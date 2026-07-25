# HANDOFF — 현재 상태 스냅샷

> 이 파일은 **날짜 로그와 달리 항상 덮어쓴다.** 새 세션은 이 파일 하나로 "지금 어디인지"를 파악하고, 상세 이력은 날짜 파일을 참조한다.

**마지막 갱신**: 2026-07-25

## 현재 위치

- **배경 무드 슬라이스 적용 · 미커밋** (2026-07-25, 사용자 플레이테스트 대기) — SampleScene에 테이프 세계 배경: 카메라 `#101318` + Global Volume(Bloom/Vignette/FilmGrain, `Assets/Settings/TapeWorld_VolumeProfile.asset`) + Ground `#2A303B` + Backdrop 실루엣 19개(콜라이더 없음, sortingOrder -100). 레퍼런스 `Assets/Screenshots/tapeworld-mood-final.png`. MCP 도구 함정은 2026-07-25 로그 참조(m_Sprite/색상 객체 형식/volume 서브에셋)
- **아트 디렉션 확정 · 문서화 완료** (2026-07-25, 커밋 `5b18a9b`) — "테이프/방송 세계" 콘셉트, `Docs/team/ART-DIRECTION.md` 신규. 정체성 4색 팔레트 확정(Heavy 인디고/Climber 마젠타 퍼플/Carrier 앰버/Light 민트 시안, 명도 사다리 포함). **에셋 미적용** — Identity_*.asset 4색 반영은 Step 2 잔여(4색+클론 스캔라인 셰이더+REC 오버레이 — Volume은 완료)에 포함
- **Phase 3-3b 기믹 확장 완료 · 커밋 진행** (하네스 1회차 99/100, Major 0) — 깨지는 발판(CrumblingPlatform) + 정체성 제한 포탈(IdentityPortal, IdentityData[] 허용 리스트) 추가로 **기믹 8종 체제**(압력판/문 + 신규 7종). 캐리 오검출 Major 해소(지지체 3단 판정). Level_1_7 신설(붕괴 다리+FallZone 조합, Heavy 전용 렛지 포탈 게이트). RoundManager/LevelManager 0줄 수정 — 설치형 계약 확장성 실증
- **Phase 3-3 환경 기믹 5종 완료 · 커밋됨** (`460b769`, 72 → Refine 88/100) — 토글 스위치/시간제한 문/이동 발판/가시/낙하 구멍. IActivatable+ITickGimmick 설치형 구조, Assets/Prefabs/Gimmicks/ + Level_1_6(중첩 프리팹 인스턴스 첫 사례)
- **fable 잔여 폴더 삭제 완료** (2026-07-22) — `~/.claude/fable/` 제거, 백업만 유지
- **Phase 3-2 완료 · 커밋됨** (`191c90a` — 벽타기형 정체성 벽만, 하네스 v2 첫 실행 90/100). 코어 4종 전부 가동(무거운/가벼운/운반/벽타기 — 천장 제외)
- **벽점프 락아웃 수정 · 커밋됨** (`2300a0e`, 2026-07-19) — `PlayerController.cs` 재부착 락아웃 0.15s + 락아웃 중 수평 덮어쓰기 유예 + 접지 시 즉시 해제
- **접지 통일 · 커밋됨** (`b7e8f2e`, 2026-07-19) — 전 정체성 접지 마스크 3840(Ground|Clone|Climbable|Box), Box 레이어(11) 신설, PushableBox 레이어 이동(프리팹 + Level_1_4 내장 박스), 압력판 마스크 513→2561
- **클리어 Next 버튼 · 커밋됨** (`b3796d7`, 2026-07-19) — 화면 중앙 Next 버튼(Cleared 상태에서만 표시) → `LevelManager.LoadNextLevel()`. N키·Enter/NumpadEnter도 동작. 같은 커밋에 **[임시 디버그] PageDown/PageUp 강제 스테이지 이동** 포함(`#if DEVELOPMENT_BUILD || UNITY_EDITOR` 래핑 — **테스트 끝나면 제거 예정**)
- ⭐ 재미 검증 통과 (2026-07-18) → Phase 3 진행 중
- **Level_1_4/1_5 사용자 플레이테스트 대기** — 박스 밀기 감각 + 벽타기 조작감 + 벽점프 궤적 + 박스/벽/클론 위 점프 + 박스 압력판 회귀 체감

## 다음 작업 (우선순위 + 차단 관계)

1. **[사용자 몫] Level_1_4~1_7 플레이테스트** — 기존 대기(박스 밀기+벽타기+벽점프) + 기믹 8종 감각: 문 3초 타이밍, 수정된 캐리감, 깨지는 발판 1.5초(Heavy 이동속도로 다리 횡단 가능한지), 포탈 왕복 조작감, Heavy 렛지 게이트
2. **클리어 리플레이 연출(§9.3) + undo 스택 리셋** (Phase 3 잔여, 코어 루프 5단계 완성) — 기믹 C# 이벤트 노출(OnPressed/OnOpened/OnLandedOnClone/OnCleared) 포함. 재미 검증 통과로 선행 조건 충족
3. **천장 이동** (벽타기 확장) — Phase 3 마지막 잔여
4. **[비차단·임의 시점] 아트 Step 2 잔여** (하네스 1회) — Identity 에셋 4색 적용 + 클론 스캔라인 셰이더 + REC 오버레이 (Global Volume·배경 실루엣은 2026-07-25 완료). 상세는 `Docs/team/ART-DIRECTION.md` §6
5. **잔여 Minor**: `CharacterSelectUI.cs` 매 프레임 `SetActive`(Phase 5 병합) / 기믹 4곳+`PressurePlate.cs:113` 핫패스 GetComponentInParent / ToggleSwitch 바운스 이중 토글 가능성 / TimedDoor-Door 로직 중복 / 발판 top=지면 top 동일 높이 구간에선 탑승자가 지면에 인계됨(레벨 설계 시 유의) / 포탈 속도 보존 — 고속 낙하 진입 배치 유의 / [원인 미상 1회 관측] `_gimmicks=null` 상태 Rewind NRE(재현 실패, 기록만)

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
- **기믹 리셋 3경로** — Initialize/EnterSelecting/**RestartTake**(EnterSelecting 우회 경로라 별도 필수). 하나라도 빠지면 토글/타이머/위상이 잔존해 클론 재생 결정성 붕괴
- **순회 중 상태 전환 금지** — KillZone 사망은 `_isDeathPending` 플래그만, 처리는 DriveGimmicks 완료 후 단일 지점(재생/기록/틱 증가 전 return). 루프 안에서 RestartTake를 즉시 부르면 킬존 배열 위치 따라 1틱 desync
- **DriveGimmicks 위치 고정** — DriveBoxes 직후·클론 ApplyTick 이전. 뒤로 옮기면 라이브/클론 1틱 어긋남
- **기믹 검출 마스크 규약** — 스위치류·깨지는 발판 513(라이브+클론 — 클론이 이력을 재현해야 결정성 성립), KillZone·캐리·포탈 1(라이브만). 씬 상주 바닥은 top y=-2.5, x[-12,12] — 레벨 배치는 이 위 기준
- **쌍 장치 래치는 행위자가 건다** — 포탈 텔레포트 래치는 출발측이 도착측에 세팅. 도착측 자체 감지에 맡기면 구동 배열 순서에 따라 같은 틱 핑퐁. 래치 해제 질의는 마스크 1(513이면 클론이 영구 래치 유발)
- **상호작용 트리거는 Default 레이어** — Ground류에 두면 접지 질의(트리거도 잡는 오버로드)가 지면으로 오인 → 무한 점프. disabled 콜라이더 bounds는 무효(0 크기) — 꺼진 상태 질의 스킵

## 확정 사양 / 폐기한 접근

- **아트 디렉션 = "테이프/방송 세계"** (2026-07-25, 사용자 결정) — 후보 3안(테이프/청사진/손그림 흔들림) 중 A 확정. 베이스는 플랫 도형 유지, 차별화는 셰이더·연출·테마 레이어. 정체성 4색·시각 문법·가독성 상한은 `Docs/team/ART-DIRECTION.md`가 단일 기준. GDD의 "무거운=큰 네모" 임시에셋 항목은 폐기(코드 규칙 "색으로만 구분" 우선)
- **벽 부착은 Climbable 레이어(10) 전용** (2026-07-18) — Ground 벽 전체를 부착 대상으로 하면 지형이 사다리가 되어 게이트 붕괴(렛지 옆면 파훼). 부착 판정은 `_climbableLayer` 마스크로만. 천장 이동은 미구현(기믹 도입 후 확장)
- **부착 상태 규약** — 매 틱 재검증(자가 치유) + `Detach()`가 gravityScale 복원 단일 소유(이중 가드 필수, OnDisable에서도 호출). 이 규약을 깨면 "부착 중 확정→되감기→재선택"에서 중력 0 잔존 잠복 버그
- **벽점프 락아웃 규약** (2026-07-19) — 락아웃 카운터는 **부착 branch와 FixedUpdate 수평 덮어쓰기만** 차단한다. 이탈 경로는 절대 차단 금지(자가 치유 유지). 접지 시 카운터 즉시 0(락아웃 중 착지 시 입력 무시 방지)
- **fable 오케스트레이션 완전 철거** (2026-07-21 최종) — 기본 OFF 이후 실사용 없음 + OFF여도 게이트 훅이 호출당 ~350ms 소모 → 전역 설치 전체 제거. 백업 `~/.claude/_fable-teardown-backup-20260721/`. 잔여: `~/.claude/fable/hooks/orchestration-gate.py`(세션 훅 스냅샷 보호용) — **세션 재시작 후 폴더째 삭제 필요**. 하네스 모델 배분(Planner=메인 루프, Critic·Evaluator=fable, Generator=opus 강제)은 모델 이름 지정이라 무관하게 유지
- **Evaluator 반증 채점 체계** (2026-07-18) — 독립 에이전트 채점(자기 채점 편향 절단) + 스펙 감사 + 적대 시나리오 3개 실측 의무 + 만점 방지 조항. 이전 회차(99/100)와 점수 직접 비교 불가 — 하락이 정상

- **접지 전면 통일** (2026-07-19, 사용자 결정) — 점프는 어디서든(박스/벽/클론 위) 전부 가능. 전 정체성 `_groundMask` = **3840 (Ground|Clone|Climbable|Box)**. 무거운형 차별화는 점프 높이(JumpForce 10 vs 14)로만. 이전 사양 "가벼운형만 클론 밟고 점프"·"무거운형 클론 위 점프 불가"·"박스는 밀기 전용" **전부 폐기**
- **Box 레이어(11) 신설** (2026-07-19) — 박스는 Default가 아닌 Box 레이어. Default를 접지 마스크에 넣으면 라이브 자신(Default)을 발밑 체크가 잡아 무한 점프가 되므로 금지. 압력판 `_detectionMask`는 **2561 (Default|Clone|Box)** — 박스 감지 유지용 Box 비트 필수
- **박스 소유권 모델** — 미소유 박스는 운반형 라이브 라운드에만 Record(Dynamic), 그 외 Frozen(Kinematic). 확정 시 변위 있으면 OwnerSlot 귀속 → 이후 Replay(tick+1 재생). "운반형만 민다"는 물리 트릭이 아닌 `CanManipulateObjects` 데이터 플래그
- **클론은 닫힌 문을 통과한다** (확정 사양) — Kinematic MovePosition의 필연. 문 너머엔 Exit만 배치로 회피
- **무거운형은 클론 위에 서 있을 수 있으나 점프만 불가** — 접지 마스크에 Clone 없음. `excludeLayers`는 ResolveCrush와 모순되어 **폐기**
- **동적 캐릭터 생성 = 비활성 부모 트릭** — SetActive(false) 부모 아래 Instantiate로 Awake 지연 → InjectIdentity → SetActive(true). Critic 판정상 유일한 방법
- **레벨별 스폰** — 레벨 프리팹이 자기 SpawnPoint를 자식으로 포함, LevelManager가 로드 시 RoundManager에 주입
- **기믹은 설치형** (2026-07-22, 사용자 결정) — 독립 프리팹(Assets/Prefabs/Gimmicks/)을 레벨에 **중첩 프리팹 인스턴스**로 설치(MCP `create_child+source_prefab_path`, bake 복사 금지 — 재사용 계약). 인터페이스 2분할: IActivatable(수신)+ITickGimmick(구동·리셋). LevelDefinition 등록 불필요 — LevelManager가 GetComponentsInChildren로 자동 수집. 기존 레벨 1_1~1_5의 baked 압력판/문은 프로토타입으로 유지(전환 안 함)
- **기믹 상태는 기록하지 않는다** — 매 테이크 리셋 + 틱 환산 시간 + 라이브·클론 겸용 검출(513)이면 클론 재생이 활성화 이력을 자동 재현. 기존 레벨은 프로토타입, 본 맵은 별도 제작 예정

## 상세 이력

- [2026-07-25.md](2026-07-25.md) — 아트 디렉션 확정 "테이프/방송 세계" (문서화, 코드 변경 없음)
- [2026-07-23.md](2026-07-23.md) — Phase 3-3b 깨지는 발판 + 정체성 포탈 + 캐리 수정 (하네스 99)
- [2026-07-22.md](2026-07-22.md) — Phase 3-3 환경 기믹 5종 설치형 (하네스 72→Refine 88)
- [2026-07-18.md](2026-07-18.md) — 공유 스폰 포인트 + 스태거 페이드인 / Phase 2b 레벨 프리팹 시스템
- [2026-07-12.md](2026-07-12.md) — Phase 1 녹화·재생 코어 / Phase 2a 정체성(SO) + 압력판/문
- [2026-07-08.md](2026-07-08.md) — Phase 0 세팅 + 기본 이동·점프
