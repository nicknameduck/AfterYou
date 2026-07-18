using UnityEngine;

namespace AfterYou.Core
{
    /// <summary>
    /// 캐릭터 정체성(무거운형/가벼운형/…)의 데이터 정의.
    /// </summary>
    /// <remarks>
    /// 확정 규칙: 정체성은 enum이 아니라 ScriptableObject다.
    /// 새 정체성을 추가할 때 코드를 고치지 않고 에셋만 만들면 되도록 하기 위함이며,
    /// Phase 3의 운반형/벽타기형도 같은 에셋으로 확장된다.
    ///
    /// CharacterActor.Awake가 이 에셋을 읽어 PlayerController에 주입한다(ApplyIdentity).
    /// </remarks>
    [CreateAssetMenu(fileName = "Identity_", menuName = "AfterYou/Identity Data")]
    public class IdentityData : ScriptableObject
    {
        [Header("표시")]
        [SerializeField] private string _displayName = "Unnamed";

        [Tooltip("캐릭터 스프라이트 색. 크기 차등은 두지 않고 색으로만 정체성을 구분한다.")]
        [SerializeField] private Color _tintColor = Color.white;

        [Header("이동")]
        [SerializeField] private float _moveSpeed = 7f;

        [Tooltip("점프 임펄스. 도달고 h = jumpForce^2 / (2 * g * gravityScale) = jumpForce^2 / 58.86 (mass 1, gravityScale 3 기준).\n" +
                 "'높이 못 간다'는 특성은 오직 이 값으로만 표현한다 — Rigidbody2D.mass를 건드리면 안 된다.")]
        [SerializeField] private float _jumpForce = 14f;

        [Header("정체성 규칙")]
        [Tooltip("압력판 판정 전용 순수 데이터. Rigidbody2D.mass가 '아니다'.\n" +
                 "mass에 대입하면 AddForce(Impulse) 기반 점프의 Δv = jumpForce / mass 가 되어 도달고가 붕괴한다.\n" +
                 "압력판은 위에 선 캐릭터들의 Weight 합이 요구 무게 이상일 때만 열린다.")]
        [SerializeField] private float _weight = 1f;

        [Tooltip("접지 판정 마스크 — '클론을 밟고 올라설 수 있는가'를 결정한다.\n" +
                 "가벼운형 = Ground|Clone (768) → 남의 등을 밟고 점프할 수 있다.\n" +
                 "무거운형 = Ground (256) → 클론 위에 물리적으로 서 있을 수는 있으나 점프는 되지 않는다.")]
        [SerializeField] private LayerMask _groundMask;

        [Tooltip("운반형 고유 동사 '옮긴다' 게이트 — 박스 밀기 가능 여부.\n" +
                 "라이브가 이 정체성이면 그 라운드에서만 박스가 Dynamic이 되어 밀 수 있고, 궤적이 기록된다.")]
        [SerializeField] private bool _canManipulateObjects;

        public string DisplayName => _displayName;
        public Color TintColor => _tintColor;
        public float MoveSpeed => _moveSpeed;
        public float JumpForce => _jumpForce;

        /// <summary>압력판 판정 전용 무게. Rigidbody2D.mass와 무관한 순수 데이터다.</summary>
        public float Weight => _weight;

        /// <summary>접지 판정에 쓰는 레이어 마스크. Clone 레이어 포함 여부가 곧 "남의 등을 밟을 수 있는가"다.</summary>
        public LayerMask GroundMask => _groundMask;

        /// <summary>박스 밀기 가능 여부(운반형 게이트). 라이브가 이 정체성일 때만 그 라운드 박스가 Dynamic으로 기록된다.</summary>
        public bool CanManipulateObjects => _canManipulateObjects;
    }
}
