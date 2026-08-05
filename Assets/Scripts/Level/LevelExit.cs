using AfterYou.Clone;
using AfterYou.Managers;
using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>
    /// 출구. 라이브(현재 조작 중인) 캐릭터가 닿을 때만 클리어시킨다.
    /// </summary>
    /// <remarks>
    /// 확정 규칙: 클론이 출구에 닿아도 클리어되지 않는다.
    /// 태그/레이어로 대충 거르면 클론도 같은 프리팹이라 구분이 불가능하므로,
    /// RoundManager.LiveCharacter와 "참조 동일성"으로 비교한다.
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class LevelExit : MonoBehaviour
    {
        [SerializeField] private RoundManager _roundManager;

        /// <summary>라이브가 출구에 닿아 클리어가 확정되는 순간 발화(OnLevelCleared 직전).
        /// 클리어 리플레이의 마지막 협력 고리 수집 전용이다.</summary>
        public event System.Action OnCleared;

        /// <summary>
        /// 레벨 프리팹은 씬의 RoundManager를 직렬화 참조할 수 없다(프리팹→씬 참조 불가).
        /// LevelManager가 로드 시 주입한다.
        /// </summary>
        public void BindRoundManager(RoundManager roundManager)
        {
            _roundManager = roundManager;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_roundManager == null) return;

            // 콜라이더가 자식(GroundCheck 등)에 붙어 있을 수 있으므로 부모까지 거슬러 찾는다.
            CharacterActor actor = other.GetComponentInParent<CharacterActor>();
            if (actor == null) return;

            if (actor != _roundManager.LiveCharacter) return;

            // 통지가 먼저다 — OnLevelCleared가 상태를 Cleared로 바꾸기 전이라야 수집기가 클리어 틱을 읽을 수 있다.
            OnCleared?.Invoke();

            _roundManager.OnLevelCleared();
        }
    }
}
