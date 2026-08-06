# 최종 통합 계획 — 클리어 리플레이 후속 조정 3건 (시간 재흐름 + pip 제거 + 출구 흡입)

> 2026-08-06 23:04 시작. 고위험형(Bind 공개 API 시그니처 변경 + RoundManager 접촉) — 독립 Critic 검증 완료 머지본. 스냅샷 커밋 `3ff3008`. 워크트리 OFF.

## 수정 대상 (전부 Edit 전용)

1. `Assets/Scripts/Replay/ReplayDirector.cs` (472줄)
   - **Bind 시그니처 확장**: `Bind(rm, actors, boxes, gimmicks, LevelExit levelExit)` (:126-147) — levelExit null 허용(LevelDefinition.LevelExit이 null일 수 있음), `rm.MaxRecordSeconds`도 여기서 캐시. **Unbind(:153-171)에서 LevelExit 캐시 null 처리 추가**(파괴된 프리팹 참조 위생 규약)
   - **시간 프로퍼티**: `ReplayElapsedSeconds => _replayTick * Time.fixedDeltaTime` / `ReplayRemainingSeconds => Mathf.Max(0f, 캐시상한 - ReplayElapsedSeconds)` (:118-121 부근). 흡입/루프 대기 중엔 _replayTick 고정 → 시간 정지(클리어 순간 정지하는 기존 ElapsedSeconds 규약과 동형 — Critic 판정 "자연스러움")
   - **출구 흡입 연출**: 완주 블록(:411-417) 재구성 — RunPostProcessOnce()는 기존 위치(흡입 시작 시점) 유지 → BeginAbsorb(시작 위치 캡처 + `_absorbElapsed = 0` 리셋 — **매 루프 패스마다 재발동하므로 리셋 누락 금지**, 목표 = LevelExit `Collider2D.bounds.center` 런타임 조회, exit/hero null이면 기존 루프 대기로 폴백). ⚠ **[Critic 필수] `_isAbsorbing` 분기는 FixedUpdate의 `_isWaitingLoop` 분기(:379-384)와 나란히, 박스/기믹/캐스트 구동 루프(:388-395)보다 앞 early-return** — 아니면 ApplyTick(_totalTicks)가 frames 범위 밖 조회. 흡입 진행은 `_absorbElapsed += Time.fixedDeltaTime` 누적(루프 대기의 realtimeSinceStartup과 소스 혼용 금지) + SmoothStep + `hero.SetPosition`(rb+transform 동시 규약 기존 API). `_absorbDuration = 0.35f` SerializeField. 종료 시 목표에 정확 스냅 후 `_isWaitingLoop = true; _loopResumeTime = realtime + 2s`(기존 :415-416 형태). StopReplay(:448)에 `_isAbsorbing = false` 추가
   - **pip 제거**: :280 `_hud.Show(_timeline.Count)` → `_hud.Show()`, :405-406/:437-438 SetStage 호출 제거. `_stage`/`_nextRingIndex`/`OnRingReached`/타임라인/사전 스캔은 유지(2차 음악 스텝용, 구독자 0 실측)
2. `Assets/Scripts/UI/ReplayHudUI.cs` (72줄) — 제거: :1 `using System.Text`, :27-28 `_escalationText`, :30-31 `_totalRings`, :33-34 `_pipBuilder`, :50-62 SetStage 통째, **:47 Show 내부 `SetStage(0)` 호출 포함**. Show(int)→Show(). 스킵 힌트만 유지
3. `Assets/Scripts/UI/PaperHudUI.cs` (44줄) — `using AfterYou.Replay;` + `[SerializeField] private ReplayDirector _replayDirector;` + Update(:27-41)에 리플레이 분기(`_roundManager == null` 가드 뒤): IsReplaying이면 남은 시간 = ReplayRemainingSeconds("00.00") / 경과 = ReplayElapsedSeconds("00.00"). 클론 수 표시는 기존 유지. 종료 시 자연 복귀(경과 = 클리어 값, 남은 = --.--)
4. `Assets/Scripts/Managers/LevelManager.cs` — :218 Bind 호출 인자에 `_currentLevel.LevelExit` 추가 (호출부 이 1곳뿐 — grep 실측)
5. `Assets/Scripts/Managers/RoundManager.cs` — :113-114(RemainingRecordSeconds) 인근 `public float MaxRecordSeconds => _maxRecordSeconds;` **1줄 추가 외 어떤 줄도 수정 금지** (보호 블록 diff 0)
6. SampleScene — 순서 엄수: 스크립트 5종 수정 → 컴파일 통과 → EscalationText GO(&1404080756) 에디터 삭제 → PaperHudUI(&1374308485) `_replayDirector`에 ReplayDirector(&5508597) 연결 → 씬 저장. `_escalationText` 직렬화 잔존은 필드 제거+저장으로 자동 소거 — YAML 직접 편집 금지

