# Evaluator 평가 기준 — AfterYou (Unity 퍼즐)

## 코딩 컨벤션 (18점)
- 클래스/메서드/프로퍼티: PascalCase
- private 필드: _camelCase
- bool 변수: is/has/can 접두사
- 인터페이스: I 접두사
- [SerializeField] private 유지
- is null 사용 시 감점 (반드시 == null / != null 사용)
- 디버그 코드 #if DEVELOPMENT_BUILD || UNITY_EDITOR 미래핑 시 감점
- 디버그 프로퍼티 네이밍 Debug 접두사 없으면 감점

## Unity 생명주기 (13점)
- Awake/Start/OnEnable 역할 혼용 없는지
- Update()에 무거운 로직 없는지
- OnDestroy에서 이벤트 구독 해제 및 코루틴 정리하는지

## 성능 (13점)
- Update()에서 GetComponent 호출 없는지 (Awake/Start에서 캐싱 필수)
- 런타임에 FindObjectOfType / GameObject.Find 없는지
- 핫 경로에서 Instantiate/Destroy 없는지 (오브젝트 풀링 필요)
- 문자열 연산을 Update에서 하는지

## Unity API 최신화 (감점 기준)
- Unity 공식 문서 기준 `[Obsolete]` 표시된 API 사용 시 매 항목 -3점
- 대표 사례:
  - `FindObjectOfType<T>()` → `FindAnyObjectByType<T>()`
  - `FindObjectsOfType<T>()` → `FindObjectsByType<T>(FindObjectsSortMode.None)`
  - `Object.DestroyImmediate` 런타임 사용 → `Object.Destroy`
  - `Input.*` (구 Input System) → `UnityEngine.InputSystem.*`

## 안전성 (13점)
- null 처리, 예외 처리 적절한지
- static 레퍼런스로 오브젝트 붙잡는지
- 코루틴 중복 실행 방지 처리 있는지
- 이벤트/델리게이트 구독 후 OnDestroy에서 해제하는지

## 기능 충족 (23점)

Planner가 제시한 **"동작 조건"** 체크리스트의 각 항목이 실제로 충족되는지 평가한다.
조건 누락 1개당 -5점, 최대 -23점.

### 평가 절차
1. Planner 출력의 "동작 조건" 섹션을 읽는다
2. 각 조건별로 관련 코드 경로를 추적한다 (호출 체인, 이벤트 흐름 포함)
3. UI/씬 관련 조건은 **MCP로 실측 (의무, 생략 시 감점)**
4. 각 조건에 대해 `✓` / `✗` + 근거(파일:줄번호 또는 MCP 출력)를 기입한다

### 필수 검증 항목 (UI 관련 작업일 때)
- □ UI 오브젝트가 올바른 Canvas 계층 아래에 존재하는가
- □ SetActive(true)로 실제 활성화되는 코드 경로가 있는가
- □ RectTransform의 anchoredPosition이 부모 Rect 범위 내에 있는가
- □ SerializeField 참조가 Inspector에서 None이 아닌가
- □ 부모 RectTransform의 크기가 양수인가 (0 또는 음수 시 자식 보이지 않음)
- □ CanvasGroup.alpha, Image.color.a가 0이 아닌가 (투명 상태 방지)

### 평가 결과 형식
```
### 기능 충족 평가
[조건 1] 스킬 슬롯 버튼이 BottomBar 하위에 존재
  → ✓ ManualSkillSlotUI.cs:42에서 parent 지정, MCP find 확인
[조건 2] 클릭 시 테두리 점멸 시작
  → ✗ OnClick 핸들러에 점멸 코루틴 호출 누락 [Major]
```

