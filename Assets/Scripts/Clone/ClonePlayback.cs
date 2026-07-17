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
        /// <summary>클론 기준 알파. CharacterActor.CloneAlpha와 같은 값이어야 한다(반투명 0.5).</summary>
        private const float CloneAlpha = 0.5f;

        /// <summary>스폰 지점에서 이만큼 벗어나면 "걸어 나갔다"로 보고 페이드인을 시작한다.</summary>
        private const float RevealDistance = 0.5f;

        /// <summary>페이드인에 걸리는 시간(초).</summary>
        private const float RevealDuration = 0.15f;

        /// <summary>제자리에 선 클론도 결국 나타나게 하는 폴백 틱(≈1초).</summary>
        private const int RevealTickFallback = 50;

        private Rigidbody2D _rigidbody;
        private SpriteRenderer _spriteRenderer;
        private CloneRecording _recording;

        /// <summary>페이드인 진행도(0=투명, 1=완전 공개). 한 번 오르면 되돌리지 않는다.</summary>
        private float _revealProgress;

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

        /// <summary>라운드 리셋: 궤적의 첫 프레임으로 순간 배치하고 투명하게 되돌린다.</summary>
        public void ResetToStart()
        {
            if (FrameCount == 0) return;

            _revealProgress = 0f;

            RecordedFrame frame = _recording.GetFrame(0);
            Teleport(frame);

            // 투명하게 시작 — 스폰에서 겹친 클론들이 한꺼번에 보이지 않게 숨긴다(이동/폴백 때 페이드인).
            Color color = _spriteRenderer.color;
            color.a = 0f;
            _spriteRenderer.color = color;
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

            UpdateReveal(tick, frame.Position);
        }

        /// <summary>
        /// 스태거 페이드인. 타이밍은 밀지 않는다 — 보이는 것만 늦춘다.
        /// </summary>
        /// <remarks>
        /// 스폰 지점을 RevealDistance 이상 벗어나거나 폴백 틱을 넘기면 공개를 시작하고,
        /// 한 번 시작하면(_revealProgress > 0) 되돌리지 않고 RevealDuration에 걸쳐 알파를 0→CloneAlpha로 올린다.
        /// </remarks>
        private void UpdateReveal(int tick, Vector2 currentPosition)
        {
            if (_revealProgress < 1f)
            {
                bool hasLeftSpawn = Vector2.Distance(currentPosition, _recording.GetFrame(0).Position) > RevealDistance;
                if (_revealProgress > 0f || hasLeftSpawn || tick > RevealTickFallback)
                    _revealProgress = Mathf.Min(1f, _revealProgress + Time.fixedDeltaTime / RevealDuration);
            }

            // CharacterActor.SetMode가 세팅한 RGB(정체성 색)는 보존하고 알파만 덮어쓴다.
            Color color = _spriteRenderer.color;
            color.a = CloneAlpha * _revealProgress;
            _spriteRenderer.color = color;
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
