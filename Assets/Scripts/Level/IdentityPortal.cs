using System.Collections.Generic;
using AfterYou.Clone;
using AfterYou.Core;
using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>
    /// 허용된 정체성의 라이브 캐릭터를 짝 포탈로 순간이동시키는 포탈. 쌍으로 배치해 서로 _linkedPortal로 배선한다.
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
    /// RoundManager를 참조하지 않는다 — 포탈은 통지가 없다(BindRoundManager 불요, LevelManager 무수정 성립).
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

        private Collider2D _portalCollider;
        private ContactFilter2D _contactFilter;

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

        /// <summary>중앙 틱: 래치 중이면 이탈 감시만, 아니면 허용 캐릭터를 짝 포탈로 보낸다.</summary>
        public void DriveGimmickTick(int tick)
        {
            if (_isArrivalLatched)
            {
                // 도착 래치 중: 캐릭터가 완전히 이탈하면 해제한다. 해제 여부와 무관하게 이번 틱은 재작동하지 않는다.
                if (GetLiveOccupant() == null)
                    _isArrivalLatched = false;
                return;
            }

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

        /// <summary>라운드/보드 리셋: 도착 래치만 해제한다.</summary>
        public void ResetGimmick()
        {
            _isArrivalLatched = false;
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
