# 하네스 최신 결과 (2026-08-06) — 클리어 리플레이 연출 1차분 (협력 고리 + 리플레이 모드)

## 스펙

클리어 리플레이 연출 1차분 — 협력 고리 감지 + 리플레이 모드 (GDD §9.3, 음악 스템은 2차로 제외)

- 기믹 C# 이벤트 노출: OnPressed / OnOpened / OnLandedOnClone / OnCleared (기존 기믹 수정 — 감지 지점은 이미 구현된 상태 변화 지점)
- 클리어 시 리플레이 모드 자동 진입: 전 클론 + 라이브 궤적을 처음부터 동시 재생
- 리플레이 시작 전 확정 궤적을 스캔해 협력 고리 타임라인을 사전 생성 (고리 N개 = 고조 N단계)
- 리플레이 중 고리가 걸리는 순간 시각 강조 연출 (단계 카운트 표시 + 화면 펄스 등 — 미니멀)
- 리플레이 스킵: 아무 키/클릭 시 즉시 종료 → 클리어 흐름(Next). "아무 키나 눌러 건너뛰기" 힌트
- 리플레이 종료(완주/스킵 모두) 후 undo 스택 리셋 + 기존 클리어 흐름(Next) 복귀

동작 조건(원문): 1.자동 재생 2.틱 단위 일치 3.타임라인 동일 틱 발화 4.고조 누적 표시 5.아무 입력 즉시 스킵+후처리 동일 6.다음 레벨·재도전 정상 7.회귀 없음

## 동작 조건 (평가 결과)

- [✓] 클리어 시 _startDelay(0.6s) 후 자동 리플레이 진입 — 전 클론 + ex-live 동시 재생 (ReplayDirector.cs:106-129, 143-161)
- [✓] 리플레이 궤적 틱 단위 일치 — 실측 replayTick=30에서 rb.position vs recording frame[30] **diff 0.000000**
- [✓] 타임라인 Begin 시점 확정 — 실측 N=5 `[0:PlatePressed, 12:PlatePressed, 12:DoorOpened, 445:LandedOnClone, 445:Cleared]` 4종 전부 포함
- [✓] 고조 발화 정합 — replayTick=30에서 firedChainCount=3 == 틱≤30 이벤트 수, chainText "고리 3 / 5" 누적
- [✓] 스킵 힌트 표시/숨김 실측
- [✓] 아무 키/클릭 즉시 스킵 + 같은 프레임 LoadNextLevel 미실행 (BlocksClearedInput + _endedFrame 이중 안전, 실측 levelIndex 불변)
- [✓] 종료 후 undo 스택 리셋(ConfirmedCount 1→0 실측) + Next 재표시 + N/Enter 정상(levelIndex 0→1)
- [✓] 리플레이 중(딜레이 포함) Next 숨김·차단 (실측 pending 구간 nextButtonActive=False)
- [✓] ESC/PageDown 이탈 잔재 0 (CleanupCurrentLevel 최우선 StopImmediate, 재로드 2회 에러 0)
- [✓] HUD 클론 카운트 Cleared 홀드 (실측 "1 / 2" 유지)
- [✓] 회귀 없음 — RoundManager 보호 블록 diff 0(순수 추가 65줄), 세션 전체 콘솔 에러 0
- [✗→실측 보완] 스펙 조건 6 "재도전"이 체크리스트에 명시 항목으로 누락 [-5 스펙 감사] — 단 Evaluator가 실측으로 별도 확인: 같은 레벨 재로드 2회·재라운드·재클리어·재리플레이 전부 정상

## 참조 패턴

- [시스템조립] #7 엔딩 오버레이 모드 — 코어 enum 무추가, Cleared 휴면 가드 위 독립 시퀀스, State 독자 전수 조사, 복사본 절연, 단일 Stop
- [코어시스템] #1 녹화·재생 코어 — 틱 단일 소유(자기 _replayTick), tick+1 규약 ApplyTick 재사용, 구동 순서 복제(박스→기믹→클론)

## 점수

**91 / 100** (Major 0, 1회차 통과)

| 섹션 | 점수 |
|---|---|
| 코딩 컨벤션 | 10/12 |
| Unity 생명주기 | 8/9 |
| 성능 | 10/10 |
| 안전성 | 9/9 |
| 기능 충족 | 23/23 |
| 스모크 실측 | 17/17 |
| 단순성 | 9/10 |
| 완결성 | 10/10 |
| 스펙 감사 | -5 |

## 피드백

### Major
- 없음

### Minor
1. ReplayDirector.cs:70 `BlocksClearedInput` — is/has/can 접두사 규정 위반 (`ShouldBlockClearedInput` 권장) [-1]
2. PlayerController.cs:57 `_wasGrounded` — 접두사 불일치(관용적) [-1]
3. ReplayFlourishUI.cs — OnDisable/OnDestroy 부재: 직접 비활성화 시 펄스 알파/텍스트 스케일 잔재 가능. OnDisable에서 HideAll 권장 [-1]
4. ReplayDirector.cs:284-302 — 스킵 시 보드가 리플레이 중간 상태로 동결: 스킵 시 마지막 틱 1회 적용(ApplyTick(_replayLength-1))하면 완주와 최종 화면 일치
5. SkipHint 배치가 하단 중앙 — 스펙 문구는 "화면 구석" (기능 무해)
6. ReplayDirector.cs:37, 274-278 — _skipGrace 옵션 + Alt/Tab/Meta 제외는 스펙 외 방어 분기(취지 타당) [-1]
7. 체크리스트에 스펙 조건 6 "재도전" 명시 누락 [스펙 감사 -5 원인 — 실동작은 정상]

## 수정 파일

- Assets/Scripts/Managers/RoundManager.cs (+65, 보호 블록 0 diff)
- Assets/Scripts/Managers/LevelManager.cs (+27)
- Assets/Scripts/Level/PressurePlate.cs (+7) / Door.cs (+7) / TimedDoor.cs (+10) / LevelExit.cs (+7)
- Assets/Scripts/Player/PlayerController.cs (+26)
- Assets/Scripts/UI/CharacterSelectUI.cs (+12/-2) / PaperHudUI.cs (+4/-1)
- Assets/Scripts/Replay/ChainTimelineTracker.cs (신규) / ReplayDirector.cs (신규) / ReplayFlourishUI.cs (신규)
- Assets/Scenes/SampleScene.unity (+395 — ReplayDirector/ChainTimelineTracker 오브젝트 + Canvas 리플레이 UI 3종, FadeOverlay 아래 sibling 7)
