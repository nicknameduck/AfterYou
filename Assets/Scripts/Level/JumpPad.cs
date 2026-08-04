using System.Collections.Generic;
using AfterYou.Clone;
using AfterYou.Core;
using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>
    /// 밟으면 캐릭터를 수직으로 발사하는 발판. 정체성 무게(Weight)가 무거울수록 발사 속도가 깎여 낮게 뜬다.
    /// </summary>
    /// <remarks>
    /// 중앙 틱 규약: 자체 Update/FixedUpdate가 없다 — RoundManager가 DriveGimmickTick(tick)으로 구동한다.
    /// 실시간 API(Time.time 등)는 쓰지 않는다. 플래시 길이도 Time.fixedDeltaTime으로 틱 수를 환산해 센다.
    ///
    /// 무상태 설계: 발사 자체는 아무 상태도 남기지 않는다(유일한 상태는 시각 플래시 카운터).
    ///  그래서 패드 위에 계속 서 있으면(vy ≈ 0) 매 틱 다시 발사되는 트램펄린 동작이 나온다 — 의도된 사양이다.
    ///  발사한 다음 틱은 vy > ε(상승 중)라 같은 도약 안에서 이중 발사가 겹치지는 않는다.
    ///
    /// IActivatable은 구현하지 않는다 — 스위치로 켜고 끄는 배선 대상이 아니라 항상 살아 있는 지형 기믹이다.
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class JumpPad : MonoBehaviour, ITickGimmick
    {
        /// <summary>"하강 또는 정지"로 인정할 수직 속도 상한. 이 값보다 빠르게 상승 중이면 발사하지 않는다.</summary>
        private const float DescendingVelocityEpsilon = 0.01f;

        [Header("Detection")]
        [Tooltip("검출 레이어. 라이브(Default=1)만. 클론(512)은 일부러 뺐다.\n" +
                 "클론은 Kinematic + MovePosition 재생이라 속도 발사가 물리적으로 먹지 않고, 애초에 불필요하다 " +
                 "— 라이브 시절 발사된 궤적이 그대로 기록되어 재생이 발사를 자동 재현한다.\n" +
                 "⚠ CrumblingPlatform의 513을 보고 '통일'하지 말 것. 513으로 바꾸면 클론이 검출 대상에 들어오는데, " +
                 "클론은 velocity가 항상 0이라 조건을 통과해 매 틱 의미 없는 재발사 시도만 낭비된다.")]
        [SerializeField] private LayerMask _detectionMask = 1;

        [Tooltip("발판 윗면 위로 띄울 검출 박스의 두께.")]
        [SerializeField] private float _detectionHeight = 0.1f;

        [Tooltip("발바닥이 발판 윗면보다 이만큼 아래여도 '올라섰다'고 인정한다.")]
        [SerializeField] private float _standTolerance = 0.08f;

        [Header("Launch")]
        [Tooltip("무게 1 기준 발사 속도. 도달고 h = launchSpeed^2 / (2 * g * gravityScale) = launchSpeed^2 / 58.86.")]
        [SerializeField] private float _baseLaunchSpeed = 18f;

        [Tooltip("무게 1 초과분 1당 깎을 발사 속도. 정체성별 발사고 차등이 여기서 나온다.")]
        [SerializeField] private float _weightPenaltyPerUnit = 0.8f;

        [Tooltip("아무리 무거워도 이 속도 아래로는 내려가지 않는다(무게가 커도 최소한 튕기긴 해야 읽힌다).")]
        [SerializeField] private float _minLaunchSpeed = 5f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("평상시 색.")]
        [SerializeField] private Color _baseColor = new Color(0.2f, 0.8f, 0.8f, 1f);

        [Tooltip("발사 순간 번쩍이는 색. '지금 튕겼다'를 눈으로 확인시킨다.")]
        [SerializeField] private Color _flashColor = new Color(0.65f, 1f, 1f, 1f);

        [Tooltip("플래시 지속 시간(초). 틱 수로 환산해 센다 — 실시간 시계를 쓰지 않는다.")]
        [SerializeField] private float _flashSeconds = 0.15f;

        private Collider2D _padCollider;
        private ContactFilter2D _contactFilter;

        /// <summary>점유 질의 결과 버퍼(non-alloc 재사용).</summary>
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

        /// <summary>플래시가 끝나기까지 남은 틱. 이 컴포넌트의 유일한 상태다.</summary>
        private int _flashRemainingTicks;

        private void Awake()
        {
            _padCollider = GetComponent<Collider2D>();

            _contactFilter = new ContactFilter2D { useTriggers = false };
            _contactFilter.SetLayerMask(_detectionMask);

            if (_renderer != null)
                _renderer.color = _baseColor;
        }

        /// <summary>중앙 틱: 플래시를 먼저 한 틱 소비하고, 그 다음 윗면 점유자를 발사한다.</summary>
        /// <remarks>
        /// 순서가 중요하다 — 발사가 먼저면 이번 틱에 래치한 플래시를 그 자리에서 1틱 깎아버린다.
        /// </remarks>
        public void DriveGimmickTick(int tick)
        {
            TickFlash();
            LaunchOccupants();
        }

        /// <summary>라운드/보드 리셋: 플래시 카운터와 색을 초기 상태로 되돌린다(그 외 상태 없음).</summary>
        public void ResetGimmick()
        {
            _flashRemainingTicks = 0;

            if (_renderer != null)
                _renderer.color = _baseColor;
        }

        /// <summary>발판 윗면에 올라선 라이브 전원을 수직 발사한다(같은 틱에 여럿이면 각각 적용).</summary>
        private void LaunchOccupants()
        {
            Bounds bounds = _padCollider.bounds;
            float top = bounds.max.y;

            Vector2 boxCenter = new Vector2(bounds.center.x, top + _detectionHeight * 0.5f);
            Vector2 boxSize = new Vector2(bounds.size.x, _detectionHeight);

            Physics2D.OverlapBox(boxCenter, boxSize, 0f, _contactFilter, _overlapBuffer);

            for (int i = 0; i < _overlapBuffer.Count; i++)
            {
                Collider2D other = _overlapBuffer[i];
                if (other == null) continue;

                CharacterActor actor = other.GetComponentInParent<CharacterActor>();
                if (actor == null) continue;

                // 발바닥이 윗면 근처에 있어야 한다(옆구리로 스치는 것은 발사가 아니다).
                if (other.bounds.min.y < top - _standTolerance) continue;

                // 핫패스라 GetComponent를 쓰지 않는다 — 콜라이더가 이미 자기 바디를 캐시하고 있다.
                Rigidbody2D rigidbody = other.attachedRigidbody;
                if (rigidbody == null) continue;

                // 하강/정지 중일 때만 발사한다(상승 중 재발사 방지).
                // ⚠ "속도 기반 판정 금지" 제약은 클론(vy가 항상 0)이 섞여 들어오는 질의에 대한 것이다.
                //   이 질의는 마스크 1로 라이브(Dynamic)만 고르므로 velocity가 진짜 물리 권위 값이고,
                //   따라서 제약 적용 범위 밖이다(KillZone의 라이브 전용 규약과 같은 성격).
                if (rigidbody.linearVelocity.y > DescendingVelocityEpsilon) continue;

                // x는 보존한다 — 수평 관성을 지우면 발사 후 렛지로 건너갈 수 없다.
                rigidbody.linearVelocity = new Vector2(rigidbody.linearVelocity.x, ComputeLaunchSpeed(actor));
                LatchFlash();
            }
        }

        /// <summary>정체성 무게로 발사 속도를 깎는다. 정체성 미할당이면 무게 1로 본다.</summary>
        private float ComputeLaunchSpeed(CharacterActor actor)
        {
            IdentityData identity = actor.Identity;
            float weight = identity != null ? identity.Weight : 1f;

            return Mathf.Max(_minLaunchSpeed, _baseLaunchSpeed - (weight - 1f) * _weightPenaltyPerUnit);
        }

        /// <summary>발사 틱에 플래시를 래치한다. 초를 틱 수로 환산해 실시간 시계를 배제한다.</summary>
        private void LatchFlash()
        {
            _flashRemainingTicks = Mathf.Max(1, Mathf.CeilToInt(_flashSeconds / Time.fixedDeltaTime));

            if (_renderer != null)
                _renderer.color = _flashColor;
        }

        /// <summary>플래시를 한 틱 소비하고, 0이 되면 평상시 색으로 되돌린다.</summary>
        private void TickFlash()
        {
            if (_flashRemainingTicks <= 0) return;

            _flashRemainingTicks--;

            if (_flashRemainingTicks <= 0 && _renderer != null)
                _renderer.color = _baseColor;
        }
    }
}
