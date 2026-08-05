# 최종 통합 계획 (Generator 입력본) — 엔딩 연출 (ALL CLEAR + 정체성 봇 + 잔상)

> 2026-08-05. 스냅샷 커밋 `cb0e9b4`. 워크트리 OFF (씬/프리팹 수정 포함).

## 수정 대상

1. `Assets/Scripts/Managers/LevelManager.cs` (185줄, **Edit 전용**) — 수정 범위: 필드 추가 30~43 부근 / LoadLevel 53~66 (정리 로직을 `CleanupCurrentLevel()`로 추출 — **라인 이동만, 본문 바이트 동일** + `_currentLevel = null` 한 줄 추가) / LoadNextLevel 137~153 (마지막 레벨이면 순환 대신 페이드 경유 엔딩 진입) / Update 155~183 (엔딩 입력 + 디버그 키 엔딩 정리 분기). LoadLevel의 3)~6) 단계(비활성 부모 트릭 포함)는 **무변경 — git diff로 증명**.
2. `Assets/Scripts/Player/AfterimageTrail.cs` (165줄, **Edit 전용**) — 수정 2곳 한정: 9줄 `RequireComponent`에서 **CharacterActor만 제거(SpriteRenderer 유지)** / 69줄 라이브 판정을 `if (_actor != null && _actor.Mode != CharacterMode.Live)` 가드로 완화 (actor 없으면 항상 잔상 활성). AfterimageGhost·SpawnGhost·풀 로직 0줄. `_ghostRoot` 정리는 기존 OnDestroy가 봇 파괴 시에도 커버 — 추가 코드 불필요.
3. `Assets/Scripts/UI/CharacterSelectUI.cs` (139줄, **Edit 전용**) — `_suppressed` 필드 + `SetSuppressed(bool)` 공개 메서드 추가, 62줄 한 줄 병합만: `bool canSelect = !_suppressed && _roundManager.State == RoundState.Selecting;`. RebuildCards(100~137) 0줄. 배경: 엔딩 중 RoundManager.State가 Selecting으로 남아(Teardown이 리셋, RoundManager.cs:219) 카드 도크가 재표시되는 문제 실측 확인.

## 신규 파일

1. `Assets/Scripts/Ending/EndingBot.cs` — namespace AfterYou.Ending. `Init(IdentityData)`로 틴트/moveSpeed/jumpForce 주입(필드 대입만). FixedUpdate: 수평 속도만 덮어쓰기(y 보존 — 축 분리 규약), 접지 시 랜덤 점프(타이머), 전방 레이캐스트(Ground 마스크 256, useTriggers=false)로 벽 감지 시 방향 반전 + 랜덤 방향 전환 타이머. 접지 판정 OverlapBox(발밑, Ground만). UnityEngine.Random 사용(연출 전용 — 결정성 무관).
2. `Assets/Scripts/Ending/EndingSequence.cs` — `Begin(IdentityData[] 복사본, Transform levelParent)`: Level_Ending Instantiate + 봇 종류별 1기 스폰(x 산개 ±18 이내) + ALL CLEAR 하강 코루틴(SmoothStep) + `_hideDuringEnding` GameObject[] 숨김 + CharacterSelectUI.SetSuppressed(true). `Stop()`: 소유 리스트 기반 역순 정리(봇/레벨 인스턴스 Destroy, 코루틴 StopCoroutine, 라벨 초기 anchoredPosition 복원, 숨긴 GO 전부 SetActive(true) 복원, SetSuppressed(false)). **이중 호출 안전 가드 + IsActive 프로퍼티**. EndingSequence GO 자체 SetActive(false) 금지(토글러 데드락 패턴).
3. `Assets/Prefabs/Ending/EndingBot.prefab` — SpriteRenderer(Player와 동일 스프라이트 guid `96df851bdc6dceb44af67797957285fd`) + Rigidbody2D(Dynamic, mass 1, gravityScale 3, freezeRotation) + BoxCollider2D + EndingBot + AfterimageTrail. 레이어 Default(0).
4. `Assets/Prefabs/Ending/Level_Ending.prefab` — Ground(top y=-7.71, 폭 40.53, 검정, Ground 레이어) + 좌우 벽(x≈±20, **top ≥ -3.2 (바닥 top +4.5 이상 — jumpForce 14 도달고 3.33 차단)**, 검정, Ground 레이어). LevelDefinition 없음 — `_levels`에 등록하지 않고 EndingSequence가 GameObject로 소유.

## 씬 수정 (SampleScene)

- Canvas 하위 `AllClearLabel` — legacy `UnityEngine.UI.Text`(프로젝트 TMP 미사용), Pretendard-Bold(guid `89715c641a174fe49b2a48491d439c29`), 검정 글자. **anchor top-center + anchoredPosition 오프셋 방식(절대 스크린 좌표 금지)** — 초기 위치는 화면 위 밖(라벨 높이 이상 오프셋), 하강 목표도 anchoredPosition 기준. CanvasScaler는 ScaleWithScreenSize 1920×1080 match 0.5 확인됨.
- `=== MANAGERS ===` 하위 EndingSequence 컴포넌트 + 참조 연결: LevelManager._endingSequence / EndingSequence의 라벨·봇 프리팹·엔딩 레벨 프리팹·_hideDuringEnding(**PaperHUD + StatusText 필수** — statusText HelpLine이 엔딩 중 ALL CLEAR에 겹치는 것 방지)·CharacterSelectUI.

## 구현 순서

