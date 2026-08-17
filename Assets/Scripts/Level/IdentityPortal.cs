using System.Collections.Generic;
using AfterYou.Clone;
using AfterYou.Core;
using AfterYou.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterYou.Level
{
    /// <summary>
    /// 허용된 정체성의 라이브 캐릭터가 범위 안에서 위 키(↑/W)를 눌러 짝 포탈로 이동하는 포탈.
    /// 쌍으로 배치해 서로 _linkedPortal로 배선한다. 범위 안에 들어오면 ↑ 프롬프트가 뜬다(LevelExit과 동일 UX).
    /// </summary>
    /// <remarks>
    /// ⚠ 레이어는 반드시 Default(0)여야 한다 — Ground/Clone/Climbable에 두면 PlayerController의 접지 질의
    ///  (QueriesHitTriggers=1이라 트리거도 잡는 오버로드)가 포탈 트리거를 지면으로 오인해 무한 점프가 난다.
    ///
    /// 검출 마스크는 1(라이브만) — 클론은 궤적 재생에 텔레포트 결과가 이미 포함돼 있으므로 다시 옮기면 안 된다
    ///  (KillZone과 동일 원리). 래치 해제 질의도 마스크 1이다 — 513으로 하면 포탈 위 클론이 영구 래치를 만든다.
    ///
    /// 핑퐁 방지: 도착 래치는 반드시 "출발측이 도착측에" 건다(_linkedPortal.SetArrivalLatch()).
    ///  도착측 자체 감지에 맡기면 DriveGimmicks 배열 순서에 따라 같은 틱에 즉시 되돌려보내는 핑퐁이 난다.
    ///
    /// 입력 게이트(2026-08-18 변경: 자동 발동 → 위 키 발동 — 사용자 결정):
    ///  입력은 Update에서 래치만 하고 텔레포트는 지금까지처럼 DriveGimmickTick(틱 위상)에서 실행한다.
    ///  위치 변화가 틱 경계에서만 일어나므로 녹화·클론 재생 결정성이 그대로 유지된다.
    ///
    /// RoundManager는 프롬프트/입력의 Recording 상태 게이트에만 쓴다(KillZone처럼 LevelManager가 주입).
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class IdentityPortal : MonoBehaviour, ITickGimmick
    {
        [Header("Link")]
        [Tooltip("짝 포탈. 이 포탈로 들어온 허용 캐릭터는 여기로 나간다. 쌍방이 서로를 가리켜야 한다.")]
        [SerializeField] private IdentityPortal _linkedPortal;

        [Tooltip("이 포탈을 탈 수 있는 정체성들. 참조 동일성으로 비교한다. 비허용 정체성은 완전 무반응.")]
        [SerializeField] private IdentityData[] _allowedIdentities;

        [Header("Detection")]
        [Tooltip("검출 레이어. 라이브(Default=1)만. 클론은 궤적에 텔레포트가 이미 포함돼 있어 제외.")]
        [SerializeField] private LayerMask _detectionMask = 1;

        [Header("입장 프롬프트")]
        [SerializeField] private SpriteRenderer _promptRenderer;   // 포탈 위 ↑ 표시(LevelExit과 동일 구성)
        [SerializeField] private float _promptWorldScale = 0.5f;   // 프롬프트 월드 크기(부모 스케일 무관)
        [SerializeField] private float _promptGap = 0.35f;         // 포탈 꼭대기와의 간격
        [SerializeField] private float _bobAmplitude = 0.08f;      // 둥실거림 진폭
        [SerializeField] private float _bobFrequency = 2.2f;       // 둥실거림 속도(Hz)

        private RoundManager _roundManager;
        private Collider2D _portalCollider;
        private ContactFilter2D _contactFilter;
        private Vector3 _promptBasePosition;

        /// <summary>Update에서 위 키 입력을 래치하고 다음 틱(DriveGimmickTick)이 소비한다.
        /// 텔레포트를 틱 위상에 묶어 두기 위한 중계 플래그 — 결정성 유지의 핵심.</summary>
        private bool _isTeleportRequested;

        /// <summary>점유 질의 결과 버퍼(non-alloc 재사용).</summary>
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

        /// <summary>도착 직후 래치. 캐릭터가 완전히 이탈할 때까지 이 포탈은 재작동하지 않는다(즉시 되돌려보냄 방지).</summary>
        private bool _isArrivalLatched;

        private void Awake()
        {
            _portalCollider = GetComponent<Collider2D>();

            _contactFilter = new ContactFilter2D { useTriggers = false };
            _contactFilter.SetLayerMask(_detectionMask);
        }

        /// <summary>포탈 프리팹은 씬의 RoundManager를 직렬화 참조할 수 없다. LevelManager가 로드 시 주입한다(KillZone과 동일).</summary>
        public void BindRoundManager(RoundManager roundManager)
        {
            _roundManager = roundManager;
        }

        private void Start()
        {
            if (_promptRenderer == null) return;

            // 프롬프트를 포탈 꼭대기 위에, 부모(포탈) 스케일과 무관한 고정 월드 크기로 앉힌다(LevelExit과 동일).
            Bounds bounds = _portalCollider.bounds;
            _promptBasePosition = new Vector3(bounds.center.x, bounds.max.y + _promptGap, transform.position.z);

            Transform promptTransform = _promptRenderer.transform;
            Vector3 parentScale = promptTransform.parent != null ? promptTransform.parent.lossyScale : Vector3.one;
            promptTransform.localScale = new Vector3(
                _promptWorldScale / Mathf.Max(Mathf.Abs(parentScale.x), 1e-4f),
                _promptWorldScale / Mathf.Max(Mathf.Abs(parentScale.y), 1e-4f),
                1f);
            promptTransform.position = _promptBasePosition;

            _promptRenderer.gameObject.SetActive(false);
        }

        private void Update()
        {
            // 프롬프트 표시 조건 = 텔레포트 가능 조건과 동일해야 한다("뜨는데 안 되는" 거짓말 방지).
            bool shouldPrompt = !_isArrivalLatched
                && _linkedPortal != null
                && _roundManager != null
                && _roundManager.State == RoundState.Recording
                && IsTeleportEligible();

            if (_promptRenderer != null && _promptRenderer.gameObject.activeSelf != shouldPrompt)
                _promptRenderer.gameObject.SetActive(shouldPrompt);

            if (!shouldPrompt) return;

            // 둥실거림 — 렌더 전용 연출이라 결정성과 무관.
            float bob = Mathf.Sin(Time.time * _bobFrequency * 2f * Mathf.PI) * _bobAmplitude;
            _promptRenderer.transform.position = _promptBasePosition + new Vector3(0f, bob, 0f);

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 입력은 래치만 한다 — 실제 텔레포트는 다음 틱의 DriveGimmickTick이 실행한다(결정성 유지).
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                _isTeleportRequested = true;
        }

        /// <summary>중앙 틱: 래치 중이면 이탈 감시만, 아니면 위 키 요청이 래치된 허용 캐릭터를 짝 포탈로 보낸다.</summary>
        public void DriveGimmickTick(int tick)
        {
            // 요청 플래그는 조건 성패와 무관하게 틱마다 소비한다 — 조건 밖에서 누른 입력이
            // 남아 있다가 나중에 발동하는 유령 텔레포트를 막는다.
            bool isRequested = _isTeleportRequested;
            _isTeleportRequested = false;

            if (_isArrivalLatched)
            {
                // 도착 래치 중: 캐릭터가 완전히 이탈하면 해제한다. 해제 여부와 무관하게 이번 틱은 재작동하지 않는다.
                if (GetLiveOccupant() == null)
                    _isArrivalLatched = false;
                return;
            }

            if (!isRequested) return;
            if (_linkedPortal == null) return;

            CharacterActor live = GetLiveOccupant();
            if (live == null) return;

            // 참조 동일성으로 허용 정체성만 통과. 비허용은 완전 무반응.
            if (!IsAllowed(live.Identity)) return;

            // 텔레포트: 짝 포탈 위치로 이동한다.
            // ⚠ SetPosition은 속도를 지우지 않는다 — 속도 보존은 의도된 설계 결정이다(위상/궤적 결정성에 무해하며,
            //    점프 도중 진입 시 관성을 유지해 조작감이 자연스럽다).
            live.SetPosition(_linkedPortal.transform.position);

            // 래치는 반드시 출발측이 도착측에 건다(핑퐁 방지 — remarks 참조).
            _linkedPortal.SetArrivalLatch();
        }

        /// <summary>라운드/보드 리셋: 도착 래치와 입력 요청을 해제한다.</summary>
        public void ResetGimmick()
        {
            _isArrivalLatched = false;
            _isTeleportRequested = false;
        }

        /// <summary>지금 포탈 안의 라이브 캐릭터가 허용 정체성인가(프롬프트 표시 조건 = 텔레포트 조건).</summary>
        private bool IsTeleportEligible()
        {
            CharacterActor live = GetLiveOccupant();
            return live != null && IsAllowed(live.Identity);
        }

        /// <summary>짝 포탈(출발측)이 이 포탈에 도착 래치를 건다.</summary>
        public void SetArrivalLatch()
        {
            _isArrivalLatched = true;
        }

        /// <summary>포탈 트리거 안에 있는 라이브 캐릭터를 반환한다(마스크 1이라 클론은 제외). 없으면 null.</summary>
        private CharacterActor GetLiveOccupant()
        {
            Bounds bounds = _portalCollider.bounds;
            Physics2D.OverlapBox(bounds.center, bounds.size, 0f, _contactFilter, _overlapBuffer);

            for (int i = 0; i < _overlapBuffer.Count; i++)
            {
                Collider2D other = _overlapBuffer[i];
                if (other == null) continue;

                CharacterActor actor = other.GetComponentInParent<CharacterActor>();
                if (actor != null) return actor;
            }

            return null;
        }

        /// <summary>identity가 허용 목록에 참조 동일성으로 포함되는가.</summary>
        private bool IsAllowed(IdentityData identity)
        {
            if (identity == null || _allowedIdentities == null) return false;

            for (int i = 0; i < _allowedIdentities.Length; i++)
            {
                if (_allowedIdentities[i] == identity) return true;
            }

            return false;
        }
    }
}
