using System.Collections.Generic;
using AfterYou.Clone;
using AfterYou.Managers;
using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>
    /// 라이브가 닿으면 테이크를 재시작시키는 위험 지대. 가시(Spikes)와 낙하 구멍(FallZone) 공용.
    /// </summary>
    /// <remarks>
    /// 왜 물리 콜백(OnTrigger)이 아니라 중앙 틱 질의인가:
    ///  OnTrigger는 물리 시뮬레이션 "도중"에 불린다. 그 콜백 안에서 RoundManager.OnLiveDied → RestartTake가
    ///  돌면 물리 스텝 내부에서 캐릭터 활성/위치가 뒤바뀌어 위험하다. 그래서 다른 기믹과 동일하게
    ///  DriveGimmickTick에서 OverlapBox로 질의만 하고, 실제 재시작은 물리 스텝 밖에서 일어나게 한다.
    ///
    /// 검출 마스크는 Default(1)만 = 라이브. 클론(512)/박스(2048)는 자연 배제된다
    ///  — 클론이 재생 도중 가시를 밟아 죽으면 결정성이 깨지므로 절대 죽이면 안 된다.
    ///  또한 LiveCharacter와 참조 동일성까지 확인해 이중으로 방어한다(LevelExit와 동일 원리).
    ///
    /// ⚠ 알려진 제약: Record 중인 박스가 낙하 구멍에 빠지는 케이스는 스펙 밖이다(박스는 마스크에서 배제됨).
    ///  낙하 구멍 아래로 박스가 사라지는 레벨은 설계로 회피한다.
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class KillZone : MonoBehaviour, ITickGimmick
    {
        [Header("Detection")]
        [Tooltip("검출 레이어. 라이브(Default=1)만. 클론/박스는 죽이지 않는다.")]
        [SerializeField] private LayerMask _detectionMask = 1;

        private Collider2D _zoneCollider;
        private ContactFilter2D _contactFilter;

        /// <summary>OverlapBox 결과 버퍼(non-alloc 재사용).</summary>
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

        private RoundManager _roundManager;

        /// <summary>
        /// 레벨 프리팹은 씬의 RoundManager를 직렬화 참조할 수 없다(프리팹→씬 참조 불가).
        /// LevelManager가 로드 시 주입한다(LevelExit와 동일 패턴).
        /// </summary>
        public void BindRoundManager(RoundManager roundManager)
        {
            _roundManager = roundManager;
        }

        private void Awake()
        {
            _zoneCollider = GetComponent<Collider2D>();

            _contactFilter = new ContactFilter2D { useTriggers = false };
            _contactFilter.SetLayerMask(_detectionMask);
        }

        /// <summary>킬존은 상태가 없다. 매 틱 위치 질의만 하므로 리셋할 것이 없다.</summary>
        public void ResetGimmick()
        {
        }

        /// <summary>중앙 틱: 지대 안에 라이브가 있으면 테이크를 재시작시킨다.</summary>
        public void DriveGimmickTick(int tick)
        {
            if (_roundManager == null) return;

            CharacterActor live = _roundManager.LiveCharacter;
            if (live == null) return;

            Bounds bounds = _zoneCollider.bounds;
            Physics2D.OverlapBox(bounds.center, bounds.size, 0f, _contactFilter, _overlapBuffer);

            for (int i = 0; i < _overlapBuffer.Count; i++)
            {
                Collider2D other = _overlapBuffer[i];
                if (other == null) continue;

                // (알려진 제약, 이번 회차 미최적화) 매 틱 GetComponentInParent 호출 — 핫패스지만 현 스코프에선 방치.
                CharacterActor actor = other.GetComponentInParent<CharacterActor>();
                if (actor == null) continue;

                // 라이브만 죽인다 — 참조 동일성으로 확인.
                if (actor != live) continue;

                _roundManager.OnLiveDied();
                return;
            }
        }
    }
}
