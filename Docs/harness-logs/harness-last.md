# 하네스 최종 평가 — Phase 3-2 벽타기형 정체성 (벽만)

## 스펙 (사용자 원본)
Phase 3-2: 벽타기형 정체성 구현 (벽만 — 천장 이동 제외). (1) IdentityData 벽타기 플래그 (2) PlayerController 벽 부착 상태 — 벽 방향 홀드+접촉 시 부착(중력 0), y축 상하 이동, 반대 입력/점프(벽 반대 방향 성분)/접촉 상실 시 이탈, 비클라이머 무회귀 (3) Identity_Climber 에셋 (4) 검증 레벨 Level_1_5 — 벽면 클론 발판 → 가벼운형 등반, 순서 강제 (5) 씬 _levels 등록 (6) 천장·모서리 전환 제외

## 동작 조건 (평가 결과)

- [✓] Identity_Climber.asset (canClimbWalls=1, canManipulate=0, jumpForce 10, weight 2, groundMask 256)
- [✓] Climbable 레이어(10) 등록, 기둥만 layer 10, 렛지 layer 8
- [✓] 부착 실측 (grav 3→0, 낙하 정지, vel=(0,0))
- [✓] y등반(vy=±6) / 이탈 3경로 후 grav=3 복원 실측
- [✓] 부착 중 확정→Backspace→재선택 후 grav==3 (잠복 버그 회귀 실측)
- [✓] 렛지 측면(L8) 부착 불가 — "검출은 됐다" 로깅 동반 (all마스크 히트 / climbable마스크 null)
- [✓] 비클라이머 무부착·기존 이동 그대로 (Light minVx=-7.00)
- [✓] 게이트 4종 절대좌표 실측: 솔로 ✗(-2.2) / 지면 스툴 ✗(-0.76) / 클라이머 단독 ✗ / 벽클론 경유 ✓ CLEARED
- [✓] 클론 벽면 고정 재생 + Light 접지·점프 클리어 실측
- [✓] 무회귀: 불변 라인 0-diff + Level_1_1~1_4 전이 무크래시
- [✓] 코어 7파일(RoundManager/Recorder/Playback/Actor/LevelManager/Box/Plate) diff 0
- [✓] _levels[4] 등록, 컴파일 0, 네이밍 준수
- [✗→감사] 스펙 "점프는 벽 반대 방향 성분 포함" — 체크리스트 누락(-5) + 실측 위반 (Major 1)

## 참조 패턴
- [데이터주도설계] #2 — 능력=데이터 플래그, Awake 필드 대입만, 마진 0.1 이하 금지
- [코어시스템] #1 — 위치 기록이라 신규 이동도 재생 코어 무수정 자동 성립
- [시스템조립] #3 — 게이트 절대좌표 양방향 실측, 보호 파일 0-diff 증명

## 점수

**총점 90/100 (PASS)** — 독립 Evaluator (Explore, fable, 스펙+diff+체크리스트만 입력)

| 섹션 | 배점 | 획득 |
|---|---|---|
| 코딩 컨벤션 | 12 | 12 |
| Unity 생명주기 | 9 | 9 |
| 성능 | 10 | 10 |
| 안전성 | 9 | 9 |
| 기능 충족 | 23 | 18 |
| 적대 검증 | 17 | 17 |
| 단순성 | 10 | 10 |
| 완결성 | 10 | 10 |
| 스펙 감사 | 감점 | -5 |

적대 시나리오 6종 실측 (되감기 인터럽트 / 확정→재선택 grav 오염 / 벽방향 홀드 점프 / 벽점프 수평 성분 / 렛지 마스크 우회 / 게이트 파훼) — 그중 2건이 동일 결함(Major 1) 발견.

## 피드백

- **[Major] 벽점프 "벽 반대 방향 성분" 실질 무효 — PlayerController.cs:110-116**: Update에서 세팅한 수평 속도를 다음 FixedUpdate 비부착 분기(:136)가 `_moveInput*_moveSpeed`로 덮어써 물리 스텝에 단 한 번도 반영 안 됨 (실측 x변위 0.00). 벽 방향 홀드 중 점프하면 다음 Update에서 즉시 재부착되어 점프 자체가 무효 (이탈 전이 0회). 레벨 게이트에는 무해하나 조작감 스펙 미달. **수정 제안: 벽점프 직후 재부착 락아웃(~0.15s) + 락아웃 동안 FixedUpdate 수평 덮어쓰기 유예**
- [Minor] 이탈 블립 중 잔류 상승 속도로 기둥 꼭대기 마운트 가능 (PlayerController.cs:167) — 현 레벨 무해, 향후 Climbable 지형 설계 시 유의
- [Minor] 대각 입력 주석이 키보드 전용 전제 (PlayerController.cs:159)
- [Minor] 기즈모가 transform.position 기준, 실판정은 rb.position (PlayerController.cs:243) — 에디터 시각화라 무해

## 수정 파일
- Assets/Scripts/Core/IdentityData.cs (+7)
- Assets/Scripts/Player/PlayerController.cs (+113/-1, 137→251)
- Assets/Prefabs/Player/Player.prefab (+5, _climbableLayer 1024 직렬화)
- Assets/Scenes/SampleScene.unity (+1, _levels[4])
- ProjectSettings/TagManager.asset (슬롯 10 Climbable)
- 신규: Assets/Data/Identities/Identity_Climber.asset, Assets/Prefabs/Level/Level_1_5.prefab (+.meta)

## 비고
- Generator가 계획 좌표의 자가모순(기둥탑 0.6 < Light 솔로 도달 0.83 치즈 / 기둥-렛지 9유닛 이격은 의도 해법도 불가)을 발견하고 지오메트리 수정: 기둥탑 1.1, 렛지 (0.5, top 3.0), Exit (0.5, 3.5). 4게이트 마진 >0.15 재성립.
- 알려진 대체해: "점프하는 클론 스툴" — 순서 강제 유지되므로 허용.
