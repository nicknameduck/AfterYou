using System.Collections.Generic;
using AfterYou.Clone;
using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>
    /// 밟으면 상태가 뒤집히는 토글 스위치. 배선된 대상들(IActivatable)에 ON/OFF를 통지한다.
    /// </summary>
    /// <remarks>
    /// PressurePlate와 판정 원리는 같지만(기하 질의) 규칙이 다르다:
    ///  - PressurePlate는 홀드(밟고 있는 동안만 열림)지만, 여기는 토글(밟는 "순간"마다 뒤집힘)이다.
    ///  - 자체 FixedUpdate가 없다 — RoundManager가 DriveGimmickTick(tick)에서만 질의한다(중앙 틱 규약).
    ///
    /// 왜 물리 질의(OverlapBox)인가 / 왜 속도가 아니라 기하인가: PressurePlate의 remarks와 동일하다.
    /// 클론은 Kinematic + MovePosition이라 속도가 0으로 읽히므로 "발바닥이 윗면 높이에 있는가"만 본다.
    /// 검출 마스크는 Default(1) | Clone(512) = 513 — 클론도 토글해야 재생 결정성이 성립한다.
    ///
    /// 엣지 검출: 빈 → 점유로 바뀌는 "밟는 순간"에만 토글한다. 계속 밟고 있어도 한 번만 반응한다.
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class ToggleSwitch : MonoBehaviour, ITickGimmick
    {
        [Header("Detection")]
        [Tooltip("검출 레이어. 라이브(Default) + 클론(Clone) 둘 다 포함해야 한다 → 513.")]
        [SerializeField] private LayerMask _detectionMask = 513;

        [Tooltip("판 윗면 위로 띄울 검출 박스의 두께.")]
        [SerializeField] private float _detectionHeight = 0.1f;

        [Tooltip("발바닥이 판 윗면보다 이만큼 아래여도 '올라섰다'고 인정한다. 물리 솔버의 미세한 파고듦 흡수용.")]
        [SerializeField] private float _standTolerance = 0.08f;

        [Header("Targets")]
        [Tooltip("이 스위치가 켜고 끌 대상들. IActivatable을 구현한 컴포넌트(TimedDoor 등)를 배선한다(다대상).")]
        [SerializeField] private MonoBehaviour[] _targets;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _offColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private Color _onColor = new Color(0.3f, 0.9f, 0.4f, 1f);

        /// <summary>
        /// OverlapBox 결과 버퍼. 매 틱 질의하므로 배열 반환 오버로드는 프레임마다 GC 할당이 난다.
        /// List를 재사용하는 non-alloc 오버로드로 할당 0을 유지한다(PressurePlate와 동일).
        /// </summary>
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

        private ContactFilter2D _contactFilter;
        private Collider2D _switchCollider;

        /// <summary>_targets를 IActivatable로 캐스트해 캐시한 배열. 캐스트 실패 항목은 제외된다.</summary>
        private IActivatable[] _activatables;

        /// <summary>현재 켜져 있는가. OFF가 기본 상태다.</summary>
        private bool _isOn;

        /// <summary>직전 틱에 점유돼 있었는가. 엣지(빈→점유) 검출용.</summary>
        private bool _isOccupiedPrevTick;

        private void Awake()
        {
            _switchCollider = GetComponent<Collider2D>();

            // useTriggers=false: QueriesHitTriggers가 1이라 기본값이면 트리거도 잡힌다.
            // 캐릭터 콜라이더는 모두 비트리거이므로 걸러도 손해가 없다(PressurePlate와 동일).
            _contactFilter = new ContactFilter2D { useTriggers = false };
            _contactFilter.SetLayerMask(_detectionMask);

            // 배선된 대상을 IActivatable로 캐스트해 캐시한다.
            // 캐스트 실패는 배선 실수이므로 조용히 skip하지 않고 로그로 드러낸다.
            List<IActivatable> activatables = new List<IActivatable>(_targets != null ? _targets.Length : 0);
            if (_targets != null)
            {
                for (int i = 0; i < _targets.Length; i++)
                {
                    if (_targets[i] == null) continue;

                    if (_targets[i] is IActivatable activatable)
                        activatables.Add(activatable);
                    else
                        Debug.LogError($"[ToggleSwitch] {name}: _targets[{i}] ({_targets[i].GetType().Name})는 IActivatable이 아니다. 배선을 확인할 것.", this);
                }
            }
            _activatables = activatables.ToArray();

            ApplyVisual(_isOn);
        }

        /// <summary>라운드/보드 리셋: OFF로 되돌리고 점유 이력을 지운 뒤 대상에 통지한다.</summary>
        public void ResetGimmick()
        {
            _isOn = false;

            // 유령 엣지 방지: 리셋 후 여전히 밟혀 있어도 다음 틱에 즉시 재토글되지 않도록 점유 이력을 지운다.
            _isOccupiedPrevTick = false;

            ApplyVisual(_isOn);
            NotifyTargets();
        }

        /// <summary>중앙 틱: 점유 상태를 질의해 빈→점유 엣지에서만 토글한다.</summary>
        /// <remarks>
        /// ⚠ 알려진 제약(이번 회차 미수정): 캐릭터가 스위치 위에서 미세하게 튀어(점프 착지 바운스 등)
        ///  1틱 이내에 발바닥이 검출대역을 벗어났다 재진입하면 엣지가 한 번 더 잡혀 이중 토글이 날 수 있다.
        ///  _standTolerance로 대부분 흡수되므로 현 스코프에서는 방치한다.
        /// </remarks>
        public void DriveGimmickTick(int tick)
        {
            // 자기 동기화: 자동 닫힘 등으로 전 대상이 스스로 비활성이 됐는데 스위치만 ON으로 남으면,
            // 재개방에 두 번 밟아야 한다. 그 전에 스위치를 OFF로 되돌려 한 번 밟기로 재개방되게 한다.
            if (_isOn && AllTargetsInactive())
            {
                _isOn = false;
                ApplyVisual(_isOn);
            }

            bool isOccupied = IsOccupied();

            if (isOccupied && !_isOccupiedPrevTick)
                Toggle();

            _isOccupiedPrevTick = isOccupied;
        }

        /// <summary>배선된 전 대상이 현재 비활성인가. 대상이 없으면 false(자기 동기화를 트리거하지 않는다).</summary>
        private bool AllTargetsInactive()
        {
            if (_activatables.Length == 0) return false;

            for (int i = 0; i < _activatables.Length; i++)
            {
                if (_activatables[i].IsActivated) return false;
            }

            return true;
        }

        /// <summary>판 위에 실제로 서 있는 캐릭터가 하나라도 있는가.</summary>
        private bool IsOccupied()
        {
            Bounds switchBounds = _switchCollider.bounds;
            float switchTop = switchBounds.max.y;

            Vector2 boxCenter = new Vector2(switchBounds.center.x, switchTop + _detectionHeight * 0.5f);
            Vector2 boxSize = new Vector2(switchBounds.size.x, _detectionHeight);

            Physics2D.OverlapBox(boxCenter, boxSize, 0f, _contactFilter, _overlapBuffer);

            for (int i = 0; i < _overlapBuffer.Count; i++)
            {
                Collider2D other = _overlapBuffer[i];
                if (other == null) continue;

                // 콜라이더가 자식(GroundCheck 등)에 붙어 있을 수 있으므로 부모까지 거슬러 찾는다.
                // (알려진 제약, 이번 회차 미최적화) 매 틱 GetComponentInParent를 호출한다 — 핫패스지만
                // 검출 대상이 소수라 현 스코프에서는 방치한다. 필요 시 콜라이더→액터 캐시로 개선 가능.
                CharacterActor actor = other.GetComponentInParent<CharacterActor>();
                if (actor == null) continue;

                // 발바닥이 판 윗면 높이에 있어야 "올라섰다". 옆으로 스치거나 관통하는 순간을 배제한다.
                if (other.bounds.min.y < switchTop - _standTolerance) continue;

                return true;
            }

            return false;
        }

        private void Toggle()
        {
            _isOn = !_isOn;
            ApplyVisual(_isOn);
            NotifyTargets();
        }

        private void NotifyTargets()
        {
            for (int i = 0; i < _activatables.Length; i++)
                _activatables[i].SetActivated(_isOn);
        }

        private void ApplyVisual(bool isOn)
        {
            if (_renderer != null)
                _renderer.color = isOn ? _onColor : _offColor;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Collider2D switchCollider = _switchCollider != null ? _switchCollider : GetComponent<Collider2D>();
            if (switchCollider == null) return;

            Bounds switchBounds = switchCollider.bounds;
            Vector3 boxCenter = new Vector3(switchBounds.center.x, switchBounds.max.y + _detectionHeight * 0.5f, 0f);
            Vector3 boxSize = new Vector3(switchBounds.size.x, _detectionHeight, 0f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(boxCenter, boxSize);
        }
#endif
    }
}
