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

        private Rigidbody2D _rigidbody;
        private InputAction _moveAction;
        private InputAction _jumpAction;

        private float _moveInput;
        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private bool _isGrounded;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();

            // 인스턴스별 액션 사본을 갖는다(Unity PlayerInput이 멀티플레이에서 쓰는 공식 패턴).
            // 에셋을 공유하면 캐릭터 3명이 같은 InputAction 객체를 쓰게 되고, OnEnable/OnDisable에
            // 레퍼런스 카운팅이 없어 호출 순서에 따라 액션이 최종 Disable → 2라운드부터 조작 불능이 된다.
            _inputActions = Instantiate(_inputActions);

            InputActionMap map = _inputActions.FindActionMap(_actionMapName, throwIfNotFound: true);
            _moveAction = map.FindAction("Move", throwIfNotFound: true);
            _jumpAction = map.FindAction("Jump", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            _moveAction.Enable();
            _jumpAction.Enable();
        }

        private void OnDisable()
        {
            _moveAction.Disable();
            _jumpAction.Disable();
        }

        private void Update()
        {
            _moveInput = _moveAction.ReadValue<Vector2>().x;

            // 접지 체크: 발밑 박스에 지면 레이어가 겹치는지
            _isGrounded = Physics2D.OverlapBox(_groundCheck.position, _groundCheckSize, 0f, _groundLayer);

            // 코요테 타임: 지면을 막 떠나도 잠깐은 점프 허용
            _coyoteCounter = _isGrounded ? _coyoteTime : _coyoteCounter - Time.deltaTime;

            // 점프 버퍼: 착지 직전 입력을 잠깐 기억
            if (_jumpAction.WasPressedThisFrame())
                _jumpBufferCounter = _jumpBufferTime;
            else
                _jumpBufferCounter -= Time.deltaTime;

            if (_jumpBufferCounter > 0f && _coyoteCounter > 0f)
            {
                Jump();
                _jumpBufferCounter = 0f;
                _coyoteCounter = 0f;
            }
        }

        private void FixedUpdate()
        {
            // 수평 속도만 덮어쓰고 수직(중력/점프)은 물리에 맡긴다
            _rigidbody.linearVelocity = new Vector2(_moveInput * _moveSpeed, _rigidbody.linearVelocity.y);
        }

        private void Jump()
        {
            _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 0f);
            _rigidbody.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
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
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_groundCheck == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_groundCheck.position, _groundCheckSize);
        }
#endif
    }
}
