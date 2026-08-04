using AfterYou.Core;
using UnityEngine;

namespace AfterYou.Ending
{
    /// <summary>
    /// 엔딩 연출 전용 간이 AI. 좌우로 걷다 벽에 닿으면 돌아서고, 접지 상태에서 가끔 점프한다.
    /// </summary>
    /// <remarks>
    /// 클론/라운드 시스템과 완전히 무관하다 — 녹화·재생·틱 규약을 일절 쓰지 않고 Default(0) 레이어의
    /// 독립 물리 객체로만 존재한다. 결정성이 필요 없는 연출이므로 UnityEngine.Random을 그대로 쓴다.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public class EndingBot : MonoBehaviour
    {
        [Header("이동")]
        [Tooltip("수평 이동 속도. Init이 정체성 값으로 덮어쓴다.")]
        [SerializeField] private float _moveSpeed = 7f;

        [Tooltip("점프 임펄스. Init이 정체성 값으로 덮어쓴다. (mass 1, gravityScale 3 전제)")]
        [SerializeField] private float _jumpForce = 14f;

        [Header("접지 / 벽 감지")]
        [Tooltip("발밑 접지 판정 박스 크기.")]
        [SerializeField] private Vector2 _groundCheckSize = new Vector2(0.9f, 0.1f);

        [Tooltip("중심에서 발밑 판정 박스까지의 아래 방향 거리. 콜라이더 반높이(0.5)에 맞춘다.")]
        [SerializeField] private float _groundCheckOffset = 0.5f;

        [Tooltip("접지·벽 질의에 쓰는 레이어. Ground(256)만 — 클론/박스 레이어 규약과 무관하다.")]
        [SerializeField] private LayerMask _groundLayer = 256;

        [Tooltip("진행 방향 벽 감지 레이 길이. 콜라이더 반폭(0.5)보다 조금 길게.")]
        [SerializeField] private float _wallCheckDistance = 0.7f;

        [Header("랜덤 타이머(초)")]
        [Tooltip("점프 시도 간격의 최소/최대. 접지 상태에서만 실제로 점프한다.")]
        [SerializeField] private Vector2 _jumpIntervalRange = new Vector2(0.7f, 2.2f);

        [Tooltip("자발적 방향 전환 간격의 최소/최대. 벽 반사와는 별개 타이머다.")]
        [SerializeField] private Vector2 _turnIntervalRange = new Vector2(1.5f, 4f);

        private Rigidbody2D _rigidbody;
        private SpriteRenderer _renderer;

        /// <summary>진행 방향(±1). 벽 반사와 랜덤 전환 타이머가 함께 뒤집는다.</summary>
        private int _direction = 1;
        private float _jumpTimer;
        private float _turnTimer;

        /// <summary>Ground 전용 질의 필터. 트리거(출구·킬존 등)를 벽/바닥으로 오인하지 않게 useTriggers=false.</summary>
        private ContactFilter2D _groundFilter;

        // 질의 결과 버퍼 — 존재 여부만 보면 되므로 크기 1로 충분하고, 매 프레임 배열 할당을 피한다.
        private readonly Collider2D[] _overlapBuffer = new Collider2D[1];
        private readonly RaycastHit2D[] _rayBuffer = new RaycastHit2D[1];

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _renderer = GetComponent<SpriteRenderer>();

            _groundFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = _groundLayer
            };

            _direction = Random.value < 0.5f ? -1 : 1;
            _jumpTimer = RandomInRange(_jumpIntervalRange);
            _turnTimer = RandomInRange(_turnIntervalRange);
        }

        /// <summary>
        /// 정체성의 겉모습·능력치를 이 봇에 주입한다. EndingSequence가 스폰 직후 호출한다.
        /// </summary>
        /// <remarks>
        /// 필드 대입만 한다 — Awake와 호출 순서가 보장되지 않으므로 캐시된 참조(_rigidbody 등)를 건드리지 않는다.
        /// 색만 예외적으로 SpriteRenderer를 직접 잡아 쓴다(Awake 전이어도 GetComponent는 안전).
        /// </remarks>
        public void Init(IdentityData identity)
        {
            if (identity == null) return;

            _moveSpeed = identity.MoveSpeed;
            _jumpForce = identity.JumpForce;
            GetComponent<SpriteRenderer>().color = identity.TintColor;
        }

        private void FixedUpdate()
        {
            bool isGrounded = Physics2D.OverlapBox(
                _rigidbody.position + Vector2.down * _groundCheckOffset,
                _groundCheckSize, 0f, _groundFilter, _overlapBuffer) > 0;

            // 자발적 방향 전환(벽과 무관하게 화면을 어슬렁거리게 만드는 타이머).
            _turnTimer -= Time.fixedDeltaTime;
            if (_turnTimer <= 0f)
            {
                _direction = -_direction;
                _turnTimer = RandomInRange(_turnIntervalRange);
            }

            // 벽 반사 — 진행 방향에 Ground가 있으면 즉시 되돌아선다(엔딩 무대 밖으로 나가지 않게).
            if (Physics2D.Raycast(_rigidbody.position, Vector2.right * _direction,
                    _groundFilter, _rayBuffer, _wallCheckDistance) > 0)
            {
                _direction = -_direction;
            }

            _jumpTimer -= Time.fixedDeltaTime;
            if (_jumpTimer <= 0f)
            {
                // 공중이면 이번 만료는 버리고 다음 간격을 다시 뽑는다(공중 점프 방지).
                if (isGrounded)
                {
                    _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 0f);
                    _rigidbody.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
                }
                _jumpTimer = RandomInRange(_jumpIntervalRange);
            }

            // 수평만 덮어쓰고 수직(중력/점프)은 물리에 맡긴다 — 프로젝트 축 분리 규약.
            _rigidbody.linearVelocity = new Vector2(_direction * _moveSpeed, _rigidbody.linearVelocity.y);
            _renderer.flipX = _direction < 0;
        }

        /// <summary>Vector2를 (min, max) 구간으로 해석해 난수를 뽑는다.</summary>
        private static float RandomInRange(Vector2 range)
        {
            return Random.Range(range.x, range.y);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.down * _groundCheckOffset, _groundCheckSize);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.right * _direction * _wallCheckDistance);
        }
#endif
    }
}
