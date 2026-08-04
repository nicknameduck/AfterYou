# 하네스 최신 결과 (2026-08-04) — 점프대(JumpPad) 기믹

## 스펙
점프대 기믹 추가 — 밟으면 캐릭터를 수직으로 발사하는 발판. 기존 설치형 기믹 시스템(IActivatable/ITickGimmick, Assets/Prefabs/Gimmicks/)에 맞춰 레벨 프리팹에 배치만으로 동작. 정체성 Weight 기반 반발력 차등(IdentityData 무게 값에 따라 발사 높이 차등, 배율은 점프대 인스펙터에서 조정 가능). 클론 재생 재현성 유지: 틱 기반 판정, 재시작 시 동일 위상 재현. 동작 조건: 1) 밟으면 위로 발사된다 2) 가벼운/무거운 정체성의 발사 높이가 다르다 3) 클론이 밟아도 라이브와 동일하게 동작한다 4) 재도전 시 항상 같은 결과가 재현된다 5) 기존 기믹 레벨 회귀 없음

## 동작 조건 (평가 결과)
- [✓] JumpPad.cs가 ITickGimmick 구현, 자체 Update/FixedUpdate 없음 (JumpPad.cs:22/87/94, DriveGimmicks 경유만)
- [✓] 라이브가 밟으면(발바닥 tolerance && vy≤ε) 그 틱에 `linearVelocity.y = launchSpeed` 발사 (JumpPad.cs:108~135, 스폰 틱 발사 실측 Δy=0.348 정합)
- [✓] 발사 속도 = max(min, base − (Weight−1)×penalty) — 정점 실측 Light −1.474 / Heavy −4.925, 차등 3.45유닛 (JumpPad.cs:146)
- [✓] base/penalty/min 인스펙터 노출 (JumpPad.cs:43/46/49 + prefab 직렬화 확인)
- [✓] 클론(레이어 9, Kinematic)은 발사 질의 대상 아님(마스크 1) — 재생 중 clonePos vs frames[tick] 거리 0.000000 실측
- [✓] 재시작 결정성 — 발사 위상 한중간 RestartTake 인터럽트 후 재기록 첫 120프레임 비트 단위 동일 실측 (ResetGimmick 3경로 자동 커버)
- [✓] JumpPad.prefab: Ground(8) + 비트리거 BoxCollider2D — 트램펄린 반복 바운스(착지→재발사) 실측
- [✓] Level_1_8에 중첩 프리팹 인스턴스 설치(m_SourcePrefab guid 일치), LevelManager 자동 수집 — 매니저 코드 0줄
- [✓] 게이트 절대좌표 실측: 패드 top −7.41 / 렛지 top −3.41. Light 점프 −4.08 미달, Light 발사 −1.97 도달(여유 1.44), Heavy 발사 −5.43 미달(부족 2.0)
- [✓] 기존 회귀 0 — 1_1~1_7 프리팹·매니저 4파일 diff 0, 1_6/1_7 라이브 라운드 구동 콘솔 0
- [✓] 컴파일 에러/워닝 0 (read_console 3회)

## 참조 패턴
- [시스템조립] #5 설치형 틱 기믹 시스템 — 인터페이스 계약 + 자동 수집 드롭인, 리셋 3경로 자동 커버, 중첩 프리팹 설치, 지오메트리 실측 선행, 도달고 산술 게이트
- [데이터주도설계] #2 정체성+압력판 — Weight 순수 데이터, ContactFilter2D 3중 필터 + 재사용 List 할당 0

## 점수
**97/100** — 컨벤션 12/12 · 생명주기 9/9 · 성능 9/10 · 안전성 9/9 · 기능 충족 23/23 · 적대 검증 17/17 · 단순성 8/10 · 완결성 10/10
적대 시나리오 4종(공중 인터럽트 재현성 / 클론 재생 오염 / 측면 접촉 오발사 / 기존 레벨 회귀) 전부 실측 통과, Major 0.

## 피드백
### Major
- 없음

### Minor
- JumpPad.cs:51~61, 149~167 — 플래시 시각 서브시스템은 스펙 미요청(단순성 −2). 기존 기믹 시각 피드백 관례와는 일치
- JumpPad.cs:118 — 점유 지속 중 매 틱 GetComponentInParent (성능 −1, PressurePlate와 동일 관례 — Phase 5 핫패스 일괄 정리 대상에 합류)
- JumpPad.cs:125 — 지역변수명 `rigidbody`가 obsolete 프로퍼티를 가림 (동작 무해)
- 트램펄린 사양: 패드 위 대기 불가(매 착지 재발사) — 향후 "패드 위에서 대기"가 필요한 레벨 설계와 충돌하는 설계 제약으로 인지

## 수정 파일
- Assets/Scripts/Level/JumpPad.cs (신규)
- Assets/Prefabs/Gimmicks/JumpPad.prefab (신규)
- Assets/Prefabs/Level/Level_1_8.prefab (신규)
- Assets/Scenes/SampleScene.unity (+1/−0, _levels 등록)
