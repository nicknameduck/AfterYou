# 하네스 최종 평가 — Phase 3-1 운반형 정체성 + 물체 밀기

## 스펙 (사용자 원본)
Phase 3-1: 운반형 정체성 + 물체 밀기 규칙 구현. (1) IdentityData 물체 조작 플래그 (2) PushableBox — 운반형만 밀기, 명시적 판정 차단 (3) 박스 궤적 기록·재생 Kinematic 100% 일치 (4) RoundManager 라운드/전체 리셋 복원 (5) PressurePlate 박스 Weight 합산 (6) Identity_Carrier 에셋 + 박스 프리팹 (7) 검증 레벨 Level_1_4 (8) 들기·전달 제외

## 점수

**총점 99/100 (PASS)**

| 섹션 | 배점 | 획득 |
|---|---|---|
| 코딩 컨벤션 | 18 | 18 |
| Unity 생명주기 | 13 | 13 |
| 성능 | 13 | 12 |
| 안전성 | 13 | 13 |
| 기능 충족 | 23 | 23 |
| 단순성 | 10 | 10 |
| 완결성 | 10 | 10 |

감점: 성능 -1 (PressurePlate.MeasureWeightOnPlate가 FixedUpdate 핫패스에서 겹친 콜라이더마다 `GetComponentInParent<PushableBox>()` 호출 추가 — 기존 `GetComponentInParent<CharacterActor>()` 패턴과 동일한 관용이나 실측상 매 물리 스텝 부담. 무해).

## 동작 조건 (✓/✗ + 근거)

- ✓ **Identity_Carrier.asset 존재, _canManipulateObjects=1** — asset:23 `_canManipulateObjects: 1`, _weight:3
- ✓ **PushableBox.prefab: Dynamic·mass 1·freeze rotation Z·layer Default, _weight≥요구무게** — prefab: m_BodyType 0(Dynamic), m_Mass 1, m_Constraints 4(FreezeRotation), m_Layer 0(Default), _weight 10 ≥ 요구 10. 런타임 실측: 로드 후 Kinematic, mass=1, constraints=FreezeRotation, gravityScale=3
- ✓ **런타임 rb.mass==1 + 전역 `.mass =` 없음** — 실측 rb.mass=1, grep `\.mass\s*=` in Assets/Scripts → 0 matches
- ✓ **Level_1_4.prefab 배선 완비 + 씬 _levels 등록** — _identities=[Carrier(guid f4343637…), Light(guid 1fc8cf4b…)], _spawnPoint→SpawnPoint, _levelExit→Exit(fileID …064), _boxes=[PushableBox(fileID …025)]. PressurePlate _door→Door(fileID …054), _detectionMask=513, _requiredWeight=10, _renderer 연결. SampleScene _levels[3]=Level_1_4(guid a7790e2d…), diff +1줄
- ✓ **로드 직후 박스 Frozen·Kinematic·미낙하, 문 닫힘** — 실측: box.Mode=Frozen, bodyType=Kinematic, pos=(-2.00,-2.00)=base(미낙하), plate.IsPressed=False
- ✓ **운반형 박스 밀기→Dynamic→판 판정 통과→문 개방, 확정 시 OwnerSlot 기록** — 실측: SelectCharacter(0=Carrier)→box.Mode=Record/bodyType=Dynamic. 박스 판 위 이동 후 MeasureWeightOnPlate()=10 ≥ 10. Door.SetOpen(true)→collider.enabled False(개방)/SetOpen(false)→True(폐쇄). CommitRecording 코드: RoundManager:468-479 (HasDisplacement>ε일 때만 OwnerSlot 승격)
- ✓ **가벼운형 라운드 클론+박스 tick+1 동기 재생** — PushableBox.ApplyBoxTick:107 `Mathf.Clamp(tick+1,0,FrameCount-1)` == ClonePlayback.ApplyTick:96 `Mathf.Clamp(tick+1,0,FrameCount-1)` 완전 일치. DriveBoxes(RoundManager:604)는 틱 증가 이전 호출로 클론과 동일 _tick 공유
- ✓ **순서 강제: 비운반형 박스 밀기 불가(Frozen 유지)** — 실측: SelectCharacter(1=Light)→box.Mode=Frozen/bodyType=Kinematic. Kinematic 박스는 Dynamic 라이브가 밀 수 없음. PrepareBoxesForRound(RoundManager:625) canManipulate=false→Frozen. 가벼운형 선행 시 판 무게 미달(1<10)+박스 미이동→문 폐쇄→클리어 불가
- ✓ **리셋: Rewind 시 박스 base 복귀·소유 해제, 재로드 초기 위치** — 실측: Rewind 후 box.Mode=Frozen/Kinematic/OwnerSlot=-1/pos=(-2,-2). Rewind(RoundManager:534-538) OwnerSlot==slot→ClearOwnership, EnterSelecting(590-594) 전 박스 Frozen+ResetToBase. Initialize(124-125) ClearOwnership 루프
- ✓ **무회귀: Level_1_1~1_3 정상 (박스 0개 null-안전)** — 실측(클린 프레임): _boxes.Length = idx0:0, idx1:0, idx2:0, idx3:1. Level_1_1 SelectCharacter(0)→Recording, Rewind→Selecting 무크래시. `_boxes ?? Array.Empty` (Initialize:116), 빈 배열 루프 안전
- ✓ **RoundManager 보호 블록 본문 0-diff** — git diff: 변경은 using(+1)·_boxes 필드·BoxDisplacementEpsilon const·Initialize 시그니처(+boxes)·ClearOwnership 루프·Teardown _boxes=null·DriveBoxes 훅 호출·PrepareBoxesForRound 훅 호출·ConfirmClone 커밋 블록·Rewind 해제 블록·EnterSelecting 리셋 블록·신규 2메서드뿐. 중앙 틱 4단계/ResolveCrush/Push/RestoreAllSpawnOverlaps/IgnoreCollision 3개소 본문 diff 0
- ✓ **컴파일 0·Obsolete 없음·UTF-8 BOM·네이밍** — read_console error/warning 0(플레이 전후). Obsolete grep 0. PushableBox.cs BOM=EF BB BF. 네이밍: _camelCase, can 접두사(_canManipulateObjects), == null/!= null 사용, is null 미사용

