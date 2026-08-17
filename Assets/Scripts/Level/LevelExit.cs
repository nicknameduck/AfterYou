using AfterYou.Clone;
using AfterYou.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterYou.Level
{
    /// <summary>
    /// 출구. 라이브(현재 조작 중인) 캐릭터가 범위 안에서 위 키(↑/W)를 눌러야 클리어시킨다.
    /// 범위 안에 들어오면 문 위에 ↑ 프롬프트가 떠서 입력을 유도한다.
    /// </summary>
    /// <remarks>
    /// 확정 규칙: 클론이 출구에 닿아도 클리어되지 않는다.
    /// 태그/레이어로 대충 거르면 클론도 같은 프리팹이라 구분이 불가능하므로,
    /// RoundManager.LiveCharacter와 "참조 동일성"으로 비교한다.
    /// (2026-08-17 변경: 닿는 즉시 클리어 → 위 키 입력 게이트 + 유도 프롬프트 — 사용자 결정)
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class LevelExit : MonoBehaviour
    {
        [SerializeField] private RoundManager _roundManager;

        [Header("입장 프롬프트")]
        [SerializeField] private SpriteRenderer _promptRenderer;   // 문 위 ↑ 표시(에디터 스크립트가 배선)
        [SerializeField] private float _promptWorldScale = 0.5f;   // 프롬프트 월드 크기(부모 스케일 무관)
        [SerializeField] private float _promptGap = 0.35f;         // 문 꼭대기와의 간격
        [SerializeField] private float _bobAmplitude = 0.08f;      // 둥실거림 진폭
        [SerializeField] private float _bobFrequency = 2.2f;       // 둥실거림 속도(Hz)

        // bool이 아니라 "안에 있는 액터 참조"를 저장한다 — 범위 안에서 클론 확정으로 라이브가
        // 바뀌면(참조는 남고 트리거 이벤트는 안 옴) bool은 stale이 되어 새 라이브가 밖에 있어도
        // 프롬프트·클리어가 오발동한다. 참조 저장 + 매 프레임 LiveCharacter 동일성 재검증으로 차단.
        private CharacterActor _insideActor;
        private Vector3 _promptBasePosition;

        /// <summary>
        /// 라이브 캐릭터가 출구에서 클리어한 순간(= 클리어 전이 직전)에 1회 발화한다.
        /// </summary>
        /// <remarks>
        /// ⚠ 핸들러에서 상태 전환 메서드를 호출하지 말 것. 연출·큐잉 전용이다.
        /// </remarks>
        public event System.Action OnCleared;

        /// <summary>
        /// 레벨 프리팹은 씬의 RoundManager를 직렬화 참조할 수 없다(프리팹→씬 참조 불가).
        /// LevelManager가 로드 시 주입한다.
        /// </summary>
        public void BindRoundManager(RoundManager roundManager)
        {
            _roundManager = roundManager;
        }

        private void Start()
        {
            if (_promptRenderer == null) return;

            // 프롬프트를 문 꼭대기 위에, 부모(출구) 스케일과 무관한 고정 월드 크기로 앉힌다.
            // 출구를 세로로 키워도(사용자 튜닝) 화살표가 늘어나지 않도록 lossyScale 역보정.
            Bounds bounds = GetComponent<Collider2D>().bounds;
            _promptBasePosition = new Vector3(bounds.center.x, bounds.max.y + _promptGap, transform.position.z);

            Transform promptTransform = _promptRenderer.transform;
            Vector3 parentScale = promptTransform.parent != null ? promptTransform.parent.lossyScale : Vector3.one;
            promptTransform.localScale = new Vector3(
                _promptWorldScale / Mathf.Max(Mathf.Abs(parentScale.x), 1e-4f),
                _promptWorldScale / Mathf.Max(Mathf.Abs(parentScale.y), 1e-4f),
                1f);
            promptTransform.position = _promptBasePosition;

            _promptRenderer.gameObject.SetActive(false);
        }

        private void Update()
        {
            bool shouldPrompt = _insideActor != null
                && _roundManager != null
                && _insideActor == _roundManager.LiveCharacter
                && _roundManager.State == RoundState.Recording;

            if (_promptRenderer != null && _promptRenderer.gameObject.activeSelf != shouldPrompt)
                _promptRenderer.gameObject.SetActive(shouldPrompt);

            if (!shouldPrompt) return;

            // 둥실거림 — 렌더 전용 연출이라 결정성과 무관(판정은 키 입력 순간에만 본다).
            float bob = Mathf.Sin(Time.time * _bobFrequency * 2f * Mathf.PI) * _bobAmplitude;
            _promptRenderer.transform.position = _promptBasePosition + new Vector3(0f, bob, 0f);

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            {
                // Invoke만 가드한다 — 상태 전환 "이전"에 발화하므로 여기서는 아직 Recording이다.
                if (_roundManager.State != RoundState.Cleared)
                    OnCleared?.Invoke();

                // 클리어 통지는 무조건 호출한다(내부에서 Cleared면 즉시 return하는 멱등 메서드다).
                _roundManager.OnLevelCleared();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_roundManager == null) return;

            // 콜라이더가 자식(GroundCheck 등)에 붙어 있을 수 있으므로 부모까지 거슬러 찾는다.
            CharacterActor actor = other.GetComponentInParent<CharacterActor>();
            if (actor == null) return;

            if (actor == _roundManager.LiveCharacter)
                _insideActor = actor;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            CharacterActor actor = other.GetComponentInParent<CharacterActor>();
            if (actor != null && actor == _insideActor)
                _insideActor = null;
        }
    }
}
