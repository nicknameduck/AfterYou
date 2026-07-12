using UnityEngine;

namespace AfterYou.Core
{
    /// <summary>
    /// 궤적 1틱 분량의 스냅샷.
    /// 중앙 틱(RoundManager)이 FixedUpdate마다 하나씩 쌓고, 클론이 같은 틱 인덱스로 되읽는다.
    /// </summary>
    /// <remarks>
    /// 향후 확장 여지: 정체성별 상태(Phase 2 — 운반 중 여부, 벽타기 부착 여부),
    /// 환경 상호작용 플래그(압력판 밟음/레버 당김) 등을 필드로 추가할 자리.
    /// 틱당 1개씩 쌓이므로(15초 = 750개 × 3클론) 힙 할당을 피하려고 struct로 둔다.
    /// </remarks>
    public struct RecordedFrame
    {
        /// <summary>물리 권위 좌표(Rigidbody2D.position). transform.position은 Interpolate 때문에 신뢰할 수 없다.</summary>
        public Vector2 Position;

        /// <summary>스프라이트 방향. PlayerController는 flip을 하지 않으므로 CharacterRecorder가 속도 부호로 파생한다.</summary>
        public bool IsFacingRight;

        public RecordedFrame(Vector2 position, bool isFacingRight)
        {
            Position = position;
            IsFacingRight = isFacingRight;
        }
    }
}