## MCP 실측 인용 (수치)
- read_console: error/warning 0건 (플레이 전, 후 모두)
- 로드 직후: box.Mode=Frozen, rb.bodyType=Kinematic, rb.mass=1, rb.constraints=FreezeRotation, rb.gravityScale=3, box.Weight=10, box.OwnerSlot=-1, box.pos=(-2.00,-2.00)
- plate._requiredWeight=10, plate.IsPressed=False, MeasureWeightOnPlate@rest=0
- 박스 판 위(-6,-2.00, boxBottom=-2.5=plateTop): MeasureWeightOnPlate=10
- Door: SetOpen(true)→collider.enabled=False, SetOpen(false)→collider.enabled=True
- Carrier(0) 선택: box.Mode=Record/Dynamic. Rewind 후: Frozen/Kinematic/OwnerSlot=-1/(-2,-2). Light(1) 선택: box.Mode=Frozen/Kinematic
- RoundManager._boxes.Length: Level idx 0=0, 1=0, 2=0, 3=1

## 피드백

### Major
- 없음

### Minor
1. **[성능] PressurePlate.cs:113** — `MeasureWeightOnPlate`가 매 FixedUpdate, 겹친 콜라이더마다 `GetComponentInParent<PushableBox>()` + `GetComponentInParent<CharacterActor>()` 2회 트래버스. 레이어(박스=Default 0, 클론=Clone 9)로 먼저 분기하면 컴포넌트 조회를 줄일 수 있다. 겹침 집합이 작아 실사용 영향은 미미. (감점 -1)
2. **[관용] PushableBox.cs:95** — `Debug.Assert(tick == _recording.FrameCount)`는 CharacterRecorder.cs:48과 동일한 미래핑 패턴. UNITY_ASSERTIONS로 자동 스트립되므로 무해. (무감점)

## 참조 패턴
- [코어시스템] #1 — rb.position 기록·tick+1 규약·속도0→bodyType·중앙 틱 단일소유를 **박스라는 2번째 오브젝트 클래스로 확장**(Frozen/Record/Replay 모드, DriveBoxes, CaptureBoxTick/ApplyBoxTick)
- [데이터주도설계] #2 — Weight/mass 분리·데이터 플래그(CanManipulateObjects)·기하 판정을 박스에 재사용
- [시스템조립] #3 — _boxes 런타임 주입·null-안전·보호 코어루프 0-diff

## 수정 파일
- 수정: IdentityData.cs(+7), PressurePlate.cs(+10), LevelDefinition.cs(+4), LevelManager.cs(1/1), RoundManager.cs(+101/-1), SampleScene.unity(+1)
- 신규: PushableBox.cs, Identity_Carrier.asset, PushableBox.prefab, Level_1_4.prefab (+각 .meta)
