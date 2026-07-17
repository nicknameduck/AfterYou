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

        /// <summary>현재 열려 있는가. 닫힘이 기본 상태다.</summary>
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            // 씬 시작 시 시각/물리 상태를 코드 상태(닫힘)와 강제로 일치시킨다.
            // 인스펙터에서 콜라이더를 꺼둔 채 저장하면 "닫혔는데 통과되는" 유령 버그가 된다.
            ApplyState(false);
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

            if (_renderer != null)
                _renderer.color = isOpen ? _openColor : _closedColor;
        }
    }
}
