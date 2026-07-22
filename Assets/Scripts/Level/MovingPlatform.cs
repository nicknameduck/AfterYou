using System.Collections.Generic;
using AfterYou.Clone;
using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>
    /// base ↔ base+offset 사이를 왕복하는 움직이는 발판. Kinematic Rigidbody2D + Ground 레이어.
    /// </summary>
    /// <remarks>
    /// 결정성이 핵심이다. 위치는 "위상(_phaseTicks)의 순수 함수"다 — 실시간이 아니라 틱으로만 굴러간다.
    /// 활성(_isActive) 중에만 위상이 +1 되고, 위상이 같으면 위치도 항상 같다 → 라운드마다 완벽히 재현된다.
    /// tick+1 규약: ClonePlayback/PushableBox와 동일하게, FixedUpdate는 물리 이전이라 목표는 다음 위상이다.
    ///
    /// ⚠ 라이브 캐리는 반드시 "위치 가산"으로 한다 — 속도 기반은 금지다.
    ///   PlayerController.FixedUpdate가 매 스텝 linearVelocity.x를 통째로 덮어쓰므로(150줄),
    ///   발판이 속도로 태우려 하면 실행 순서에 따라 지워져 라이브가 미끄러진다.
    ///   그래서 발판이 이번 스텝에 이동한 delta를 계산해, 윗면에 서 있는 라이브의 위치에 직접 더한다.
    ///   캐릭터 쪽 코드는 건드리지 않고 CharacterActor의 공개 API(Position / SetPosition)만 쓴다.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public class MovingPlatform : MonoBehaviour, ITickGimmick, IActivatable
    {
        [Header("Motion")]
        [Tooltip("base 위치에서의 이동 벡터. base ↔ base+offset 사이를 왕복한다.")]
        [SerializeField] private Vector2 _localOffset = new Vector2(3f, 0f);

        [Tooltip("편도(base → base+offset) 이동에 걸리는 시간(초).")]
        [SerializeField] private float _travelSeconds = 2f;

        [Tooltip("시작 시 가동 여부. SetActivated로 스위치에 연동할 수도 있다(미연결이면 상시 가동).")]
        [SerializeField] private bool _isActiveAtStart = true;

        [Header("Rider Carry")]
        [Tooltip("발판에 태울 대상 레이어. Default(1)만 = 라이브. 클론(512)/박스(2048)는 자체 궤적으로 재생되므로 제외한다.")]
        [SerializeField] private LayerMask _riderMask = 1;

        [Tooltip("발판 윗면 위로 띄울 탑승자 검출 박스의 두께.")]
        [SerializeField] private float _riderCheckHeight = 0.1f;

        [Tooltip("발바닥이 발판 윗면보다 이만큼 아래여도 '탔다'고 인정한다.")]
        [SerializeField] private float _riderTolerance = 0.08f;

        /// <summary>탑승자 질의 결과 버퍼(non-alloc 재사용).</summary>
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

        private Rigidbody2D _rigidbody;
        private Collider2D _collider;
        private ContactFilter2D _riderFilter;

        /// <summary>Awake 시점 물리 위치. 왕복의 기준점이다.</summary>
        private Vector2 _basePosition;

        /// <summary>가동 위상. 활성 중에만 증가한다. 위치는 이 값의 순수 함수다.</summary>
        private int _phaseTicks;

        /// <summary>현재 가동 중인가. false면 위상이 멈춰 발판이 제자리에 선다.</summary>
        private bool _isActive;

        /// <summary>현재 가동 중인가(IActivatable 계약).</summary>
        public bool IsActivated => _isActive;

        /// <summary>편도 이동에 필요한 틱 수. 0 나눗셈을 막기 위해 최소 1.</summary>
        private int TravelTicks => Mathf.Max(1, Mathf.CeilToInt(_travelSeconds / Time.fixedDeltaTime));

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();

            _basePosition = _rigidbody.position;
            _isActive = _isActiveAtStart;

            _riderFilter = new ContactFilter2D { useTriggers = false };
            _riderFilter.SetLayerMask(_riderMask);
        }

        /// <summary>중앙 틱: 다음 위상 위치로 MovePosition하고, 그 이동분만큼 탑승자를 함께 옮긴다.</summary>
        public void DriveGimmickTick(int tick)
        {
            // 다음 위상(위상+1) 위치를 목표로 삼는다 — 클론 frames[t+1]과 동일한 tick+1 규약.
            // 비활성이면 위상이 멈춰 현재 위치를 유지한다.
            int targetPhase = _isActive ? _phaseTicks + 1 : _phaseTicks;
            Vector2 target = PositionForPhase(targetPhase);

            Vector2 delta = target - _rigidbody.position;
            _rigidbody.MovePosition(target);

            if (delta != Vector2.zero)
                CarryRiders(delta);

            if (_isActive)
                _phaseTicks++;
        }

        /// <summary>라운드/보드 리셋: 위상 0 + base 복귀 + 가동 상태를 초기값으로.</summary>
        public void ResetGimmick()
        {
            _phaseTicks = 0;
            _isActive = _isActiveAtStart;

            // base로 순간 복귀. Interpolate/AutoSync 스미어를 피하려고 rb와 transform을 함께 맞춘다(PushableBox.ResetToBase 패턴).
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.position = _basePosition;
            transform.position = new Vector3(_basePosition.x, _basePosition.y, transform.position.z);
        }

        /// <summary>가동/정지 토글. 스위치(IActivatable)에 연동할 때 쓴다.</summary>
        public void SetActivated(bool isActive)
        {
            _isActive = isActive;
        }

        /// <summary>위상 → 월드 위치. PingPong으로 base↔base+offset 왕복을 보간한다.</summary>
        private Vector2 PositionForPhase(int phase)
        {
            // PingPong((float)phase / TravelTicks, 1) : 0→1→0→1... 왕복. 한 사이클 = 2*TravelTicks.
            float t = Mathf.PingPong((float)phase / TravelTicks, 1f);
            return _basePosition + _localOffset * t;
        }

        /// <summary>발판 윗면에 서 있는 라이브를 이번 스텝 이동분(delta)만큼 위치 가산으로 태운다.</summary>
        private void CarryRiders(Vector2 delta)
        {
            Bounds platformBounds = _collider.bounds;
            float platformTop = platformBounds.max.y;

            Vector2 boxCenter = new Vector2(platformBounds.center.x, platformTop + _riderCheckHeight * 0.5f);
            Vector2 boxSize = new Vector2(platformBounds.size.x, _riderCheckHeight);

            Physics2D.OverlapBox(boxCenter, boxSize, 0f, _riderFilter, _overlapBuffer);

            for (int i = 0; i < _overlapBuffer.Count; i++)
            {
                Collider2D other = _overlapBuffer[i];
                if (other == null) continue;

                // 콜라이더가 자식(GroundCheck 등)에 붙어 있을 수 있으므로 부모까지 거슬러 찾는다.
                // (알려진 제약, 이번 회차 미최적화) 매 틱 GetComponentInParent 호출 — 핫패스지만 현 스코프에선 방치.
                CharacterActor actor = other.GetComponentInParent<CharacterActor>();
                if (actor == null) continue;

                // 발바닥이 발판 윗면 높이에 있어야 "탔다".
                if (other.bounds.min.y < platformTop - _riderTolerance) continue;

                actor.SetPosition(actor.Position + delta);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // base ↔ base+offset 왕복 경로를 에디터에서 눈으로 확인한다.
            Vector3 basePos = Application.isPlaying ? (Vector3)_basePosition : transform.position;
            Vector3 endPos = basePos + (Vector3)_localOffset;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(basePos, endPos);
            Gizmos.DrawWireSphere(endPos, 0.15f);
        }
#endif
    }
}
