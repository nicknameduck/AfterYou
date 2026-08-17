using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>
    /// 스위치(IActivatable)에 연동되는 문. 열림 = 콜라이더를 꺼서 통행 허용, 닫힘 = 통행 차단.
    /// 열린 뒤 _openSeconds가 지나면 자동으로 닫힌다(조기 닫힘은 SetActivated(false)로 가능).
    /// </summary>
    /// <remarks>
    /// Door와 콜라이더/색 토글 방식은 동일하다. 차이는 두 가지다:
    ///  - 여는 주체가 압력판이 아니라 IActivatable 통지(ToggleSwitch 등)다.
    ///  - 자동 닫힘 타이머가 있다. ⚠ 타이머는 실시간이 아니라 "틱"으로 센다 — Time.time 류 실시간 API를
    ///    쓰면 클론 재생과 어긋나 결정성이 깨진다. _openSeconds를 Time.fixedDeltaTime으로 틱 수로 환산해
    ///    DriveGimmickTick마다 1씩 깎는다.
    ///
    /// Door와 동일한 확정 제약: 클론은 닫힌 문을 그대로 통과한다(Kinematic MovePosition). 레벨 설계로 회피한다.
    /// SetActive(false)로 문을 열지 않는다 — 콜라이더 on/off로만 제어한다(비활성 오브젝트는 통지를 못 받는다).
    /// </remarks>
    public class TimedDoor : MonoBehaviour, IActivatable, ITickGimmick
    {
        [Tooltip("통행 차단용 콜라이더. 열림 = enabled false.")]
        [SerializeField] private Collider2D _collider;

        [SerializeField] private SpriteRenderer _renderer;

        [Header("Visual")]
        [SerializeField] private Color _closedColor = new Color(0.55f, 0.32f, 0.18f, 1f);

        [Tooltip("열렸을 때 색. 알파를 낮춰 '통과 가능'을 읽히게 한다.")]
        [SerializeField] private Color _openColor = new Color(0.55f, 0.32f, 0.18f, 0.2f);

        [Tooltip("색 페이드 시간(초). Door와 동일한 값으로 개폐 연출을 통일한다.")]
        [SerializeField] private float _fadeDuration = 0.1f;

        [Header("Timing")]
        [Tooltip("열린 뒤 자동으로 닫히기까지의 시간(초).")]
        [SerializeField] private float _openSeconds = 3f;

        /// <summary>현재 열려 있는가. 닫힘이 기본 상태다.</summary>
        private bool _isOpen;

        /// <summary>자동 닫힘까지 남은 틱. 0이면 이미 닫힘.</summary>
        private int _remainingTicks;

        /// <summary>현재 열려 있는가(IActivatable 계약). 스위치의 자기 동기화가 읽는다.</summary>
        public bool IsActivated => _isOpen;

        /// <summary>페이드가 향하는 목표 색. Update가 매 프레임 이 색으로 수렴시킨다(Door와 동일 패턴).</summary>
        private Color _targetColor;

        private void Awake()
        {
            // 씬 시작 시 시각/물리 상태를 코드 상태(닫힘)와 강제로 일치시킨다(Door.Awake와 동일).
            ApplyState(false);

            // 시작 상태는 페이드 없이 즉시 일치시킨다 — 씬 로드 직후 어중간한 색에서 출발하지 않도록.
            if (_renderer != null)
                _renderer.color = _targetColor;
        }

        private void Update()
        {
            // 연출 전용 페이드. 콜라이더(게임 판정)와 틱 타이머는 즉시/틱 기반이므로 규칙에 영향 없다.
            if (_renderer == null || _renderer.color == _targetColor) return;

            // MoveTowards의 델타는 "초당 색 거리"다. 닫힘↔열림 전체 거리를 _fadeDuration으로 나눠,
            // 상태 전환 시 정확히 _fadeDuration초에 목표에 도착하는 등속 페이드로 환산한다(Door와 동일).
            float colorDistance = Vector4.Distance(_closedColor, _openColor);
            float speed = _fadeDuration > 0f ? colorDistance / _fadeDuration : float.MaxValue;
            _renderer.color = Vector4.MoveTowards(_renderer.color, _targetColor, speed * Time.deltaTime);
        }

        /// <summary>스위치가 켜지면 열고 자동 닫힘 타이머를 건다. 꺼지면 즉시 닫는다.</summary>
        public void SetActivated(bool isActive)
        {
            if (isActive)
            {
                ApplyState(true);

                // 자동 닫힘 카운터. 실시간이 아니라 틱 수로 환산해 결정적으로 닫는다.
                _remainingTicks = Mathf.CeilToInt(_openSeconds / Time.fixedDeltaTime);
            }
            else
            {
                ApplyState(false);
                _remainingTicks = 0;
            }
        }

        /// <summary>중앙 틱: 열려 있고 타이머가 걸려 있으면 1틱 깎고, 0에 도달하면 닫는다.</summary>
        public void DriveGimmickTick(int tick)
        {
            if (!_isOpen) return;
            if (_remainingTicks <= 0) return;

            _remainingTicks--;
            if (_remainingTicks == 0)
                ApplyState(false);
        }

        /// <summary>라운드/보드 리셋: 닫힘 + 타이머 0.</summary>
        public void ResetGimmick()
        {
            _remainingTicks = 0;
            ApplyState(false);
        }

        private void ApplyState(bool isOpen)
        {
            _isOpen = isOpen;

            if (_collider != null)
                _collider.enabled = !isOpen;

            // 색은 즉시 대입하지 않고 목표만 갱신한다 — 실제 수렴은 Update의 페이드가 담당(Door와 동일).
            _targetColor = isOpen ? _openColor : _closedColor;
        }
    }
}
