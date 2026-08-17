using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>
    /// 압력판에 연동되는 문. 열림 = 콜라이더를 꺼서 통행 허용, 닫힘 = 통행 차단.
    /// </summary>
    /// <remarks>
    /// ⚠ 확정된 설계 제약: <b>클론은 닫힌 문을 그대로 통과한다.</b>
    /// 클론은 Kinematic + Rigidbody2D.MovePosition으로 재생되므로 "무조건 목표 위치로" 이동한다.
    /// 물리 솔버가 막을 수 없다(이건 클론 재생의 권위를 보장하기 위한 의도된 대가다).
    /// → <b>레벨 설계로 회피한다: 문 너머에는 Exit만 둔다.</b>
    ///   클리어 판정은 라이브 캐릭터만 하므로(LevelExit), 클론이 문을 통과해 Exit에 닿아도 아무 일도 일어나지 않는다.
    ///   문 너머에 "클론이 밟고 지나가면 안 되는" 장치(다른 압력판 등)를 두면 퍼즐이 깨진다.
    ///
    /// SetActive(false)로 문을 열지 않는다 — 비활성 오브젝트는 참조가 유실된 것처럼 동작하고
    /// (컴포넌트 콜백이 죽는다) PressurePlate의 통지도 받지 못한다. 콜라이더 on/off로만 제어한다.
    /// </remarks>
    public class Door : MonoBehaviour
    {
        [Tooltip("통행 차단용 콜라이더. 열림 = enabled false.")]
        [SerializeField] private Collider2D _collider;

        [SerializeField] private SpriteRenderer _renderer;

        [Header("Visual")]
        [SerializeField] private Color _closedColor = new Color(0.55f, 0.32f, 0.18f, 1f);

        [Tooltip("열렸을 때 색. 알파를 낮춰 '통과 가능'을 읽히게 한다.")]
        [SerializeField] private Color _openColor = new Color(0.55f, 0.32f, 0.18f, 0.2f);

        [Tooltip("색 페이드 시간(초). 압력판이 내려가는 시간(_pressDepth 0.06 ÷ _moveSpeed 0.6 = 0.1초)과 맞춘 기본값.")]
        [SerializeField] private float _fadeDuration = 0.1f;

        /// <summary>현재 열려 있는가. 닫힘이 기본 상태다.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 문이 열린 순간에 1회 발화한다(닫힐 때는 발화하지 않는다). Awake의 초기 ApplyState(false)도 발화하지 않는다.
        /// </summary>
        /// <remarks>
        /// ⚠ 핸들러에서 상태 전환 메서드를 호출하지 말 것. PressurePlate.FixedUpdate = 중앙 틱 위상 안에서
        ///   연쇄 발화되므로, 핸들러가 상태를 바꾸면 같은 틱에서 위상이 어긋난다. 연출·큐잉 전용이다.
        /// </remarks>
        public event System.Action OnOpened;

        /// <summary>페이드가 향하는 목표 색. Update가 매 프레임 이 색으로 수렴시킨다.</summary>
        private Color _targetColor;

        private void Awake()
        {
            // 씬 시작 시 시각/물리 상태를 코드 상태(닫힘)와 강제로 일치시킨다.
            // 인스펙터에서 콜라이더를 꺼둔 채 저장하면 "닫혔는데 통과되는" 유령 버그가 된다.
            ApplyState(false);

            // 시작 상태는 페이드 없이 즉시 일치시킨다 — 씬 로드 직후 닫힌 문이 어중간한 색에서 출발하지 않도록.
            if (_renderer != null)
                _renderer.color = _targetColor;
        }

        private void Update()
        {
            // 연출 전용 페이드. 콜라이더(게임 판정)는 ApplyState에서 즉시 전환되므로 규칙에 영향 없다.
            if (_renderer == null || _renderer.color == _targetColor) return;

            // MoveTowards의 델타는 "초당 색 거리"다. 닫힘↔열림 전체 거리를 _fadeDuration으로 나눠,
            // 상태 전환 시 정확히 _fadeDuration초에 목표에 도착하는 등속 페이드로 환산한다.
            float colorDistance = Vector4.Distance(_closedColor, _openColor);
            float speed = _fadeDuration > 0f ? colorDistance / _fadeDuration : float.MaxValue;
            _renderer.color = Vector4.MoveTowards(_renderer.color, _targetColor, speed * Time.deltaTime);
        }

        /// <summary>
        /// 문을 열거나 닫는다. PressurePlate가 "상태가 바뀐 순간에만" 호출한다.
        /// </summary>
        public void SetOpen(bool isOpen)
        {
            if (IsOpen == isOpen) return;
            ApplyState(isOpen);
        }

        private void ApplyState(bool isOpen)
        {
            IsOpen = isOpen;

            if (_collider != null)
                _collider.enabled = !isOpen;

            // 색은 즉시 대입하지 않고 목표만 갱신한다 — 실제 수렴은 Update의 페이드가 담당.
            _targetColor = isOpen ? _openColor : _closedColor;

            if (isOpen)
                OnOpened?.Invoke();
        }
    }
}
