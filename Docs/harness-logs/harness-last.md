# 하네스 최신 결과 (2026-07-22) — Phase 3-3 환경 기믹 5종 [REFINE 1회차]

## 스펙

Phase 3-3 환경 기믹 시스템 — 프리팹 설치형 신규 기믹 5종 구현.
1. 신규 기믹 5종: 토글 스위치, 시간제한 문, 이동 발판, 가시, 낙하 구멍
2. 배선 구조: IActivatable 인터페이스 + 다대상 리스트(스위치 하나로 문 여러 개). 신규 기믹에만 적용 — 기존 PressurePlate/Door 1:1은 무수정
3. 사망 처리: 가시/낙하 구멍에 라이브 접촉 시 RestartTake로 현재 테이크만 재시작. 클론은 통과(결정성)
4. 프리팹화 범위: 신규 기믹만 독립 프리팹(Assets/Prefabs/Gimmicks/ 신설). 기존 레벨 1_1~1_5 무수정
5. 결정성: 시간제한 문·이동 발판은 실시간 타이머 금지, RoundManager 틱 기반
6. 사용자 의도(핵심): 기믹을 프리팹/데이터로 관리하고 레벨에 "설치"하는 방식 — 향후 본 맵에서 재사용
7. 검증: Level_1_6 테스트 레벨에서 실측. 배선/설치가 실제로 동작해야 함

## 동작 조건 (평가 결과 — REFINE 후)

- [✓] 컴파일 에러 0
- [✓] Assets/Prefabs/Gimmicks/에 프리팹 5개 존재
- [✓] Level_1_6에 중첩 프리팹 인스턴스 6개(PrefabInstance + m_SourcePrefab guid 일치) — 1회차 ✗ 해소
- [✓] 씬 _levels 등록·LoadLevel(5) 실측 성공
- [✗] 기믹 5종 실플레이 안전 도달 — 매몰은 해소됐으나 **스위치가 발판 스윕 코리더 내부** → 캐리 오검출과 결합해 스위치 점유 시 강제 사망 (Major 1·2)
- [✓] 토글 → 문 2개 동시 제어(다대상), 재밟기 닫힘 실측
- [✓] 시간제한 문 틱 환산(150틱) 자동 닫힘 + 자기 동기화 후 **한 번 밟기 재개방** 실측 — 1회차 Minor 1 해소
- [△] 발판 왕복·캐리·접지 — 왕복 ✓, 캐리 동작 ✓, 단 **비탑승자(지면 기립자) 오검출 드래그** (Major 1). 접지는 정적 근거만(레이어 8 + 마스크 3840)
- [✓] 가시 사망(5회 재현)·클론 무시(마스크 1 + 참조 동일성 이중 방어)
- [✓] FallZone 낙하 사망 → RestartTake
- [✓] **사망 재시작 위상 desync 0** (_tick==_phaseTicks=81) — 1회차 Major 3 해소 (플래그 지연 처리)
- [✓] 테이크 시작/되감기/사망 시 기믹 리셋 (EnterSelecting·RestartTake·Initialize 3경로)
- [✓] 클론 재생이 스위치 재토글(마스크 513) — 사망 인터럽트 후 재생 재현까지 실측
- [✓] RoundManager 보호 블록 diff 0 (순수 추가만)
- [✓] 기존 레벨·기존 스크립트 회귀 없음 (Initialize 호출처 LevelManager.cs:97 유일 확인)

## 참조 패턴

- [코어시스템 #1] 중앙 틱 단일 소유 + 초→틱 환산 / [데이터주도설계 #2] 기하 판정·3중 필터·엣지 통지 / [시스템조립 #3] Bind 주입·리스트 수집·diff 0 증명

## 점수

**총점 88/100** (1회차 72 → +16)

| 항목 | 배점 | 득점 |
|---|---|---|
| 코딩 컨벤션 | 12 | 12 |
| Unity 생명주기 | 9 | 9 |
| 성능 | 10 | 8 |
| 안전성 | 9 | 9 |
| 기능 충족 | 23 | 13 |
| 적대 검증 | 17 | 17 |
| 단순성 | 10 | 10 |
| 완결성 | 10 | 10 |

## 피드백

### Major (잔여 2건 — 단일 결함 축)
1. **MovingPlatform 캐리 오검출 — 지면 기립자를 탑승자로 드래그** (MovingPlatform.cs:124-148, 판정 :145). 발판 상면(-1.5)이 지면 top(-1.5)과 동일 높이로 스윕하면 발판 위 검출대역이 지면 기립 캐릭터 발바닥과 겹쳐 발판 속도로 끌고 감. "무엇 위에 서 있는가"(지지체 소유) 판정 부재. 실측: 스위치 위 대기 ~1.3초 → 가시로 드래그 사망, 5회 재현
2. **Level_1_6 배치 결함 — 스위치(x=1.5)가 발판 스윕 코리더(x∈[-3,2]) 내부** — 1과 결합해 검증 레벨이 자체 기믹 간섭으로 오염. 수정 방향: 캐리 판정 강화(지지체 소유/높이 차 요구) 또는 발판 스윕 구간과 지면 높이 분리 배치

### Minor
1. 핫패스 매 틱 GetComponentInParent 3곳 (주석 자인, 성능 -2)
2. ToggleSwitch 1틱 바운스 이중 토글 가능성 (주석 자인, 미실측)
3. TimedDoor가 Door와 콜라이더/색 토글 중복 (기존 Door 불변 스펙상 허용)
4. [관측만] 세션 초반 1회 `_isInitialized=true && _gimmicks=null` 상태 Rewind NRE — 재현 3회 실패, 코드 경로상 도달 불가, 원인 미상 기록

### 1회차 대비 해소된 항목
- Major 1(매몰)·Major 2(PrefabInstance 0건)·Major 3(사망 desync) 전부 해소
- Minor 1(토글 자기 동기화)·Minor 2(bool 네이밍)·Minor 3(수동문 분기 제거) 해소

## 수정 파일

- 수정: Assets/Scripts/Managers/RoundManager.cs (+63 누적, 순수 삽입 — _isDeathPending 플래그 지연 처리 포함), Assets/Scripts/Managers/LevelManager.cs (+9), Assets/Scenes/SampleScene.unity (+1)
- 신규: Assets/Scripts/Level/{IActivatable,ITickGimmick,ToggleSwitch,TimedDoor,MovingPlatform,KillZone}.cs
- 신규: Assets/Prefabs/Gimmicks/{ToggleSwitch,TimedDoor,MovingPlatform,Spikes,FallZone}.prefab, Assets/Prefabs/Level/Level_1_6.prefab (중첩 인스턴스 6개 + _targets 오버라이드 배선)