## 참조처 맵 (Critic 실측)

- `Bind(` → LevelManager.cs:218 유일 / `Show(int)` → ReplayDirector.cs:280 유일 / `SetStage` → ReplayDirector 2곳 + ReplayHudUI.cs:47 내부 1곳, 외부 0
- `MaxRecordSeconds` 기존 참조 0(이름 충돌 없음) / `PaperHudUI` 코드 참조 0(씬 1개) / `OnRingReached` 구독자 0(유지 정합)
- asmdef 단일(AfterYou.asmdef) — 네임스페이스 참조 추가에 수정 불요

## 제약 검증 결과 (Critic)

- 출구 트리거 재통과 안전(LevelExit :49 가드 + OnLevelCleared 멱등) / 흡입 중 스킵 안전(StopReplay가 FixedUpdate 차단) / AfterimageTrail 무해(히어로 Clone 모드라 잔상 자체 미생성 — 실측 확정) / PaperHudUI._replayDirector 수명 안전(씬 상주, 레벨 전환은 프리팹 교체) / 결정성 불변(ResetToStart가 흡입 위치 덮어씀)

## 동작 조건 체크리스트 (Evaluator 채점 기준)

1. 리플레이 중 PaperHUD 남은 시간 = 상한(15.00) − 리플레이 경과 카운트다운, 경과 시간 = 0부터 증가(리플레이 틱 × fixedDeltaTime)
2. 리플레이 종료(스킵/정지) 시 남은 시간 --.--, 경과 = 클리어 시점 값으로 복귀
3. 흡입/루프 대기 중 시간 표시 정지(틱 고정)
4. 상단 pip(●○) 미표시 — EscalationText 씬에서 제거, 스킵 힌트("아무 키나 눌러 건너뛰기")는 유지
5. 마지막 틱 후 히어로가 출구 콜라이더 중심으로 ≈0.35s SmoothStep 이동해 출구에 덮인 상태로 정지 → 2s 후 루프 재시작 시 처음부터 정상 재생(위치 오염 없음)
6. 매 루프 패스마다 흡입 재발동(_absorbElapsed 리셋)
7. 스킵/Unbind 시 흡입 즉시 중단 + 후처리(ResetUndoStack) 1회 유지
8. 흡입 중 틱 구동(박스/기믹/캐스트 ApplyTick) 정지 — frames 범위 밖 조회 없음
9. 클론/박스/기믹 재생 결정성 불변(리플레이 궤적 틱 일치 기존과 동일)
10. RoundManager 보호 블록 diff 0(MaxRecordSeconds 1줄만)
11. 컴파일 에러/워닝 0

## 셀프 리뷰 (계획이 틀릴 수 있는 지점 → 대응)

1. 흡입 중 ApplyTick 경합 → Critic이 배치 위험으로 격상, early-return 위치 필수 지시로 해소
2. 루프 타이머 시작 시점 이동(완주 즉시→흡입 완료 후) → 완주 블록 재구성 명세로 해소
3. 시간 상한이 레벨별로 달라질 가능성 → rm.MaxRecordSeconds 캐시(하드코딩 아님)로 자동 추종
