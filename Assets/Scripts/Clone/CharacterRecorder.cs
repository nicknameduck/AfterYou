using AfterYou.Core;
using UnityEngine;

namespace AfterYou.Clone
{
    /// <summary>
    /// 라이브 캐릭터의 궤적을 틱 단위로 기록한다.
    /// </summary>
    /// <remarks>
    /// 중앙 틱 규약: 이 클래스는 Update/FixedUpdate를 갖지 않는다.
    /// RoundManager가 자기 FixedUpdate에서 CaptureTick(tick)을 직접 호출한다.
    /// (컴포넌트별 자체 인덱스를 두면 실행 순서에 따라 클론끼리 1틱씩 어긋난다)
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterRecorder : MonoBehaviour
    {
        /// <summary>이보다 느리면 방향 전환으로 보지 않는다(정지 시 마지막 방향 유지).</summary>
        private const float FacingSpeedThreshold = 0.01f;

        private Rigidbody2D _rigidbody;
        private CloneRecording _recording;
        private bool _isFacingRight = true;

        public CloneRecording Recording => _recording;

        public int FrameCount => _recording != null ? _recording.FrameCount : 0;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        /// <summary>새 테이크 시작. RoundManager.SelectCharacter에서 호출.</summary>
        public void BeginRecording(int slotIndex)
        {
            _recording = new CloneRecording(slotIndex);
            _isFacingRight = true;
        }

        /// <summary>
        /// 틱 t의 "시작" 상태를 기록. RoundManager.FixedUpdate에서 물리 시뮬레이션 이전에 호출된다.
        /// </summary>
        public void CaptureTick(int tick)
        {
            if (_recording == null) return;

            // 중앙 틱과 프레임 인덱스는 1:1이어야 한다. 어긋나면 재생이 통째로 밀린다.
            Debug.Assert(tick == _recording.FrameCount,
                $"[CharacterRecorder] 틱/프레임 불일치: tick={tick}, frameCount={_recording.FrameCount}", this);

            // PlayerController는 스프라이트를 뒤집지 않으므로 방향은 여기서 속도 부호로 파생한다.
            float velocityX = _rigidbody.linearVelocity.x;
            if (Mathf.Abs(velocityX) >= FacingSpeedThreshold)
                _isFacingRight = velocityX > 0f;

            // 좌표는 반드시 Rigidbody2D.position. 프리팹이 Interpolate라 transform.position은 물리 권위 값이 아니다.
            _recording.AddFrame(new RecordedFrame(_rigidbody.position, _isFacingRight));
        }
    }
}
