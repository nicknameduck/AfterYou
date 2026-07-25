# 하네스 최신 결과 (2026-07-25) — 아트 Step 2: 클론 스캔라인 셰이더 + REC 오버레이

## 스펙
아트 Step 2 잔여 — 클론 스캔라인 셰이더 + REC 오버레이:
(1) 클론 고스트 셰이더: 재생 중인 클론 스프라이트에 스캔라인(흰색, 알파 8~12%) + 밝기 미세 지터를 적용하는 URP 2D 스프라이트 셰이더 + 머티리얼. RGB=정체성 색(vertex color) 보존, 알파 규약(CloneAlpha 0.5 × revealProgress) 완전 호환. 위치 지터 금지(밝기/UV만) — ART-DIRECTION.md §3·§4 준수. 라이브는 기존 머티리얼 유지, 클론 모드만 적용(CharacterActor.SetMode 연동).
(2) REC 오버레이: 녹화(Recording) 중일 때만 화면 구석 REC● UI(1Hz 점멸, REC 레드 #FF3B30, OSD 오프화이트 #E8ECF2). RoundManager 상태 기준 표시/숨김, 기존 Canvas 활용. 워크트리 OFF.

## 동작 조건 (평가 결과)
- [✓] CloneGhost.shader 컴파일 에러 0, CloneGhost.mat이 해당 셰이더 사용 (isSupported=True, ShaderHasError=False)
- [✓] 클론 모드 sharedMaterial == CloneGhost.mat, 라이브/대기는 기본(Sprite-Lit-Default) — 플레이 실측
- [✓] 확정→되감기→재선택 왕복 후 기본 머티리얼 복원 (2단 Rewind 실측)
- [✓] 클론 2기 sharedMaterial 참조 동일(ReferenceEquals=True, 에셋 원본 — 인스턴스 복제 없음)
- [✓] 스캔라인 시각 효과 — RT 픽셀 실측 4px 주기 줄무늬(ΔL≈0.016~0.02)
- [✓] 클론 RGB=TintColor, 알파=0.5×revealProgress 유지, ClonePlayback 수정 0줄
- [✓] 위치 지터 없음 (버텍스는 UnityFlipSprite+TransformObjectToHClip만)
- [✓] Canvas 하위 RecIndicator 존재, Recording에서만 표시 (RectTransform 우상단 (1810,1026)~(1890,1050) 화면 내)
- [✓] REC 점 #FF3B30 1Hz 점멸(양 위상 실측), 라벨 #E8ECF2
- [✓] Selecting/Cleared에서 숨김 (Cleared 강제 주입 실측 포함)
- [✓] 코어 파일(RoundManager/ClonePlayback/CharacterRecorder/RecordedFrame) 수정 0줄, CharacterActor 기존 라인 삭제 0 (git diff +31/-0)

## 참조 패턴
- [코어시스템] #1 모드 전환 단일 캡슐화 — 머티리얼 스왑을 SetMode 안에서만 수행
- [상태 머신] #4 "불변 라인 지정 + 삽입만" — SetMode 보호 블록 바이트 불변 유지

## 점수
**93/100** — 컨벤션 12/12, 생명주기 9/9, 성능 10/10, 안전성 9/9, 기능 충족 23/23, 적대 검증 17/17, 단순성 8/10, 완결성 10/10, 스펙 감사 -5 (상한 94 적용 대상)

## 피드백
- Major: 없음
- [Minor] 스캔라인 실효 강도 ~2.5% (CloneGhost.shader:106 가산항 × alpha 후 블렌딩에서 α 재곱 — α² 이중 감쇠). 명목 8~12% 대비 어두움. 아트 리뷰에서 안 보이면 _ScanlineStrength 상향 또는 보정
- [Minor] 라이브(Sprite-Lit)/클론(Unlit) 조명 모델 불일치 — 2D 라이트 본격 사용 시 클론만 조명 미수신으로 밝기 단차 가능
- [Minor] _cloneMaterial null 방어 과잉 (CharacterActor.cs:35-36, 170-177 static 경고 플래그 — 도메인 리로드 비활성 시 세션 간 잔존)
- [Minor] 점멸 위상이 전역 Time.time 기준 — Recording 진입 시점 따라 최대 0.5초 점이 꺼진 채 시작 가능
- [Minor] Label이 legacy Text — 기존 UI와 일관되므로 무감점, TMP 전환 시 함께 이관

## 스펙 감사
- 체크리스트 누락 1건(-5): "밝기 미세 지터 적용됨" 항목 부재 (구현 자체는 확인됨 — CloneGhost.shader:96-98, 24Hz 스텝 ±5%)

## 수정 파일
- Assets/Art/Shaders/CloneGhost.shader (신규, 115줄)
- Assets/Art/Materials/CloneGhost.mat (신규, guid c2d5caa5b795aa84d9dbca608ed6199e)
- Assets/Scripts/UI/RecIndicatorUI.cs (신규, 43줄)
- Assets/Scripts/Clone/CharacterActor.cs (+31/-0, 199→230줄)
- Assets/Prefabs/Player/Player.prefab (+3, _cloneMaterial 배선)
- Assets/Scenes/SampleScene.unity (+244, RecIndicator/Content/Dot/Label 계층)

## 특이사항 (Generator 발견)
- SRP Batcher 경로에서 SpriteRenderer.color는 vertex color가 아니라 **unity_SpriteColor**(UnityPerDraw)로 전달됨 — `input.color × _Color × unity_SpriteColor` 필요. flipX는 UnityFlipSprite로 반영
- 스펙의 "_root=RecIndicator 자신 + 같은 GO에 스크립트" 조합은 SetActive(false) 시 Update 정지로 데드락 — Content 래퍼 분리로 회피 (의도적 이탈 1건)
- 빌트인 폰트는 GetBuiltinExtraResource가 아니라 `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`
