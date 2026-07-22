# 하네스 최신 결과 (2026-07-23) — Phase 3-3b 기믹 확장 (신규 2종 + 캐리 Major 수정)

## 스펙

Phase 3-3b 기믹 확장.
1. **깨지는 발판(CrumblingPlatform)**: 캐릭터(513, 라이브+클론) 밟으면 N틱 카운트다운 후 붕괴(콜라이더 off), 테이크/되감기/사망 3경로 복원, Ground 레이어, 클론 재생이 붕괴 틱 재현(결정성)
2. **정체성 제한 포탈(IdentityPortal)**: A↔B 양방향 쌍, IdentityData[] 허용 리스트(참조 동일성) — 허용 정체성의 라이브만 텔레포트, 도착 후 영역 이탈 전 재작동 금지 래치, 클론 미작동(마스크 1), rb+transform 동시 세팅
3. **[Major] MovingPlatform 캐리 오검출 수정**: 지지체 소유 판정 + Level_1_6 스위치 스윕 코리더 밖 이동
4. ITickGimmick 편입·RoundManager/LevelManager 무수정·중첩 프리팹 인스턴스(bake 금지)
5. 씬 상주 바닥 기준 배치 + 실플레이 검증, 전부 틱 기반

## 동작 조건 (평가 결과)

- [✓] 컴파일 에러 0
- [✓] Gimmicks/ 프리팹 7개 (기존 5 + CrumblingPlatform·IdentityPortal)
- [✓] Level_1_7 중첩 PrefabInstance ×4(FallZone 재사용 포함) + 씬 7번째 등록·LoadLevel(6) 성공
- [✓] 붕괴: N틱→콜라이더 off→낙하, 경고색 보간, 3경로(테이크/되감기/사망) 복원 전부 실측
- [✓] 붕괴 결정성: 라이브 T1=577, 클론 재생 T2=577 — **동일 틱 재현 실측**
- [✓] 포탈: Heavy 이동 / Light 327틱 무반응(참조 동일성), rb+transform 동시(CharacterActor.SetPosition)
- [✓] 렛지 게이트: Light 도달고 3.33m < 필요 3.7m(산술) / Heavy 포탈로만 도달(실측)
- [✓] 왕복 래치: 323틱 점유 재텔레포트 0회 → 이탈 시 같은 테이크 내 해제 → 재사용, 리셋 시 해제
- [✓] 클론 포탈 무작동: 클론 327틱 상주 — 텔레포트 0회·래치 오염 없음
- [✓] 캐리 오검출 소멸: 7회 스윕 통과에도 지면 기립자 x 부동(1회차 사망 시나리오 소멸)
- [✓] 캐리 정상 유지: 갭 횡단 캐리 + 경계 안착 생존
- [✓] Level_1_6 스위치 x=-7(코리더+동선 밖) + 문 2개 배선 유지
- [✓] RoundManager/LevelManager 전체 diff 0 (git status 미포함)
- [✓] 기존 레벨·기믹 회귀 없음 (Level_1_1/1_6 실측, 콘솔 에러 0)

## 참조 패턴

- [시스템조립 #5] 설치형 틱 기믹 시스템(골격 전체 — 반복 성공)
- [데이터주도설계 #2] 게이트를 데이터로(IdentityData 허용 리스트)
- [코어시스템 #1] 자가 캡처 함정(지지체 질의 자기 콜라이더 제외)

## 점수

**총점 99/100** (95점 게이트: 적대 3/3 실측 통과 + 스펙 감사 누락 0건)

| 항목 | 배점 | 득점 |
|---|---|---|
| 코딩 컨벤션 | 12 | 12 |
| Unity 생명주기 | 9 | 9 |
| 성능 | 10 | 9 |
| 안전성 | 9 | 9 |
| 기능 충족 | 23 | 23 |
| 적대 검증 | 17 | 17 |
| 단순성 | 10 | 10 |
| 완결성 | 10 | 10 |

## 피드백

### Major
없음.

### Minor
1. 핫패스 GetComponentInParent — MovingPlatform.cs:159, CrumblingPlatform.cs:131, IdentityPortal.cs:105 (알려진 제약 자가 문서화, 콜라이더→액터 캐시로 개선 가능)
2. 동일 높이 구간 캐리 의미론 — 발판 top=지면 top 구간에서 탑승자가 지면에 인계됨(실측 무해). "동일 높이 지면을 가로지르며 계속 태우는" 레벨 설계 시 이 경계가 드러남 — 레벨 설계 가이드에 명시 권장
3. 포탈 텔레포트 속도 보존(의도 설계) — 고속 낙하 중 진입 시 도착지 관통 가능성은 배치 시 유의
4. 클론 라운드의 붕괴 "완료" 순간 자체는 미실측(트리거 틱 동일성 577=577 + 틱 순수함수로 함의)

## 수정 파일

- 수정: Assets/Scripts/Level/MovingPlatform.cs(+50/-2, 캐리 3단 배제), Assets/Prefabs/Level/Level_1_6.prefab(1줄 — 스위치 x=-7), Assets/Scenes/SampleScene.unity(1줄 — Level_1_7 등록)
- 신규: Assets/Scripts/Level/{CrumblingPlatform,IdentityPortal}.cs, Assets/Prefabs/Gimmicks/{CrumblingPlatform,IdentityPortal}.prefab, Assets/Prefabs/Level/Level_1_7.prefab
