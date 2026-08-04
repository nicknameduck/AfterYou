# 하네스 최신 결과 (2026-08-05) — 엔딩 연출 (ALL CLEAR + 정체성 봇 + 잔상)

## 스펙

엔딩 연출 구현 — 마지막 레벨 클리어 시 첫 레벨 순환 대신 엔딩 진입. 포트폴리오 영상용 간단 엔딩: ① 위에서 아래로 내려오는 ALL CLEAR 텍스트 ② 지금까지 사용 가능한 도형 캐릭터들이 왔다갔다 점프도 하며 자유자재로 신나게 뛰어다니는 간이 AI ③ 잔상 동반.

- EndingBot.cs 신규 — 좌우 랜덤 워크 + 랜덤 점프, 벽/가장자리 방향 전환, Rigidbody2D 물리, 정체성 틴트 + 정체성별 점프력 차등
- EndingSequence.cs 신규 — ALL CLEAR 하강 SmoothStep, 해금 정체성 종류별 봇 1마리, Enter/N 첫 레벨 재시작
- Level_Ending.prefab 신규 — 바닥 top y=-7.71 + 좌우 벽
- LevelManager.cs 수정 — 마지막 레벨 클리어 시 엔딩 분기, ScreenFader 페이드 경유, _unlockedIdentities 전달
- AfterimageTrail.cs 소폭 수정 — CharacterActor 의존 완화(봇 재사용), 라이브 동작 불변
- CharacterSelectUI.cs 소폭 수정 — SetSuppressed(엔딩 중 State=Selecting 잔존으로 도크 재표시되는 문제 봉인)
- SampleScene — Canvas 하위 AllClearLabel(legacy Text, Pretendard-Bold, 검정) + EndingSequence + 참조 연결
- 제약: RoundManager 0줄 수정 / 클론 시스템 규약 불변 / 기존 레벨 프리팹 무수정 — 전부 준수 확인

## 동작 조건 (평가 결과)

- [✓] 마지막 레벨(Level_1_8, _levels 8개) 클리어 후 N/Enter/Next → 페이드 경유 엔딩 진입 (LevelManager.cs:157, :164-165)
- [✓] ALL CLEAR 화면 위 밖→하강 안착 (EndingSequence.cs:174 SmoothStep, 실측 y +199.98→-300.00)
- [✓] 해금 종류별 봇 스폰 4기, 색=TintColor, 점프력=JumpForce 차등 (실측 jump 10/10/12/14, 색 4종 상이)
- [✓] 좌우 이동 + 간헐 점프 + 벽 반전, 이탈 없음 (벽 top -1.0 vs 점프 최고 상단 -3.38, 18초 실측 |x|<20.3)
- [✓] 잔상 트레일 CharacterActor 없이 동작 (실측 *_Afterimages 4개, 활성 고스트 27장, NRE 0)
- [✓] 엔딩 중 카드 도크·PaperHUD·REC·Next 미표시 (SetSuppressed + activeSelf 스냅숏 숨김)
- [✓] Enter/N → Stop→해금 풀 Clear→LoadLevel(0), 2회차 엔딩 정상 (라벨 y=200 복원 실측)
- [✓] PageDown/PageUp 디버그 이탈 시 엔딩 잔재 0 (LevelManager.cs:228, :234 — Stop 선행)
- [✓] 엔딩 중 RoundManager 휴면 (_isInitialized=False 실측, Enter 녹화 미트리거)
- [✓] 기존 레벨 전환·라이브 잔상 불변 (재시작 후 _isInitialized=True 실측)
- [✓] 컴파일 에러/워닝 0

## 참조 패턴

- [카테고리: 시스템조립] #3 레벨=프리팹 교체 + 런타임 주입 — 소유 리스트 파괴 / SerializeField 배선 / 정리 순서 고정. #5 리셋 경로 전수 조사(엔딩 이탈 3경로 전부 Stop 경유), #6 토글러 데드락 회피(EndingSequence 자기 SetActive 금지)도 반영

## 점수

**98/100** (1회차) — 컨벤션 12/12, 생명주기 9/9, 성능 10/10, 안전성 9/9, 기능 충족 23/23, 스모크 실측 17/17, 단순성 8/10, 완결성 10/10. Major 0.

## 피드백

- [Minor] EndingSequence.cs:148 — `_hiddenPreviousStates` null/길이 방어는 도달 불가능 분기 (단순성 -2)
- [Minor] LevelManager.cs:225-231 — 엔딩 진입 페이드 진행 중(sub-second) PageDown 시 레이스 (디버그 전용 창, 기존 전환에도 동일 구조, Enter/PageDown으로 즉시 복구)
- [Minor] EndingBot.cs:78-80 — "Awake 순서 미보장" 주석은 사실과 다름(활성 프리팹 Instantiate는 반환 전 Awake 실행). Init의 GetComponent<SpriteRenderer>()는 캐시 재사용 가능
- [Minor] EndingSequence.cs:92-94 — bot.name 지정이 Instantiate 이후라 잔상 루트 이름이 `EndingBot(Clone)_Afterimages`로 남음 (기능 무영향)

## 수정 파일

- Assets/Scripts/Managers/LevelManager.cs (185→261줄)
- Assets/Scripts/Player/AfterimageTrail.cs (165→166줄, 2곳)
- Assets/Scripts/UI/CharacterSelectUI.cs (139→151줄, 2곳)
- Assets/Scenes/SampleScene.unity (+137/-0)
- Assets/Scripts/Ending/EndingBot.cs (신규)
- Assets/Scripts/Ending/EndingSequence.cs (신규)
- Assets/Prefabs/Ending/EndingBot.prefab (신규)
- Assets/Prefabs/Ending/Level_Ending.prefab (신규)

## 비고

- Generator 의도적 편차 1건 수용: `_hideDuringEnding` 복원을 "전부 SetActive(true)"가 아닌 Begin 시점 activeSelf 스냅숏 복원으로 — StatusText가 씬에서 원래 비활성이라 무조건 복원이 회귀가 되는 것을 방지
- 경량화 체계(스모크 실측 1회 + 독립 Critic) 첫 시스템급 적용 회차
