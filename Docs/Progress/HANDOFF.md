# HANDOFF — 현재 상태 스냅샷

> 이 파일은 **날짜 로그와 달리 항상 덮어쓴다.** 새 세션은 이 파일 하나로 "지금 어디인지"를 파악하고, 상세 이력은 날짜 파일을 참조한다.

**마지막 갱신**: 2026-08-05

## 현재 위치

- **엔딩 연출(ALL CLEAR + 정체성 봇 + 잔상) · 커밋 진행** (2026-08-05, 하네스 98/100 Major 0, 1회차 통과 — 경량화 체계 첫 시스템급 적용) — 마지막 레벨(Level_1_8) 클리어 후 첫 레벨 순환 대신 엔딩 진입(페이드 경유). `EndingBot.cs`(간이 AI — 정체성별 틴트/이동속도/점프력 차등 주입, 수평만 덮어쓰기 y 보존, 접지 시 랜덤 점프, 벽 레이캐스트 반전) + `EndingSequence.cs`(ALL CLEAR 하강 SmoothStep +200→-300, 해금 종류별 봇 1기, UI 숨김 activeSelf 스냅숏 복원, Stop 소유 리스트 역순 정리) + `EndingBot.prefab`/`Level_Ending.prefab`(바닥 top -7.71 + 벽 top -1.0 — jump 14 도달 상단 -3.38 차단) + LevelManager 엔딩 분기(CleanupCurrentLevel 추출, 엔딩 중 Enter/N → Stop→해금 풀 Clear→LoadLevel(0) 재시작) + AfterimageTrail CharacterActor 의존 완화(봇 잔상 재사용) + CharacterSelectUI.SetSuppressed(엔딩 중 State=Selecting 잔존 도크 봉인). **RoundManager 0줄 수정** — Teardown 휴면 가드(_isInitialized) 위에 올린 라운드 밖 오버레이 모드(성공 패턴 #7 신규). 스모크 실측: 봇 4기 차등/라벨 하강/이탈 0/잔상 27장/재시작 잔재 0/콘솔 0. 잔여 Minor 4(도달 불가 방어 분기, 디버그 키 페이드 레이스, 주석 오류, 잔상 루트 이름 — 전부 기능 무영향). 엔딩 체감(봇 신남 정도·타이밍)은 플레이테스트 대기, 파라미터 전부 Inspector 노출
- **점프대(JumpPad) 기믹 · 커밋됨(`a14fd51`)** (2026-08-04, 하네스 97/100 Major 0, 1회차 통과) — 기믹 9종 체제. `JumpPad.cs` 신규(ITickGimmick, 마스크 1 라이브 전용 — 클론은 기록 궤적이 발사를 재현, vy≤ε 게이트로 무상태 발사, Weight 선형 감쇠 base 18/penalty 0.8/min 5 인스펙터 조정 가능 → 발사고 Light 5.50/Climber 5.03/Carrier 4.57/Heavy 1.98) + `JumpPad.prefab`(Ground 레이어, 비트리거) + `Level_1_8.prefab`(검증 레벨: 렛지 = 패드 +4.0 → Light만 발사로 도달, 점프·Heavy 불가 — 양방향 절대좌표 실측) + 씬 `_levels` 등록(+1줄). 매니저 4파일 0줄 수정(설치형 계약 3번째 실증). 적대 실측 4종 통과(공중 인터럽트 결정성/클론 재현 0.000000/측면 오발사 0/1_6·1_7 회귀 0). 잔여 Minor: 핫패스 GetComponentInParent(Phase 5 합류)/트램펄린 사양(패드 위 대기 불가 — 레벨 설계 제약). 플레이테스트 대기. 스냅샷 커밋 `14c40c6`
- **레벨 전환 페이드아웃/인 · 커밋됨(`14c40c6`)** (2026-08-04) — `ScreenFader.cs` 신규(풀스크린 오버레이, 아웃 0.3s→레벨 교체→인 0.3s, SmoothStep 이징 — "순간 멈춤" 피드백 대응, 시간 연장은 사용자가 보류, Inspector 튜닝 가능). 색상은 검정→**흰색 전환**(2026-08-04, 흰 배경에 녹아드는 연출 시도 중 — 코드는 알파만 제어, 색은 씬 FadeOverlay Image 소유) + `LevelManager.LoadNextLevel()` 페이드 경유(미할당 시 즉시 전환 폴백, `IsFading` 재진입 가드, 페이드 중 오버레이가 클릭 흡수). SampleScene Canvas 최상단 `FadeOverlay`(알파 0, raycastTarget OFF) + 참조 연결. 디버그 PageDown/PageUp은 즉시 전환 유지. MCP 실측: 중간 스크린샷 어두워짐 + 종료 후 alpha 0 복귀 + 콘솔 0. 클리어→Next 실제 흐름 체감은 플레이테스트 대기
- **클론 점선 외곽선 실험 → 전체 폐기** (2026-07-29, 사용자 "안 어울린다") — v1(몸통 위 덧그림)→v2(밴드 컷아웃+어두운 점선)→v3(띄운 프레임+몸통 색 점선, 실척 검증까지 완료) 3차 반복 후 폐기. `CloneGhost.shader`는 git checkout으로 `d5e55fe` 원본(스캔라인+지터만) 복원, 컴파일 0. 실험 스크린샷 `clone-outline-*.png` 5장 보존 — 재검토 시 v3 구조([점선 밴드→투명 갭→몸통] 3단 구획, 모서리 고정 점, 스텝 사각파 점멸)가 최종 도달점이었음
- **라이브 캐릭터 이동 잔상 · 미커밋** (2026-07-29, 사용자 A안 승인 + 그라데이션 피드백 반영) — `AfterimageTrail.cs` 신규(풀 8장, 간격 0.06s/수명 0.42s(동시 7장)/α0.75 시작·**제곱 감쇠**로 머리 진하고 꼬리 급감, 정체성 틴트 유지 — 4차 튜닝 반영) + Player.prefab 부착. 라이브 모드에서만 스폰, 포탈/되감기 순간이동은 잔상 안 이음(2u/frame 임계), 본체 비활성화 후에도 잔상은 자기 페이드로 정상 소멸. MCP 실측: 이동 중 동시 5장(이론 최대와 일치)/정지 시 0장/콘솔 0. 파라미터는 전부 Inspector 노출 — 체감 튜닝은 플레이테스트 대기
- **선택 도크 상태 연동 표시/숨김 · 미커밋** (2026-07-28, 사용자 요청) — `CharacterSelectUI.cs` Update()에서 카드 컨테이너를 Selecting에서만 표시, Recording/Cleared에선 숨김(activeSelf 변화 시에만 SetActive). MCP 실측: 녹화 중 도크 영역 픽셀 0, Selecting 복귀 시 재표시 확인
- **선택 도크 카드 배경 반투명 검정 · 미커밋** (2026-07-28, 사용자 C안 선택) — CardTemplate/Inner `(0,0,0,0.85)` (SampleScene). 흰 카드가 흰 월드에 묻히던 문제 해소, 방송 OSD 오버레이 콘셉트. Normal 테두리(흰 α0.15)는 어두운 안쪽 위에서 회색 림으로 읽힘. MCP 스크린샷 검증 완료(`card-translucent-check.png`)
- **FilmGrain 임시 OFF + 캐릭터 접지 부양 수정 · 미커밋** (2026-07-28, 사용자 요청) — ① `TapeWorld_VolumeProfile.asset` FilmGrain `active: 0` (값 0.35/0.3 보존 — "일단" 제거라 재활성만 하면 복귀. 이제 노이즈 룩 구성요소 중 활성인 것은 없음: 스캔라인 mat도 강도 0 상태) ② 캐릭터가 바닥 위 0.015유닛(~0.7px) 떠 보이던 문제 = Physics2D `Default Contact Offset 0.01` 때문(Box2D 정지 접촉 이격) → **0.005로 축소**(`ProjectSettings/Physics2DSettings.asset`). MCP 픽셀 실측으로 흰 틈 0px 검증, 콘솔 0. 접촉 물리 전역 영향이므로 플레이테스트에서 박스 밀기/스택 체감 확인 필요
- **바닥 레벨 프리팹 이관(공통 바닥 폐지) · 미커밋** (2026-07-28, 사용자 결정) — 씬 상주 Ground 삭제, Level_1_1~1_7 각 프리팹에 Ground 자식(top y=-7.71, 40.53×1.68, 검정/Ground 레이어/BoxCollider2D) + **프리팹 내부 좌표 전면 재기준**(전 콘텐츠 y -5.21 시프트, WYSIWYG — `=== LEVEL ===` 오프셋 없음). 새 레벨 배치 기준선 = **바닥 top y=-7.71**(화면 하단 y=-9.4, 카메라 y=2/ortho 11.4 불변). 플레이 모드 전 레벨 순회로 7개 전부 실측 검증. 이제 레벨별 바닥 구멍/높이/폭 디자인 가능
- **정체성 아이디어 기록: 트램펄린형 · 문서만** (2026-07-28) — GDD §5.2 확장 백로그에 추가(고유 동사 *띄운다* — 머리를 밟은 클론을 높이 튕겨 올리는 능동 발판). 색상은 초록 후보였으나 탈출구와 겹쳐 미정 보류
- **바닥 화면 하단 부착 + 선택 도크 우측 하단 · 미커밋** (2026-07-28) — ~~상주 바닥 y[-10,-2.5] 연장~~ **바닥 부분은 이후 레벨 프리팹 이관으로 대체됨(위 항목 참조)**. 선택 도크는 우측 하단(앵커 (1,0), -24/24, LowerCenter) 검은 바닥 띠 위에 배치. 하이라이트 테두리는 검은 띠 위라 흰색으로 반전(Highlight 흰 불투명 / Normal alpha 0.15). 스크린샷 `floor-attached-rightdock-check.png`
- **캐릭터 선택 UI 아이콘 도크 전환 · 미커밋** (2026-07-28) — 하단 카드 바 폐기 → 세로 아이콘 도크(96×96, VerticalLayoutGroup). Selecting 진입 시 첫 항목 자동 하이라이트(pending), ↑↓ 방향키 순환 이동 + 1/2/3·클릭 예비 선택 + Enter 시작. 하이라이트 = 카드 테두리(카드 루트 Image가 테두리, Inner가 안쪽 덮는 구조). Recording/Cleared에선 하이라이트 꺼짐(아이콘은 상시 표시). SelectBar 장식(TopLine/Heading/SideLine)은 비활성화만. MCP 실측 검증 완료
- **선택 모델 개편: 정체성 종류 카드 + 클론 예산 · 미커밋** (2026-07-27) — 카드 = 종류별 1장(중복 제거), 같은 종류 반복 사용 가능(예산 한도), 사용 시 클론 수 소모. LevelManager가 종류×예산 사전 스폰(Identity 주입 Awake 전용 제약), RoundManager.Initialize 시그니처 확장 + SetPendingIdentity/CloneBudget API. 1/2/3·클릭 = 예비 선택 → **Enter로 녹화 시작**. 상세 2026-07-27.md 8차
- **종이 룩 폐기 → 노이즈 반전 룩 복원 · 미커밋** (2026-07-28, 사용자 결정) — 종이 실험 4~13차 전부 폐기, 아트만 되돌리고 게임플레이(해금/카드 UI/점프 수정/줌아웃/HUD)는 유지. 복원 직전 전체 스냅샷은 **`experiment/paper-look` 브랜치(`8301a99`)에 백업**. 복원 내용: FullScreenPass 재활성+ScreenScanline.mat, FilmGrain ON(Medium1/0.35/resp 0.3), Vignette 0.28, Identity·기믹·PushableBox 원색 checkout, Player 머티리얼만 원복(_groundCheckSize 0.9 유지), 레벨 지형 9개 검정 재적용, 씬 바닥 검정+PaperBackground 삭제+카드 Inner 플랫화, 종이 에셋·NotoSerif 삭제(잔존 참조 0 검증). 콘솔 에러 0, 레퍼런스 `Assets/Screenshots/noise-look-restored.png`. 상세 2026-07-28.md
- **종이 시대 산출물 중 유지되는 것** — PaperHudUI.cs(상단 3분할 HUD — 이름만 Paper, 룩 무관), 캔버스 Screen Space Camera, 하단 카드형 선택 바(스킨만 플랫), 카메라 y=2, Pretendard-Bold
- **모서리 점프 수정 + 남은 시간 UI + TWA급 줌아웃 · 미커밋** (2026-07-27) — 접지 박스 x 0.5→0.9(Player.prefab, 모서리 반걸침 점프 불가 수정 — 1.0 이상은 벽 낙하 중 공중점프 부작용이라 금지), RoundManager.RemainingRecordSeconds + 상단 상태줄 "남은 시간 N.Ns"(녹화 중만), 카메라 ortho 5→8→**11.4**(사용자 "70%로 더 축소" — 캐릭터 = 화면 높이 약 1/23, 본 맵은 이 화면 기준으로 넓게 설계 예정) + 상주 바닥 폭 24→44, CloneAlpha 최종 0.85(사용자 상향 후 동기). **주의**: 배경 실루엣 19종이 씬에서 사라진 상태(사용자 삭제 추정, 확인 대기 — git 복구 가능)
- **반전 룩(현재 채택): 흰 배경 + 검은 지형 · 미커밋** (2026-07-27) — 카메라 배경 흰색(SampleScene), 씬 상주 바닥 + Level_1_2~1_7 지형 9개 검정(기믹·벽타기 벽 제외, 컴포넌트 이름 기반 제외 판정). 그레인 Medium1/0.35/response 0.3(흰 배경 자글자글 질감), CloneAlpha 0.5→0.65(흰 배경에서 0.5는 색 씻김). **미확정 잔여**: 배경 실루엣이 흰 배경에서 검정 덩어리로 보여 지형과 혼동(연회색으로 밀어내기 필요) / OSD 흰 글자 대비 죽음 / 클론 가산 스캔라인이 흰 배경에서 안 보임 / 채택 시 ART-DIRECTION 환경 팔레트 전면 개편, 폐기 시 git revert
- **풀스크린 노이즈 A+B 적용 · 미커밋** (2026-07-27, 사용자 승인 후 진행) — ① FilmGrain 0.18→0.38, response 0.8→0.55 (`TapeWorld_VolumeProfile.asset`) ② `ScreenScanline.shader`(신규, 감산·7px·0.035) + `ScreenScanline.mat` + `Renderer2D.asset`에 FullScreenPassRendererFeature 등록(AfterRenderingPostProcessing, fetchColorBuffer ON). 스크린샷 픽셀 분석으로 7px 주기 밴딩 실측 검증 완료, 콘솔 에러 0. 클론 스캔라인(가산·4px·0.1)과 구별 설계
- **서사 없음 + 캐릭터 디자인 방향 확정 · 문서화 완료** (2026-07-27, 사용자 결정) — GDD §11/§13 + ART-DIRECTION.md 갱신. 스토리/세계관 프레임 폐기(심플 진행형). 캐릭터 = 도형 베이스 + 단일 프레임 + 정적 손그림 외곽선(몸통 색 어두운 톤) + 얼굴 없음. **라인 보일 폐기(노이즈-온리)** — 생명감은 매체 노이즈(그레인/스캔라인/지터), 캐릭터 살은 스쿼시/스트레치 모션
- **아트 Step 2 클론 스캔라인 셰이더 + REC 오버레이 · 커밋됨** (`d5e55fe`, 2026-07-25, 하네스 93/100 Major 0, 플레이테스트 대기) — CloneGhost.shader/mat(클론 sharedMaterial 스왑, SetMode 삽입만 +31/-0) + RecIndicatorUI(Recording 중 REC● 1Hz). 잔여 Minor: 스캔라인 실효 ~2.5%로 약함(_ScanlineStrength 상향 여지) / 라이브(Lit)·클론(Unlit) 조명 모델 불일치(2D 라이트 본격 사용 시 단차). 상세 `Docs/harness-logs/harness-last.md`
- **배경 무드 슬라이스 적용 · 커밋됨** (`354b8bb`, 2026-07-25) — SampleScene에 테이프 세계 배경: 카메라 `#101318` + Global Volume(Bloom/Vignette/FilmGrain, `Assets/Settings/TapeWorld_VolumeProfile.asset`) + Ground `#2A303B` + Backdrop 실루엣 19개(콜라이더 없음, sortingOrder -100). 레퍼런스 `Assets/Screenshots/tapeworld-mood-final.png`. MCP 도구 함정은 2026-07-25 로그 참조(m_Sprite/색상 객체 형식/volume 서브에셋)
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

1. **[사용자 몫] 엔딩 플레이테스트 + 포트폴리오 영상 촬영** — 마지막 레벨 클리어 → 엔딩 체감(봇 점프 빈도/속도/방향 전환 리듬, ALL CLEAR 하강 1.2s, 잔상 밀도 — 전부 Inspector 튜닝 가능). 엔딩에서 Enter/N으로 처음부터 재시작(해금 풀 리셋) 가능해 촬영 반복 용이
2. **[사용자 몫] Level_1_4~1_8 플레이테스트** — 기존 대기(박스 밀기+벽타기+벽점프) + 기믹 감각: 문 3초 타이밍, 수정된 캐리감, 깨지는 발판 1.5초(Heavy 이동속도로 다리 횡단 가능한지), 포탈 왕복 조작감, Heavy 렛지 게이트, **점프대 발사감(Light/Heavy 차등 체감, 인스펙터 base 18/penalty 0.8/min 5 튜닝 여지)** + 레벨 전환 페이드 체감(흰색 0.3s)
3. **클리어 리플레이 연출(§9.3) + undo 스택 리셋** (Phase 3 잔여, 코어 루프 5단계 완성) — 기믹 C# 이벤트 노출(OnPressed/OnOpened/OnLandedOnClone/OnCleared) 포함. 재미 검증 통과로 선행 조건 충족
4. **천장 이동** (벽타기 확장) — Phase 3 마지막 잔여
5. **[비차단·임의 시점] 아트 Step 2 잔여** — Identity 에셋 4색 적용만 남음 (`Assets/Data/Identities/Identity_*.asset` 4개 `_tintColor`, ART-DIRECTION.md §2 확정값). 셰이더·REC 오버레이·Volume·실루엣은 2026-07-25 완료
6. **잔여 Minor**: `CharacterSelectUI.cs` 매 프레임 `SetActive`(Phase 5 병합) / 기믹 4곳+`PressurePlate.cs:113` 핫패스 GetComponentInParent / ToggleSwitch 바운스 이중 토글 가능성 / TimedDoor-Door 로직 중복 / 발판 top=지면 top 동일 높이 구간에선 탑승자가 지면에 인계됨(레벨 설계 시 유의) / 포탈 속도 보존 — 고속 낙하 진입 배치 유의 / [원인 미상 1회 관측] `_gimmicks=null` 상태 Rewind NRE(재현 실패, 기록만)

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
- **기믹 검출 마스크 규약** — 스위치류·깨지는 발판 513(라이브+클론 — 클론이 이력을 재현해야 결정성 성립), KillZone·캐리·포탈·**점프대** 1(라이브만 — 점프대 발사는 궤적에 기록되어 클론 재생이 자동 재현, 513으로 바꾸면 안 됨). 판정법: 기믹 효과가 캐릭터 궤적에 남으면 1, 기믹 자신의 상태에 남으면 513. 바닥은 **레벨 프리팹 소유**(공통 바닥 없음, 2026-07-28 이관) — 기준선 top y=-7.71, 레벨 배치는 이 위 기준
- **쌍 장치 래치는 행위자가 건다** — 포탈 텔레포트 래치는 출발측이 도착측에 세팅. 도착측 자체 감지에 맡기면 구동 배열 순서에 따라 같은 틱 핑퐁. 래치 해제 질의는 마스크 1(513이면 클론이 영구 래치 유발)
- **상호작용 트리거는 Default 레이어** — Ground류에 두면 접지 질의(트리거도 잡는 오버로드)가 지면으로 오인 → 무한 점프. disabled 콜라이더 bounds는 무효(0 크기) — 꺼진 상태 질의 스킵

## 확정 사양 / 폐기한 접근

- **종이 세계 룩 폐기 → 노이즈(반전 룩) 채택** (2026-07-28, 사용자 결정) — 종이 질감 실험 13차까지 진행 후 "다 별로" 판정. 현재 룩 = 흰 배경 + 검은 지형 + FilmGrain(Medium1/0.35/0.3) + 풀스크린 감산 스캔라인(7px/0.035). 종이 실험 전체는 `experiment/paper-look` 브랜치에 보존 — 재검토 시 여기서 복원
- **서사 프레임 없음** (2026-07-27, 사용자 결정) — 스토리를 넣으면 스코프 비대화 → GDD §11 세계관 컨셉 후보 4종(분열된 자아/시간 메아리/로봇 삼형제/꿈의 조각들) 전부 폐기. "그냥 진행되는 심플 게임". 서사적 살이 필요하면 추가 연출로만
- **캐릭터 디자인 = 도형 + 정적 손그림 외곽선, 라인 보일 폐기** (2026-07-27, 사용자 결정) — 노이즈-온리: 화면 생명감은 매체 노이즈(그레인/스캔라인/지터), 캐릭터 생명감은 스쿼시/스트레치 모션, 카툰 느낌은 정적 손그림 외곽선(몸통 색 어두운 톤, 순수 검정 금지). 얼굴 요소 없음. Thomas Was Alone은 참고 기준으로만. 라인 보일은 예비 카드(기본 미사용). 상세 ART-DIRECTION.md §캐릭터 디자인 방향
- **아트 디렉션 = "테이프/방송 세계"** (2026-07-25, 사용자 결정) — 후보 3안(테이프/청사진/손그림 흔들림) 중 A 확정. 베이스는 플랫 도형 유지, 차별화는 셰이더·연출·테마 레이어. 정체성 4색·시각 문법·가독성 상한은 `Docs/team/ART-DIRECTION.md`가 단일 기준. GDD의 "무거운=큰 네모" 임시에셋 항목은 폐기(코드 규칙 "색으로만 구분" 우선)
- **벽 부착은 Climbable 레이어(10) 전용** (2026-07-18) — Ground 벽 전체를 부착 대상으로 하면 지형이 사다리가 되어 게이트 붕괴(렛지 옆면 파훼). 부착 판정은 `_climbableLayer` 마스크로만. 천장 이동은 미구현(기믹 도입 후 확장)
- **부착 상태 규약** — 매 틱 재검증(자가 치유) + `Detach()`가 gravityScale 복원 단일 소유(이중 가드 필수, OnDisable에서도 호출). 이 규약을 깨면 "부착 중 확정→되감기→재선택"에서 중력 0 잔존 잠복 버그
- **벽점프 락아웃 규약** (2026-07-19) — 락아웃 카운터는 **부착 branch와 FixedUpdate 수평 덮어쓰기만** 차단한다. 이탈 경로는 절대 차단 금지(자가 치유 유지). 접지 시 카운터 즉시 0(락아웃 중 착지 시 입력 무시 방지)
- **fable 오케스트레이션 완전 철거** (2026-07-21 최종) — 기본 OFF 이후 실사용 없음 + OFF여도 게이트 훅이 호출당 ~350ms 소모 → 전역 설치 전체 제거. 백업 `~/.claude/_fable-teardown-backup-20260721/`. 잔여: `~/.claude/fable/hooks/orchestration-gate.py`(세션 훅 스냅샷 보호용) — **세션 재시작 후 폴더째 삭제 필요**. 하네스 모델 배분(Planner=메인 루프, Critic·Evaluator=fable, Generator=opus 강제)은 모델 이름 지정이라 무관하게 유지
- **하네스 경량화: 스모크 실측 체계** (2026-08-05, 사용자 결정) — 실행 시간이 초기 ~10분에서 40분+로 비대해진 원인 = 7/18 반증 채점 개편 이후 규칙 누적(작업 크기와 무관하게 전액 부과). ① **적대 시나리오 3개 의무 폐지** → 스모크 실측 1회(플레이 모드 진입 최대 1회 — 틱 결정성·좌표 수치 게이트·직렬화 연결만 수치 확인. 반복 검증·기존 레벨 회귀·구석 찌르기는 사용자 플레이테스트 담당) ② **Critic 조건부 생략** — 기존 파일 수정 2곳 이내·각 5줄 이내 + 같은 카테고리 성공 패턴 존재 시 에이전트 미실행, 메인 루프 인라인 체크(실패 로그 대조+참조처 grep) + 계획 말미 셀프 리뷰 블록("틀릴 수 있는 지점 3개")으로 대체. 시그니처/enum 변경·코어 다중 수정·신규 유형은 독립 Critic 유지 ③ **성공 패턴은 코드/시스템만 기록**, 태그 통계 블록·"평가 시 통했던 검증 기법" 섹션 폐지 ④ **Planner 풀 계획 유지**(델타 계획 축소안은 사용자 기각). 목표 소요: 정형 ~15-20분 / 시스템급 ~25-30분. 반영 파일: `~/.claude/skills/harness/SKILL.md`(전역 — LostPages에도 적용됨)/`.claude/harness-eval.md`/`CLAUDE.md`
- **Evaluator 반증 채점 체계** (2026-07-18) — 독립 에이전트 채점(자기 채점 편향 절단) + 스펙 감사 + 적대 시나리오 3개 실측 의무 + 만점 방지 조항. 이전 회차(99/100)와 점수 직접 비교 불가 — 하락이 정상. → **2026-08-05 스모크 실측 체계로 개편** (위 항목): 독립 채점·스펙 감사·만점 방지는 유지, 적대 실측 의무만 폐지

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
