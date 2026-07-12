using AfterYou.Core;
using UnityEngine;

namespace AfterYou.Clone
{
    /// <summary>
    /// 확정된 궤적을 Kinematic 바디로 되재생한다.
    /// </summary>
    /// <remarks>
    /// 중앙 틱 규약: Update/FixedUpdate 없음. RoundManager가 ApplyTick(tick)을 직접 호출한다.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public class ClonePlayback : MonoBehaviour
    {
        private Rigidbody2D _rigidbody;
        private SpriteRenderer _spriteRenderer;
        private CloneRecording _recording;

        public int FrameCount => _recording != null ? _recording.FrameCount : 0;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void SetRecording(CloneRecording recording)
        {
            _recording = recording;
        }

        /// <summary>
        /// 미래 틱의 위치를 미리 조회한다. 궤적이 이미 확정돼 있으므로 "예언"이 가능하다.
        /// </summary>
        /// <remarks>
        /// 압사 예측(RoundManager.ResolveCrush)이 쓴다. 클론이 몇 틱 뒤 어디에 있을지 알면
        /// 부딪히기 "전에" 미리 비켜설 수 있다 — 겹친 뒤 밀어내는 사후 대응은 파묻힘이 눈에 보인다.
        /// </remarks>
        public bool TryGetFuturePosition(int tick, out Vector2 position)
        {
            if (FrameCount == 0)
            {
                position = default;
                return false;
            }

            // 재생이 끝난 뒤에는 마지막 프레임에서 정지하므로 클램프가 곧 정답이다.
            int index = Mathf.Clamp(tick, 0, FrameCount - 1);
            position = _recording.GetFrame(index).Position;
            return true;
        }

        /// <summary>라운드 리셋: 궤적의 첫 프레임으로 순간 배치한다.</summary>
        public void ResetToStart()
        {
            if (FrameCount == 0) return;

            RecordedFrame frame = _recording.GetFrame(0);
            Teleport(frame);
        }

        /// <summary>
        /// 중앙 틱 t를 이 클론의 물리 상태로 적용한다.
        /// </summary>
        public void ApplyTick(int tick)
        {
            if (FrameCount == 0) return;

            // ── 틱 오프셋 규약 ──
            // FixedUpdate는 물리 시뮬레이션 "이전"에 실행된다 → 지금 위치 = 틱 t의 시작 위치.
            // MovePosition은 이번 물리 스텝 동안 목표까지 이동시킨다 → 스텝이 끝나면(= 틱 t+1의 시작) 목표에 도달.
            // 따라서 틱 t에서는 frames[t+1]을 목표로 삼아야 라이브 캐릭터가 그렸던 궤적과 시점이 정확히 일치한다.
            // off-by-one이 나면 재생이 1틱 밀려 압력판/발판 타이밍 버그의 원인이 된다.
            int index = Mathf.Clamp(tick + 1, 0, FrameCount - 1); // 재생 종료 후에는 마지막 프레임을 홀드
            RecordedFrame frame = _recording.GetFrame(index);

            _rigidbody.MovePosition(frame.Position);
            _spriteRenderer.flipX = !frame.IsFacingRight;
        }

        /// <summary>보간 스미어를 피하려고 Rigidbody2D와 Transform을 함께 맞춘다(AutoSyncTransforms가 꺼져 있음).</summary>
        private void Teleport(RecordedFrame frame)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.position = frame.Position;
            transform.position = new Vector3(frame.Position.x, frame.Position.y, transform.position.z);
            _spriteRenderer.flipX = !frame.IsFacingRight;
        }
    }
}
