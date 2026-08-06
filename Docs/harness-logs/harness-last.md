# 하네스 최신 결과 (2026-08-06) — 클리어 리플레이 후속 조정 3건 (시간 재흐름 + pip 제거 + 출구 흡입)

> 고위험형(Bind 공개 API 시그니처 변경 + RoundManager 접촉 → 독립 Critic 선행). 스냅샷 커밋 `3ff3008`.

## 스펙

클리어 리플레이 후속 조정 3건 (사용자 플레이테스트 피드백):
- ① 리플레이 중 HUD 시간 재흐름 — 남은 시간 = 녹화 상한(15s) 카운트다운, 경과 = 0부터 다시 증가(사용자 확정 2026-08-06), 종료 시 기존 표시(--.--/클리어 값) 복귀
- ② 상단 고조 pip(●○) 제거 — 스킵 힌트 유지, OnRingReached/타임라인/사전 스캔은 2차 음악용 유지
- ③ 출구 덮임 마무리 — 마지막 틱 후 히어로를 출구 콜라이더 중심으로 ≈0.35s SmoothStep 흡입 보간 → 2s 루프 대기, 매 루프 재발동, 스킵/Unbind 즉시 중단
- 제약: RoundManager 보호 블록 diff 0(MaxRecordSeconds 1줄 노출만), 클론 재생 결정성 불변

## 동작 조건 (평가 결과)

1. [✓] 리플레이 중 남은 = 상한−경과 / 경과 = 틱×fixedDeltaTime (실측 tick 264: hudT 09.72 = 15−05.28 정확)
2. [✓] 종료 시 --.-- / 클리어 값 복귀 (실측 hudElapsed 06.73 = rmElapsed 6.726)
3. [✓] 흡입/루프 대기 중 시간 정지 (실측 0.5s 간격 2회 동일, tick 336 고정)
4. [✓] pip 미표시 + 스킵 힌트 유지 (씬 EscalationText 3블록 삭제, 런타임 Find null)
5. [✓] 흡입 0.36s 소요, distToCenter 0.0000, 루프 재시작 시 distToSpawnFrame0 0.0000 (위치 오염 없음)
6. [✓] 2패스 흡입 재발동 (absEl=0.000 재캡처)
7. [✓] 스킵 시 _isAbsorbing=False + ResetUndoStack 1회 가드
8. [✓] 흡입 분기 early-return이 ApplyTick보다 앞 — frames 범위 밖 조회 차단 (ReplayDirector.cs:424-428)
9. [✓] 결정성 불변 (실측 tick 264 heroPos vs frame dist 0.0000)
10. [✓] RoundManager diff = MaxRecordSeconds 프로퍼티+주석 3줄 추가만
11. [✓] 컴파일 에러/워닝 0

## 참조 패턴

성공 패턴 #7(코어 밖 오버레이 모드)의 연장 조정 — 신규 기록 없음(연출 조정 성격).

## 점수

**94/100** (Major 1, Minor 3) — 기능 23/23, 스모크 17/17, 컨벤션 12/12, 나머지 만점. 감점은 스펙 감사 -5(아래 Major) + 95점 게이트 미충족 상한.

## 피드백

- [Major] **스펙 감사 누락** — Planner 체크리스트에 "OnRingReached/타임라인/사전 스캔 유지" 조건 미기재. **구현은 정상 유지 실측 확인**(ReplayDirector.cs:74 선언, :329-402 스캔, :442-448 소비 — 코드 결함 아님, 문서 결함). 차기 회차부터 스펙의 "유지" 요구도 조건화할 것
- [Minor] ReplayDirector.cs:479 — `_levelExit.GetComponent<Collider2D>()` 루프 패스당 1회 호출 (Bind 캐시 가능, 핫패스 아님)
- [Minor] ReplayDirector.cs:530-536 — 흡입 도중 스킵 시 히어로가 보간 중간 지점 동결 (스펙 "즉시 중단" 부합, Unbind로 정리 — 기록만)
- [Minor] ReplayDirector.cs:486-503 — 출구 인근에 압력판 있는 레벨이면 흡입 이동이 판을 스칠 수 있음 (현 레벨 구성 미발생, ResetGimmick이 복구)

## 수정 파일

- Assets/Scripts/Replay/ReplayDirector.cs (471→554줄) — Bind 시그니처 확장(+LevelExit), 시간 프로퍼티 2종, BeginAbsorb/DriveAbsorb 흡입 연출, pip 호출 제거
- Assets/Scripts/UI/ReplayHudUI.cs (71→43줄) — pip 로직 전면 제거(의도적 삭제, 게이트 diff 확인)
- Assets/Scripts/UI/PaperHudUI.cs (43→58줄) — ReplayDirector 참조 + 리플레이 중 시간 분기
- Assets/Scripts/Managers/LevelManager.cs — Bind 호출 1곳 인자 추가
- Assets/Scripts/Managers/RoundManager.cs — MaxRecordSeconds 노출(순수 추가)
- Assets/Scenes/SampleScene.unity — EscalationText 삭제 + PaperHUD._replayDirector 연결