## 회귀 방지 (감점 기준)
수정된 코드가 기존 기능을 깨뜨리는 경우 매 항목 -5점.
- **참조처 누락**: 함수 시그니처/enum 변경 시 호출부를 함께 수정하지 않아 컴파일 에러 발생 → -5점
- **타입 검출 누락**: TryGetComponent/GetComponent 변경 시, 해당 타입을 구현하는 다른 클래스가 검출 불가 → -5점
- **레이어 불일치**: LayerMask 변경 시 프리팹 m_Layer와 코드의 LayerMask가 불일치 → -5점
- **UI 좌표계 오류**: UI 부모 변경 시 앵커/스케일로 인한 좌표 오프셋 미처리 → -5점
- **직렬화 불일치**: SerializeField 변경 시 씬/프리팹의 직렬화 데이터와 코드 필드명이 불일치 → -5점
- **컴파일 에러**: 수정 결과물에 컴파일 에러가 남아있는 경우 → -10점

## 파괴적 변경 감지 (감점 기준)
기존 파일의 구조를 대규모로 훼손하는 경우.
- **줄 수 20% 이상 감소**: 수정 전 대비 파일 줄 수가 20% 이상 줄어든 경우 → -15점
  - 의도적 리팩토링(함수 추출, 중복 제거)으로 줄어든 경우는 감점 제외
  - Write 도구로 전체 재작성하여 기존 기능이 삭제된 경우에 해당
- **Critic 지시 위반**: Critic이 "Edit만 사용"으로 지시한 파일을 Write로 전체 재작성한 경우 → -15점
- **반복 파괴**: 동일 파일에서 2회 이상 파괴적 변경 발생 시 → 추가 -10점

## Unity MCP 런타임 검증 (의무 + 감점 기준)
코드 변경이 UI 또는 씬 오브젝트와 관련될 때, Unity MCP로 실제 동작을 반드시 확인한다.

**실측 생략 처벌은 "기능 충족 (25점)" 섹션에서만 적용된다** (실측 없이는 조건 충족 증명 불가 → 해당 섹션 0점 처리). 여기 항목들은 **실측을 수행했을 때 발견된 문제**에만 적용하여 이중 감점을 방지한다.

- **UI 배치 오류**: 부모 RectTransform 크기/앵커 대비 자식 위치가 화면 밖인 경우 → -5점
- **SetActive 자기 파괴**: 자기 자신의 GameObject를 SetActive(false)해서 Start/OnEnable 이후 코드가 실행 안 되는 경우 → -5점
- **SerializeField 미연결**: Inspector에서 필드가 None인 채로 남아있는 경우 → -5점
- **컴파일 에러 미확인**: MCP read_console로 에러를 확인하지 않은 경우 → -5점

## 단순성 (10점)
스펙을 충족하는 최소 코드를 평가한다. 과도설계는 유지보수 부담을 만든다.

- **스펙 외 추상화/제네릭/플렉시빌리티 도입** → 항목당 -3점
  - 예: 스펙에 단일 사용 헬퍼인데 인터페이스/추상 클래스 도입
  - 예: 요청되지 않은 설정 가능성(SerializeField 옵션 다수, 분기 매개변수)
- **단일 사용 헬퍼 클래스/메서드 분리** → 항목당 -2점
  - 호출 지점이 1곳뿐인데 별도 클래스/유틸로 분리
- **일어나지 않는 시나리오 방어 코드** → 항목당 -2점
  - 예: 본인이 직접 생성한 직후의 인스턴스에 null 체크
  - 예: 스펙상 발생 불가능한 상태 분기 처리
- **동등 동작을 절반 코드로 가능했음에도 풀어쓴 경우** → -3점
  - LINQ/패턴 매칭/null 병합 등으로 단축 가능했는데 장황하게 작성
  - "200줄 → 50줄로 가능하면 다시 써라" 원칙

평가 시 의심스러우면 "선임 개발자가 이걸 보고 과도설계라 할까?" 자문한다.

## 완결성 (10점)
- TODO 없음, 누락 파일 없음
- 스펙에서 요구한 모든 기능 구현됨
- 인터페이스 계약 올바르게 구현됨
