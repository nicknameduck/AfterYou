using System.Collections.Generic;
using AfterYou.Clone;
using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>
    /// 밟으면 잠시 후 무너지는 발판. 최초 점유 틱에 카운트다운을 래치하고, 이후 점유 여부와 무관하게
    /// 매 틱 줄여 0에 도달하면 붕괴(콜라이더 off + 페이드 아웃)한다.
    /// 붕괴 후 _respawnSeconds가 지나면 재생성(콜라이더 on + 페이드 인 + 재래치 가능)된다.
    /// </summary>
    /// <remarks>
    /// 중앙 틱 규약: 자체 Update/FixedUpdate 없음 — RoundManager가 DriveGimmickTick으로 구동한다.
    /// 결정성: 카운트다운은 실시간이 아니라 틱으로 센다(_crumbleSeconds를 Time.fixedDeltaTime으로 환산).
    /// 점유 검출 마스크는 513(라이브 Default | 클론 Clone) — 클론 재생이 붕괴 틱을 그대로 재현해야
    ///  라운드 간 결정성이 성립한다(라이브만 잡으면 재생 때 안 무너져 궤적이 어긋난다).
    ///
    /// 1틱 바운스 이중 엣지는 무해하다: "한 번 트리거되면 진행" 래치(_isTriggered)라, 재진입해도
    ///  이미 트리거된 상태이므로 카운트다운만 계속된다(재래치·리셋 없음).
    ///
    /// RoundManager를 참조하지 않는다(통지가 없다). 붕괴/복원은 이 컴포넌트 안에서 완결된다.
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class CrumblingPlatform : MonoBehaviour, ITickGimmick
    {
        /// <summary>붕괴 후 스프라이트 알파(완전 0으로 지우지 않고 흔적만 남겨 위치를 읽히게 한다).</summary>
        private const float CrumbledAlpha = 0.15f;

        [Header("Detection")]
        [Tooltip("검출 레이어. 라이브(Default) + 클론(Clone) 둘 다 → 513. 클론이 붕괴 틱을 재현해야 결정성 성립.")]
        [SerializeField] private LayerMask _detectionMask = 513;

        [Tooltip("발판 윗면 위로 띄울 검출 박스의 두께.")]
        [SerializeField] private float _detectionHeight = 0.1f;

        [Tooltip("발바닥이 발판 윗면보다 이만큼 아래여도 '올라섰다'고 인정한다.")]
        [SerializeField] private float _standTolerance = 0.08f;

        [Header("Crumble")]
        [Tooltip("최초로 밟힌 뒤 붕괴까지의 시간(초).")]
        [SerializeField] private float _crumbleSeconds = 1.5f;

        [Tooltip("붕괴 후 재생성까지의 시간(초).")]
        [SerializeField] private float _respawnSeconds = 5f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _renderer;
        [Tooltip("안정 상태 색.")]
        [SerializeField] private Color _stableColor = new Color(0.6f, 0.55f, 0.4f, 1f);
        [Tooltip("붕괴 임박 경고색. 카운트다운 진행에 따라 이 색으로 보간된다(예측 가능해야 한다).")]
        [SerializeField] private Color _crumblingColor = new Color(0.85f, 0.3f, 0.2f, 1f);

        [Tooltip("붕괴/재생성 색 페이드 시간(초). Door/TimedDoor와 동일한 값으로 소멸 연출을 통일한다.")]
        [SerializeField] private float _fadeDuration = 0.1f;

        private Collider2D _platformCollider;
        private ContactFilter2D _contactFilter;

        /// <summary>점유 질의 결과 버퍼(non-alloc 재사용).</summary>
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

        /// <summary>카운트다운이 시작됐는가(한 번 켜지면 진행). 바운스 이중 엣지 무해화의 핵심.</summary>
        private bool _isTriggered;

        /// <summary>이미 붕괴했는가. 붕괴 후에는 점유 질의를 스킵한다(disabled 콜라이더 bounds는 무효).</summary>
        private bool _hasCrumbled;

        /// <summary>붕괴까지 남은 틱.</summary>
        private int _remainingTicks;

        /// <summary>래치 시점의 총 틱 수(경고색 보간 분모).</summary>
        private int _totalTicks;

        /// <summary>재생성까지 남은 틱. 붕괴 시점에 래치된다.</summary>
        private int _respawnTicks;

        /// <summary>페이드가 향하는 목표 색. Update가 매 프레임 이 색으로 수렴시킨다(Door와 동일 패턴).</summary>
        private Color _targetColor;

        private void Awake()
        {
            _platformCollider = GetComponent<Collider2D>();

            _contactFilter = new ContactFilter2D { useTriggers = false };
            _contactFilter.SetLayerMask(_detectionMask);

            _targetColor = _stableColor;
            if (_renderer != null)
                _renderer.color = _stableColor;
        }

        private void Update()
        {
            // 연출 전용 페이드. 붕괴/재생성 판정(콜라이더·틱 카운트)은 전부 DriveGimmickTick에서
            // 틱 기반으로 끝나므로, 이 Update는 중앙 틱 규약(게임 로직의 자체 구동 금지)을 깨지 않는다.
            if (_renderer == null || _renderer.color == _targetColor) return;

            // 속도 분자는 "가장 먼 전환"(안정색 ↔ 붕괴 잔상)의 색 거리 — 붕괴/재생성 전환이
            // 정확히 _fadeDuration초에 도착하도록 환산한다. 경고색 보간은 틱마다 목표가 조금씩
            // 이동하므로 이 속도면 사실상 즉시 추종한다(연출 차이 없음).
            Color crumbledColor = _crumblingColor;
            crumbledColor.a = CrumbledAlpha;
            float colorDistance = Vector4.Distance(_stableColor, crumbledColor);
            float speed = _fadeDuration > 0f ? colorDistance / _fadeDuration : float.MaxValue;
            _renderer.color = Vector4.MoveTowards(_renderer.color, _targetColor, speed * Time.deltaTime);
        }

        /// <summary>중앙 틱: 최초 점유에 카운트다운을 래치하고, 이후 매 틱 줄여 0에 붕괴한다.
        /// 붕괴 후에는 재생성 카운트다운으로 전환되어 0에 도달하면 초기 상태로 복원된다.</summary>
        public void DriveGimmickTick(int tick)
        {
            if (_hasCrumbled)
            {
                // 붕괴 상태: 점유 질의는 계속 스킵(disabled 콜라이더 bounds 무효), 재생성만 센다.
                // 실시간이 아니라 틱으로 세므로 클론 재생/리플레이에서 같은 틱에 재생성된다(결정성).
                _respawnTicks--;
                if (_respawnTicks <= 0)
                    Respawn();
                return;
            }

            if (!_isTriggered)
            {
                if (!IsOccupied()) return; // 아직 안 밟힘

                _isTriggered = true;
                _totalTicks = Mathf.Max(1, Mathf.CeilToInt(_crumbleSeconds / Time.fixedDeltaTime));
                _remainingTicks = _totalTicks;
            }

            // 트리거된 이후: 점유 여부와 무관하게 매 틱 감소.
            _remainingTicks--;
            UpdateWarningVisual();

            if (_remainingTicks <= 0)
                Crumble();
        }

        /// <summary>라운드/보드 리셋: 콜라이더 복구 + 카운트다운/래치/스프라이트 완전 복원.</summary>
        public void ResetGimmick()
        {
            _isTriggered = false;
            _hasCrumbled = false;
            _remainingTicks = 0;
            _totalTicks = 0;
            _respawnTicks = 0;

            if (_platformCollider != null)
                _platformCollider.enabled = true;

            // 리셋은 페이드 없이 즉시 복원한다 — 라운드 전환 시 잔상에서 서서히 살아나는 연출은
            // "재생성"과 혼동을 부른다(리셋 = 순간 복원, 재생성 = 페이드 인으로 구분).
            _targetColor = _stableColor;
            if (_renderer != null)
                _renderer.color = _stableColor;
        }

        /// <summary>발판 위에 실제로 서 있는 캐릭터가 하나라도 있는가(ToggleSwitch.IsOccupied와 동일 패턴).</summary>
        private bool IsOccupied()
        {
            Bounds bounds = _platformCollider.bounds;
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

                if (other.bounds.min.y < top - _standTolerance) continue;

                return true;
            }

            return false;
        }

        /// <summary>카운트다운 진행도(0→1)에 따라 안정색 → 경고색으로 보간한다.</summary>
        private void UpdateWarningVisual()
        {
            float progress = 1f - (float)_remainingTicks / _totalTicks;
            _targetColor = Color.Lerp(_stableColor, _crumblingColor, progress);
        }

        private void Crumble()
        {
            _hasCrumbled = true;

            // 재생성 카운트다운 래치. 붕괴와 동일하게 틱으로 환산해 결정성을 유지한다.
            _respawnTicks = Mathf.Max(1, Mathf.CeilToInt(_respawnSeconds / Time.fixedDeltaTime));

            if (_platformCollider != null)
                _platformCollider.enabled = false;

            // 즉시 대입하지 않고 목표만 바꾼다 — Update의 페이드가 잔상 알파까지 서서히 수렴시킨다.
            Color color = _crumblingColor;
            color.a = CrumbledAlpha;
            _targetColor = color;
        }

        /// <summary>재생성: 콜라이더 복구 + 래치 해제(다시 밟으면 다시 무너진다) + 안정색으로 페이드 인.</summary>
        /// <remarks>재생성 순간 그 자리에 캐릭터/박스가 있어도 무조건 켠다 — 라이브(Dynamic)는 물리
        /// 솔버가 밀어내고, 클론(Kinematic)은 원래 콜라이더를 통과하므로 끼임이 문제되지 않는다.</remarks>
        private void Respawn()
        {
            _isTriggered = false;
            _hasCrumbled = false;
            _remainingTicks = 0;
            _totalTicks = 0;

            if (_platformCollider != null)
                _platformCollider.enabled = true;

            // 리셋과 달리 페이드 인으로 살아난다(Update가 수렴).
            _targetColor = _stableColor;
        }
    }
}