1단계: AfterimageTrail.cs 완화 (2곳) + CharacterSelectUI.cs SetSuppressed (2곳) → 컴파일 확인
2단계: EndingBot.cs + EndingSequence.cs 신규 → 컴파일 확인
3단계: LevelManager.cs — CleanupCurrentLevel 추출(라인 이동만) → 엔딩 분기 + Update 재구성
   ⚠ **Update 재구성 순서 (Critic 지시)**: ①디버그 블록 내부에서 `if (_isEnding) { _endingSequence.Stop(); _isEnding = false; }` 후 LoadLevel ②디버그 블록 다음 `if (_isEnding) { Enter/N → 재시작; return; }` ③기존 Cleared 가드.
   ⚠ **엔딩 재시작 콜백 순서 고정**: `FadeOutThen(() => { _endingSequence.Stop(); _isEnding = false; _unlockedIdentities.Clear(); LoadLevel(0); })` — **Stop이 Clear보다 반드시 먼저**. 페이드 중 재입력은 IsFading 선가드.
   ⚠ **페이더 미할당 폴백의 동일 프레임 Enter 누수**: 동기 LoadLevel(0) 직후 같은 프레임 enterKey가 RoundManager에 재소비되어 즉시 녹화 시작 위험 — 폴백 경로에 위험 주석 명기(페이드 경유 시 콜백이 코루틴 프레임이라 안전).
4단계: EndingBot.prefab + Level_Ending.prefab 제작 (MCP), 봇 스폰 y ≥ -7.0
5단계: 씬 수정 — AllClearLabel + EndingSequence + 참조 연결 (직렬화 연결 전수 확인)
6단계: 컴파일 0 확인 + 스모크 준비

## 참조처 맵 (Critic 실측)

- `LevelManager.LoadLevel` → LevelManager.cs:47(Start), :147/:151(LoadNextLevel), :164/:169(디버그 키). 외부 0
- `LevelManager.LoadNextLevel` → CharacterSelectUI.cs:50(Next 버튼), LevelManager.cs:182(Enter/N)
- `AfterimageTrail` → 코드 참조 0, Player.prefab:254 직렬화 1건. RequireComponent 제거는 기존 직렬화 무영향
- `CharacterSelectUI` → 코드 참조 0, SampleScene.unity:2072 1건
- `ScreenFader.FadeOutThen/IsFading` → LevelManager.cs:146~147 단일
- `RoundManager.State` 독자 → CharacterSelectUI.cs:62/89/93, PaperHudUI.cs:35, RecIndicatorUI.cs:32, LevelManager.cs:139/175. RoundManager 자체 Update/FixedUpdate/코루틴은 `_isInitialized` 가드로 엔딩 중 완전 휴면 — **RoundManager 0줄 제약 성립**
- `Keyboard.current` → RoundManager.cs:446(_isInitialized 가드로 엔딩 중 무발화), LevelManager.cs:159/178

## 과거 실패 패턴 (Critic)

- **지오메트리 매몰(72점 회차)** — 봇 스폰/벽 좌표 산술 선확정: 화면 half-width 20.27(ortho 11.4×16/9), 바닥 top -7.71 실측 정합 확인. 벽 top ≥ -3.2.
- **UI 토글러 자기 SetActive 데드락(#6)** — EndingSequence는 상시 활성 MANAGERS에, 끄는 대상은 남(라벨/HUD)만.
- **리셋 경로 전수 조사(#5)** — 엔딩 이탈 경로 3종(Enter 재시작 / PageDown / PageUp) 전부 Stop 경유.
- **_unlockedIdentities 참조 전달 함정** — Begin에서 복사본(ToArray) 보관 (Clear 순서 실수 방어).

## 참조 패턴

- [카테고리: 시스템조립] #3 레벨=프리팹 교체 + 런타임 주입 — 반영한 핵심 접근: 파괴는 소유 리스트 기반 / 씬 참조는 SerializeField 배선 / 정리 순서 고정(Teardown→파괴→null). #5의 "리셋 경로 전수 조사", #6의 "토글러 데드락 회피"도 반영.

## 동작 조건 (Evaluator 체크리스트)

- [ ] 마지막 레벨(Level_1_8) 클리어 후 N/Enter/Next 버튼 → 첫 레벨로 순환하지 않고 페이드 경유 엔딩 진입
- [ ] ALL CLEAR 텍스트가 화면 위 밖에서 내려와 안착 (SmoothStep, Canvas 하위 legacy Text, Pretendard-Bold, 검정)
- [ ] 해금 정체성 종류 수만큼 봇 스폰(마지막 레벨 도달 시 4기), 각 봇 색 = 정체성 TintColor, 점프력 = 정체성 JumpForce(차등)
- [ ] 봇들이 좌우 이동 + 간헐 점프, 좌우 벽에서 방향 전환, 화면 밖 이탈 없음 (벽 top이 최고 점프 도달고 이상)
- [ ] 봇 이동 시 잔상 트레일 표시 (AfterimageTrail 재사용, CharacterActor 없이 동작)
- [ ] 엔딩 중 카드 도크·StatusText(HelpLine)·PaperHUD 미표시, REC/Next 버튼 미표시
- [ ] 엔딩 중 Enter/N → Stop→해금 풀 Clear→LoadLevel(0) 순서로 첫 레벨 재시작, 2회차 엔딩도 정상(라벨 초기 위치 복원)
- [ ] 엔딩 중 PageDown/PageUp → 엔딩 잔재 0 (봇/레벨/라벨/HUD 복원 후 레벨 로드)
- [ ] 엔딩 중 RoundManager 휴면 유지 (Enter가 녹화를 트리거하지 않음 — _isInitialized=false)
- [ ] 기존 레벨 전환(1→2 등) 및 라이브 캐릭터 잔상 동작 불변
- [ ] 컴파일 에러/워닝 0
