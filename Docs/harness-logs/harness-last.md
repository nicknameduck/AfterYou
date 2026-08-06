# 하네스 최신 결과 (2026-08-06) — 클리어 리플레이 연출 1차분 (협력 고리 타임라인 + 리플레이 모드)

> 하네스 v3 첫 실측. 고위험형(독립 Critic 선행). 이전 회차(구 체계 91/100, 커밋 `4254236`)는 롤백 후 참조 없이 재구현.

## 스펙

GDD §9.3 클리어 리플레이 1차분 (음악 스텝 2차 제외):
- 기믹 C# 이벤트 노출 4종: OnPressed(PressurePlate) / OnOpened(Door) / OnLandedOnClone(PlayerController) / OnCleared(LevelExit) — 이미 구현된 상태 변화 지점에서 발화
- 클리어 시 리플레이 자동 진입: 전 클론 + 라이브(히어로) 궤적을 처음부터 동시 재생
- 히어로는 원본 룩(원본 머티리얼·알파 1), 클론은 스캔라인 유지
- 탈출 완료 2초 뒤 반복 재생 / 아무 키·클릭 스킵 → Next 흐름 / "아무 키나 눌러 건너뛰기" 힌트 / Next 버튼 유지
- 종료(완주/스킵 공통) 후 undo 스택 리셋 + 기존 클리어 흐름 복귀

## 동작 조건 (Evaluator 판정)

1. ✓ 클리어 시 리플레이 자동 시작, 전원 frame0 동시 재생 (ReplayDirector.cs:184-185, 258-259 — 실측 isPlaying=True)
2. ✓ 궤적 틱 일치 — 실측 tick 158에서 클론/히어로 rb.position vs 기록 err=0.00000 (기준 0.001)
3. ✓ 사전 스캔 타임라인 == 실발화 틱 — 실측 PlatePressed@4·DoorOpened@4, OnPressed/OnOpened 실발화가 OnRingReached(tick4)와 동일 fixedTime(2주기 재현), Cleared@524 발행
4. ✓ 고조 단계 누적 — 실측 ●●○(2/3)→3/3, 총 pip=타임라인 수
5. ✓ 스킵 후처리 완주 동일 — 실측 ConfirmedCount=0·HUD 숨김·Cleared 유지·같은 프레임 IsReplaying=True(Enter/N 누수 차단)
6. ✓ Next/ESC/재시작 정상 — 실측 Level_1_2 로드·새 라운드 Recording·재바인드 정상
7. ✓ 회귀 0 — RoundManager 보호 블록 diff 0(순수 추가 16줄), 기믹은 이벤트 추가만, 세션 콘솔 에러/경고 0
8. ✓ 스킵 힌트 표시/숨김 — 실측 우하단 앵커(1,0), 리플레이 중 True·종료 후 False
9. ✓ Next 버튼 리플레이 중 유지 — 실측 active=True
10. ✓ 히어로 원본 룩 — 실측 Sprite-Lit-Default·α1.00 / 클론 CloneGhost·0.85
11. ✓ 2초 후 반복 — 실측 2주기 관찰(틱 재순환·링 재발화·stage 재적립)
12. ✓ 라이브 플레이 중 이벤트 발화 — 상태 전이 지점 발화(PressurePlate.cs:137-138, Door.cs:69-70, LevelExit.cs:47-50, PlayerController.cs:123-126)

## 참조 패턴

성공 패턴 #7(코어 밖 오버레이 모드 — 엔딩) 재사용: RoundManager 휴면(Cleared) 위에 ReplayDirector가 중앙 틱 대행 구동. RoundManager 수정은 ResetUndoStack 1개(보호 블록 diff 0).

## 점수

**97 / 100** (Major 0, Minor 5) — 컨벤션 11/12, 생명주기 9/9, 성능 9/10, 안전성 9/9, 기능 23/23, 스모크 17/17, 단순성 10/10, 완결성 9/10

## 피드백

Major: 없음.

Minor:
1. LandedOnClone 감지 기준 이중화 — 라이브는 "착지 엣지 ∧ 발밑 클론"(PlayerController.cs:123-126), 스캔은 "발밑 클론 엣지만"(ReplayDirector.cs:359-364). 클론 위로 걸어 올라간 경우 스캔만 고리로 계수 → 기준 통일 검토
2. 압력판 수집 경로 불일치 — ReplayDirector.cs:138 FindObjectsByType 씬 전역 검색 (다른 기믹은 Bind 주입 — LevelManager.cs:205). Bind 인자로 통일 권장
3. 스펙 무관 씬 변경 혼입 — SampleScene.unity 기존 텍스트 m_MaxSize 2건(BestFit=0이라 실효 없음, diff 위생)
4. Next 버튼 클릭은 IsReplaying 가드 밖(CharacterSelectUI.cs:62 직행) — 리플레이 중 클릭 시 스킵+즉시 전환 동시(의도로 볼 수 있어 통과)
5. 판 2개 이상 + 재눌림 시나리오 미실측(로직상 문제 없음, 단일 판 1회 눌림만 실측)

## 수정 파일

- Assets/Scripts/Level/PressurePlate.cs — OnPressed 이벤트 + EvaluatePressed() + LinkedDoor 프로퍼티(스캔용 접근 경로, Generator 편차 보고 후 수용)
- Assets/Scripts/Level/Door.cs — OnOpened 이벤트
- Assets/Scripts/Player/PlayerController.cs — OnLandedOnClone 이벤트 + IsCloneUnderfoot()(자기 제외)
- Assets/Scripts/Level/LevelExit.cs — OnCleared 이벤트(전이 이전 발화, Cleared 재발화 가드)
- Assets/Scripts/Managers/RoundManager.cs — ResetUndoStack() 1개(Cleared 가드, 보호 블록 diff 0)
- Assets/Scripts/Managers/LevelManager.cs — ReplayDirector Bind/Unbind 배선 + IsReplaying 입력 누수 가드
- Assets/Scripts/Level/PushableBox.cs — TryGetRecordedPosition()
- Assets/Scripts/Clone/ClonePlayback.cs — _displayAlpha + SetDisplayAlpha()
- Assets/Scripts/Clone/CharacterActor.cs — ApplyReplayOriginalLook()
- Assets/Scripts/Replay/ReplayDirector.cs — 신규(사전 스캔 + 리플레이 구동 + 스킵/루프/후처리)
- Assets/Scripts/UI/ReplayHudUI.cs — 신규(스킵 힌트 + 고조 pip)
- Assets/Scenes/SampleScene.unity — ReplayDirector GO + ReplayHud UI + 참조 연결
