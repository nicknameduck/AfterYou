using System.Collections.Generic;
using AfterYou.Clone;
using AfterYou.Core;
using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>
    /// 홀드 스위치형 압력판. 위에 선 캐릭터들의 Weight 합이 요구 무게 이상인 동안에만 문이 열린다.
    /// </summary>
    /// <remarks>
    /// 확정 규칙:
    ///  - 홀드다. 계속 밟고 있어야 열려 있고, 떼면 즉시 닫힌다(토글이 아니다).
    ///  - 라이브든 클론이든 누를 수 있다. 라이브는 Default(0), 클론은 Clone(9) 레이어이므로
    ///    검출 마스크는 두 레이어를 모두 포함해야 한다(Default 1 | Clone 512 = 513).
    ///
    /// 왜 물리 질의(OverlapBox)인가 — RoundManager._characters 배열을 순회하지 않는 이유:
    ///  대기(Idle) 캐릭터는 SetActive(false)로 "월드에 존재하지 않는다"는 게 Phase 1의 규약이다.
    ///  배열을 순회하면 씬에 없는 캐릭터까지 세어 이 규약이 깨진다. 월드에 실제로 있는 것만 물어본다.
    ///
    /// 왜 속도가 아니라 기하로 판정하는가:
    ///  클론은 Kinematic + MovePosition이라 linearVelocity가 항상 0으로 읽힌다.
    ///  속도/착지 이벤트 기반 판정은 클론에서 무조건 실패한다. 그래서 "발바닥이 판 윗면 높이에 있는가"라는
    ///  순수 기하 조건만 쓴다 — 라이브(Dynamic)와 클론(Kinematic)에 동일하게 성립하는 유일한 기준이다.
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class PressurePlate : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("검출 레이어. 라이브(Default) + 클론(Clone) 둘 다 포함해야 한다 → 513.")]
        [SerializeField] private LayerMask _detectionMask;

        [Tooltip("판 윗면 위로 띄울 검출 박스의 두께.")]
        [SerializeField] private float _detectionHeight = 0.1f;

        [Tooltip("발바닥이 판 윗면보다 이만큼 아래여도 '올라섰다'고 인정한다. 물리 솔버의 미세한 파고듦 흡수용.")]
        [SerializeField] private float _standTolerance = 0.08f;

        [Header("Motion")]
        [Tooltip("눌렸을 때 판이 내려가는 깊이. 프리팹 배치는 '올라온' 상태 기준이며, 눌리면 이만큼 내려가 지면과 맞닿는다.")]
        [SerializeField] private float _pressDepth = 0.06f;

        [Tooltip("판 이동 속도(유닛/초). 순수 연출 — 판정은 고정 앵커를 쓰므로 게임 규칙에 영향 없다.")]
        [SerializeField] private float _moveSpeed = 0.6f;

        [Header("Rule")]
        [Tooltip("문을 열기 위해 필요한 Weight 합. 무거운형(10) 1기 = 개방, 가벼운형(1) 3기 = 합 3 < 10 → 개방 불가.")]
        [SerializeField] private float _requiredWeight = 10f;

        [Header("Link")]
        [SerializeField] private Door _door;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _releasedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private Color _pressedColor = new Color(0.3f, 0.9f, 0.4f, 1f);

        /// <summary>
        /// OverlapBox 결과 버퍼. 매 FixedUpdate마다 질의하므로 배열 반환 오버로드(Physics2D.OverlapBoxAll)를
        /// 쓰면 프레임마다 GC 할당이 발생한다. List를 재사용하는 non-alloc 오버로드로 할당 0을 유지한다.
        /// </summary>
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

        private ContactFilter2D _contactFilter;
        private Collider2D _plateCollider;

        /// <summary>연출용 이동의 양끝 위치. 프리팹 배치 위치가 '올라온' 상태다.</summary>
        private Vector3 _raisedLocalPos;
        private Vector3 _pressedLocalPos;

        /// <summary>감지 앵커(= 눌린 상태의 판 윗면 = 지면 높이). 판이 연출로 움직여도 판정이 흔들리지 않도록
        /// 고정 좌표를 캐시해 쓴다 — Update 이동(프레임레이트 의존)이 판정에 새어들면 틱 결정성이 깨진다.</summary>
        private bool _isAnchorCached;
        private float _anchorTop;
        private float _anchorCenterX;
        private float _anchorWidth;

        /// <summary>현재 눌려 있는가(= Weight 합 >= 요구 무게).</summary>
        public bool IsPressed { get; private set; }

        /// <summary>이 판에 연동된 문. 없으면 null. 리플레이 사전 스캔이 "판 눌림 = 문 열림" 고리를 세는 데 쓴다.</summary>
        public Door LinkedDoor => _door;

        /// <summary>
        /// 눌리지 않음 → 눌림으로 전환된 순간에 1회 발화한다(뗄 때는 발화하지 않는다).
        /// </summary>
        /// <remarks>
        /// ⚠ 핸들러에서 상태 전환 메서드(SelectCharacter/ConfirmClone/RestartTake 등)를 호출하지 말 것.
        ///   이 이벤트는 FixedUpdate 내부 = 중앙 틱 위상 안에서 발화하므로, 핸들러가 상태를 바꾸면
        ///   같은 틱에서 기믹/클론 구동이 어긋난다. 연출·큐잉 전용이다.
        /// </remarks>
        public event System.Action OnPressed;

        private void Awake()
        {
            _plateCollider = GetComponent<Collider2D>();

            _raisedLocalPos = transform.localPosition;
            _pressedLocalPos = _raisedLocalPos + Vector3.down * _pressDepth;

            // useTriggers=false: Physics2DSettings의 QueriesHitTriggers가 1이라 기본값이면 트리거(Exit 등)도 잡힌다.
            // 캐릭터 콜라이더는 모두 비트리거이므로 여기서 걸러도 손해가 없다.
            _contactFilter = new ContactFilter2D
            {
                useTriggers = false
            };
            _contactFilter.SetLayerMask(_detectionMask);

            ApplyVisual(false);
        }

        private void FixedUpdate()
        {
            // 앵커는 첫 FixedUpdate에 캐시한다. Awake 시점의 Collider2D.bounds는 물리 등록 전이라 신뢰할 수 없고,
            // 첫 FixedUpdate까지는 판이 시작(올라온) 위치에 있음이 보장된다 — 이동은 눌림 이후 Update에서만 일어난다.
            if (!_isAnchorCached)
            {
                Bounds startBounds = _plateCollider.bounds;
                _anchorTop = startBounds.max.y - _pressDepth; // 눌린 상태의 윗면 = 지면 높이
                _anchorCenterX = startBounds.center.x;
                _anchorWidth = startBounds.size.x;
                _isAnchorCached = true;
            }

            float totalWeight = MeasureWeightOnPlate();
            bool shouldBePressed = totalWeight >= _requiredWeight;

            // 상태가 "바뀐 순간"에만 문에 통지한다. 매 틱 SetOpen을 때리면 불필요한 색/콜라이더 재설정이 반복된다.
            if (shouldBePressed == IsPressed) return;

            IsPressed = shouldBePressed;
            ApplyVisual(IsPressed);

            if (_door != null)
                _door.SetOpen(IsPressed);

            if (IsPressed)
                OnPressed?.Invoke();
        }

        private void Update()
        {
            // 연출 전용 이동. 판정은 고정 앵커(_anchorTop)를 쓰므로 이 움직임은 게임 규칙에 영향을 주지 않는다.
            Vector3 target = IsPressed ? _pressedLocalPos : _raisedLocalPos;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, _moveSpeed * Time.deltaTime);
        }

        /// <summary>판 위에 실제로 서 있는 캐릭터들의 Weight 합을 잰다. 판정 기준은 고정 앵커다(연출 이동과 무관).</summary>
        private float MeasureWeightOnPlate()
        {
            float plateTop = _anchorTop;

            Vector2 boxCenter = new Vector2(_anchorCenterX, plateTop + _detectionHeight * 0.5f);
            Vector2 boxSize = new Vector2(_anchorWidth, _detectionHeight);

            Physics2D.OverlapBox(boxCenter, boxSize, 0f, _contactFilter, _overlapBuffer);

            float totalWeight = 0f;

            for (int i = 0; i < _overlapBuffer.Count; i++)
            {
                Collider2D other = _overlapBuffer[i];
                if (other == null) continue;

                // 박스 분기: 캐릭터 필터보다 먼저 본다. CharacterActor null-continue 뒤에 두면 박스가 걸러진다.
                // 박스도 캐릭터와 동일한 기하 조건(밑면이 판 윗면 높이)일 때만 Weight를 합산한다.
                PushableBox box = other.GetComponentInParent<PushableBox>();
                if (box != null)
                {
                    if (other.bounds.min.y >= plateTop - _standTolerance)
                        totalWeight += box.Weight;
                    continue;
                }

                // 2차 필터: 레이어만으로는 부족하다. 콜라이더가 자식(GroundCheck 등)에 붙어 있을 수 있으므로
                // 부모까지 거슬러 CharacterActor를 찾는다(LevelExit과 동일 패턴).
                CharacterActor actor = other.GetComponentInParent<CharacterActor>();
                if (actor == null) continue;

                IdentityData identity = actor.Identity;
                if (identity == null) continue;

                // 3차 기하 필터: 발바닥이 판 윗면 높이에 있어야 "올라섰다".
                // 옆으로 스치거나, 점프로 검출 박스를 관통하는 순간(발바닥이 윗면보다 한참 아래)을 배제한다.
                if (other.bounds.min.y < plateTop - _standTolerance) continue;

                totalWeight += identity.Weight;
            }

            return totalWeight;
        }

        /// <summary>
        /// "지금 이 배치라면 판이 눌리는가"만 묻는다. 부수효과 0 — 상태(IsPressed)/문/연출을 전혀 건드리지 않는다.
        /// </summary>
        /// <remarks>
        /// 리플레이 사전 스캔 전용. 캐스트를 각 틱 위치에 배치한 뒤 호출해 눌림 틱을 미리 뽑는다.
        /// 판정식은 FixedUpdate와 동일한 MeasureWeightOnPlate를 그대로 재사용한다(로직 복제 금지 — 두 벌이 되면 갈라진다).
        /// </remarks>
        public bool EvaluatePressed() => MeasureWeightOnPlate() >= _requiredWeight;

        private void ApplyVisual(bool isPressed)
        {
            if (_renderer != null)
                _renderer.color = isPressed ? _pressedColor : _releasedColor;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Collider2D plateCollider = _plateCollider != null ? _plateCollider : GetComponent<Collider2D>();
            if (plateCollider == null) return;

            Bounds plateBounds = plateCollider.bounds;
            // 에디트 모드(앵커 미캐시)에서는 배치 위치가 '올라온' 상태이므로 눌림 깊이를 빼서 실제 판정 위치를 그린다.
            float plateTop = _isAnchorCached ? _anchorTop : plateBounds.max.y - _pressDepth;
            Vector3 boxCenter = new Vector3(plateBounds.center.x, plateTop + _detectionHeight * 0.5f, 0f);
            Vector3 boxSize = new Vector3(plateBounds.size.x, _detectionHeight, 0f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(boxCenter, boxSize);
        }
#endif
    }
}
