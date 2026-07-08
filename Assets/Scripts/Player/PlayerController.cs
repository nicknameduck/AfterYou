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
