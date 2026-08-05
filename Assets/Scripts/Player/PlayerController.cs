using AfterYou.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterYou.Player
{
    /// <summary>
    /// Phase 0 프로토타입용 2D 플랫포머 컨트롤러.
    /// 신규 Input System(액션 에셋)으로 좌우 이동 + 점프를 처리한다.
    /// GDD "넉넉한 실행 판정" 원칙에 따라 코요테 타임 + 점프 버퍼를 포함.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private string _actionMapName = "Player";

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 7f;
        [SerializeField] private float _jumpForce = 14f;

        [Header("Ground Check")]
        [SerializeField] private Transform _groundCheck;
        [SerializeField] private Vector2 _groundCheckSize = new Vector2(0.5f, 0.1f);
        [SerializeField] private LayerMask _groundLayer;

        [Header("넉넉한 판정")]
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField] private float _jumpBufferTime = 0.1f;

        [Header("벽타기 (Climber 전용)")]
        [Tooltip("부착 가능한 벽 레이어(Climbable=10). Ground/Clone은 제외 — 렛지·클론에는 붙지 않는다.")]
        [SerializeField] private LayerMask _climbableLayer;
        [Tooltip("측면 Climbable 검출 박스 크기(얇고 세로로 긴 박스).")]
        [SerializeField] private Vector2 _wallCheckSize = new Vector2(0.15f, 0.8f);
        [Tooltip("측면 검출 박스의 중심에서의 수평 오프셋. 캐릭터 콜라이더 반폭(0.5)에 맞춘다.")]
        [SerializeField] private float _wallCheckOffset = 0.5f;
        [Tooltip("벽점프 후 재부착을 차단하는 시간(초). 이 동안 수평 속도 덮어쓰기도 유예된다.")]
        [SerializeField] private float _wallJumpLockout = 0.15f;

        private Rigidbody2D _rigidbody;
        private InputAction _moveAction;
        private InputAction _jumpAction;

        private const float WallInputThreshold = 0.3f;

        private float _moveInput;
        private float _moveInputY;
        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private bool _isGrounded;

        /// <summary>클론(9) 레이어 전용 마스크. 접지 판정과는 별개로 "무엇을 밟았는가"만 묻는 질의에 쓴다.</summary>
        private int _cloneLayerMask;

        /// <summary>직전 프레임의 접지 여부. 공중 → 접지 전이(착지 순간)를 잡는 데만 쓴다.</summary>
        private bool _wasGrounded;

        /// <summary>공중에서 클론 위로 착지한 순간 발화. 클리어 리플레이의 협력 고리 수집 전용이다.</summary>
        public event System.Action OnLandedOnClone;

        private bool _canClimbWalls;
        private bool _isWallAttached;
        private int _wallDirection;
        private float _savedGravityScale;
        private float _wallJumpLockoutCounter;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();

            // 인스턴스별 액션 사본을 갖는다(Unity PlayerInput이 멀티플레이에서 쓰는 공식 패턴).
            // 에셋을 공유하면 캐릭터 3명이 같은 InputAction 객체를 쓰게 되고, OnEnable/OnDisable에
            // 레퍼런스 카운팅이 없어 호출 순서에 따라 액션이 최종 Disable → 2라운드부터 조작 불능이 된다.
            _inputActions = Instantiate(_inputActions);

            // NameToLayer는 없는 레이어에 -1을 준다. 1 << -1은 정의되지 않은 시프트이므로 0(검출 없음)으로 막는다.
            int cloneLayer = LayerMask.NameToLayer("Clone");
            _cloneLayerMask = cloneLayer >= 0 ? 1 << cloneLayer : 0;

            InputActionMap map = _inputActions.FindActionMap(_actionMapName, throwIfNotFound: true);
            _moveAction = map.FindAction("Move", throwIfNotFound: true);
            _jumpAction = map.FindAction("Jump", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            _moveAction.Enable();
            _jumpAction.Enable();

            // 테이크 시작 첫 프레임은 "이미 접지"로 본다. 스폰 지점에 클론이 서 있는 경우
            // 스폰 자체가 착지로 오인되는 것을 막는다(전이는 다음 이륙 이후부터 성립).
            _wasGrounded = true;
        }

        private void OnDisable()
        {
            _moveAction.Disable();
            _jumpAction.Disable();
            Detach();
        }

        private void Update()
        {
            _moveInput = _moveAction.ReadValue<Vector2>().x;
            _moveInputY = _moveAction.ReadValue<Vector2>().y;

            // 접지 체크: 발밑 박스에 지면 레이어가 겹치는지
            _isGrounded = Physics2D.OverlapBox(_groundCheck.position, _groundCheckSize, 0f, _groundLayer);

            // 착지 대상 판별(연출 전용). 접지 판정은 위 한 줄이 단독 소유하고, 여기서는 "무엇을 밟았는가"만
            // 클론 레이어 전용 마스크로 한 번 더 묻는다 — OverlapBox는 겹친 콜라이더 중 하나만 돌려주므로
            // 지면과 클론이 함께 걸린 경우 위 결과만으로는 클론 여부를 알 수 없기 때문이다.
            if (_isGrounded && !_wasGrounded && _cloneLayerMask != 0
                && Physics2D.OverlapBox(_groundCheck.position, _groundCheckSize, 0f, _cloneLayerMask))
                OnLandedOnClone?.Invoke();

            _wasGrounded = _isGrounded;

            // 벽점프 락아웃: 재부착·수평 덮어쓰기를 잠시 막는다. 착지하면 즉시 해제(입력 무시 구간 최소화).
            if (_isGrounded)
                _wallJumpLockoutCounter = 0f;
            else if (_wallJumpLockoutCounter > 0f)
                _wallJumpLockoutCounter -= Time.deltaTime;

            // 벽타기: 매 틱 부착 조건을 재검증한다(자가 치유 — 접촉/입력이 사라지면 즉시 이탈).
            UpdateWallAttach();

            // 코요테 타임: 지면을 막 떠나도 잠깐은 점프 허용
            _coyoteCounter = _isGrounded ? _coyoteTime : _coyoteCounter - Time.deltaTime;

            // 점프 버퍼: 착지 직전 입력을 잠깐 기억
            if (_jumpAction.WasPressedThisFrame())
                _jumpBufferCounter = _jumpBufferTime;
            else
                _jumpBufferCounter -= Time.deltaTime;

            if (_isWallAttached)
            {
                // 벽점프: 부착 중 점프는 벽 반대 방향으로 튕겨 나가며 이탈한다.
                // 버퍼/코요테를 소모해 기둥 하단 부착 시 접지와 겹쳐 이중 임펄스가 나는 것을 막는다.
                if (_jumpBufferCounter > 0f)
                {
                    int jumpDir = -_wallDirection;
                    Detach();
                    _rigidbody.linearVelocity = new Vector2(jumpDir * _moveSpeed, 0f);
                    _rigidbody.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
                    _jumpBufferCounter = 0f;
                    _coyoteCounter = 0f;
                    _wallJumpLockoutCounter = _wallJumpLockout;
                }
            }
            else if (_jumpBufferCounter > 0f && _coyoteCounter > 0f)
            {
                Jump();
                _jumpBufferCounter = 0f;
                _coyoteCounter = 0f;
            }
        }

        private void FixedUpdate()
        {
            // 벽 부착 중에는 x를 고정하고 y 입력으로만 상하 이동한다(중력은 Attach가 0으로 만들었다).
            if (_isWallAttached)
            {
                _rigidbody.linearVelocity = new Vector2(0f, _moveInputY * _moveSpeed);
                return;
            }

            // 벽점프 락아웃 중에는 수평 덮어쓰기를 유예해 벽점프 임펄스(반대 방향 튕김)를 보존한다.
            if (_wallJumpLockoutCounter > 0f)
                return;

            // 수평 속도만 덮어쓰고 수직(중력/점프)은 물리에 맡긴다
            _rigidbody.linearVelocity = new Vector2(_moveInput * _moveSpeed, _rigidbody.linearVelocity.y);
        }

        private void Jump()
        {
            _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 0f);
            _rigidbody.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
        }

        /// <summary>
        /// 벽타기형 전용: 매 틱 부착 상태를 재검증한다(자가 치유).
        /// 부착 중이면 이탈 조건(반대 입력 / 접촉 상실)을, 아니면 부착 조건(벽 방향 입력 + Climbable 접촉)을 본다.
        /// 벽점프는 Update의 점프 블록이 담당하므로 여기서 다루지 않는다.
        /// </summary>
        private void UpdateWallAttach()
        {
            // 정체성이 클라이머가 아니면 부착이 남아 있을 수 없다(정체성 전환 안전).
            if (!_canClimbWalls)
            {
                Detach();
                return;
            }

            // WASD 대각 입력은 정규화되어 x 성분이 0.707이므로 임계 0.3이면 대각도 벽 방향으로 인정된다.
            int inputDir = _moveInput > WallInputThreshold ? 1
                         : (_moveInput < -WallInputThreshold ? -1 : 0);

            if (_isWallAttached)
            {
                // 이탈 3경로 중 2개(반대 입력 / 접촉 상실). 중립 x입력은 부착을 유지한다.
                bool oppositeInput = inputDir == -_wallDirection;
                bool contactLost = !CheckClimbable(_wallDirection);
                if (oppositeInput || contactLost)
                    Detach();
            }
            // 벽점프 직후에는 재부착을 락아웃한다(벽 방향 홀드 중 점프가 즉시 재부착으로 무효화되는 것 방지).
            else if (_wallJumpLockoutCounter <= 0f && inputDir != 0 && CheckClimbable(inputDir))
            {
                Attach(inputDir);
            }
        }

        /// <summary>지정한 수평 방향(±1) 측면에 Climbable 레이어 콜라이더가 있는지 검사한다.</summary>
        private bool CheckClimbable(int direction)
        {
            Vector2 origin = _rigidbody.position + new Vector2(direction * _wallCheckOffset, 0f);
            return Physics2D.OverlapBox(origin, _wallCheckSize, 0f, _climbableLayer);
        }

        /// <summary>벽에 부착한다. 중력을 저장 후 0으로 만들어 y 입력으로만 상하 이동하게 한다.</summary>
        private void Attach(int direction)
        {
            if (_isWallAttached) return;
            _isWallAttached = true;
            _wallDirection = direction;
            _savedGravityScale = _rigidbody.gravityScale;
            _rigidbody.gravityScale = 0f;
            _rigidbody.linearVelocity = Vector2.zero;
        }

        /// <summary>
        /// 벽에서 이탈한다. 중력 복원을 단독 소유하는 유일한 경로다
        /// (매 틱 재검증 / 벽점프 / OnDisable 전부 이걸 호출 — gravityScale 0 잔존 방지).
        /// 미부착 상태에서 호출되면 저장값(0)으로 중력을 덮어쓰지 않도록 즉시 반환한다.
        /// </summary>
        private void Detach()
        {
            if (!_isWallAttached) return;
            _isWallAttached = false;
            _wallDirection = 0;
            _rigidbody.gravityScale = _savedGravityScale;
        }

        /// <summary>
        /// 정체성 에셋의 능력치를 이 컨트롤러에 주입한다. CharacterActor.Awake가 호출한다.
        /// </summary>
        /// <remarks>
        /// ⚠ 위 인스펙터/프리팹의 _moveSpeed / _jumpForce / _groundLayer 값은 런타임에 IdentityData가 덮어쓴다.
        ///   프리팹 값은 정체성이 붙지 않은 상태의 기본값일 뿐이다.
        ///
        /// ⚠ 이 메서드는 "필드 대입만" 한다 — 캐시된 참조(_rigidbody / _moveAction / _jumpAction / _inputActions)를
        ///   읽지도 쓰지도 않는다. CharacterActor.Awake와 이 클래스의 Awake는 실행 순서가 보장되지 않으므로,
        ///   여기서 캐시된 참조를 건드리면 아직 Awake가 돌지 않은 경우 NullReferenceException이 난다.
        ///   대입한 3개 값은 Update/FixedUpdate/Jump에서 매 프레임 다시 읽히고, 모든 Awake는 첫 Update보다
        ///   먼저 끝나므로 늦은 주입 문제도 없다.
        ///
        /// ⚠ 무게(Weight)는 여기서 쓰지 않는다. Rigidbody2D.mass에 대입하면 AddForce(Impulse)의
        ///   Δv = jumpForce / mass 가 되어 도달고가 붕괴한다. "높이 못 간다"는 오직 _jumpForce로만 표현한다.
        /// </remarks>
        public void ApplyIdentity(IdentityData identity)
        {
            if (identity == null) return;

            _moveSpeed = identity.MoveSpeed;
            _jumpForce = identity.JumpForce;
            _groundLayer = identity.GroundMask;
            _canClimbWalls = identity.CanClimbWalls;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_groundCheck == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_groundCheck.position, _groundCheckSize);

            // 벽타기 측면 검출 박스(좌/우). 부착 판정 범위를 눈으로 확인한다.
            Gizmos.color = Color.magenta;
            Vector2 center = transform.position;
            Gizmos.DrawWireCube(center + new Vector2(_wallCheckOffset, 0f), _wallCheckSize);
            Gizmos.DrawWireCube(center + new Vector2(-_wallCheckOffset, 0f), _wallCheckSize);
        }
#endif
    }
}
