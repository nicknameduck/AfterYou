using AfterYou.Core;
using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>박스의 물리 구동 모드. 라운드마다 RoundManager가 지정한다.</summary>
    public enum PushableBoxMode
    {
        /// <summary>정지. Kinematic으로 base 위치에 멈춰 있다(운반형이 아닌 라운드의 미소유 박스).</summary>
        Frozen,

        /// <summary>라이브 운반형이 미는 중. Dynamic이 되어 물리로 밀리고 궤적을 기록한다.</summary>
        Record,

        /// <summary>확정된 궤적을 Kinematic + MovePosition으로 되재생(소유 박스).</summary>
        Replay
    }

    /// <summary>
    /// 라이브 운반형이 밀 수 있는 박스. 밀린 궤적을 기록해 이후 라운드에서 클론과 함께 재생한다.
    /// </summary>
    /// <remarks>
    /// 중앙 틱 규약: 클론(ClonePlayback)과 동일하게 자체 Update/FixedUpdate가 없다.
    /// RoundManager가 FixedUpdate마다 CaptureBoxTick/ApplyBoxTick을 직접 호출해 하나의 _tick으로 구동한다.
    ///
    /// 좌표 권위는 항상 Rigidbody2D.position이다. transform.position은 Interpolate 때문에 신뢰할 수 없다.
    ///
    /// _weight는 압력판 판정 전용 순수 데이터다 — Rigidbody2D.mass가 '아니다'.
    /// mass는 1로 두고(밀기 감각 유지), 압력판만 이 Weight를 합산한다.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PushableBox : MonoBehaviour
    {
        [Tooltip("압력판 판정 전용 무게. Rigidbody2D.mass가 '아니다'. 압력판이 위에 올려진 박스의 이 값을 합산한다.")]
        [SerializeField] private float _weight = 10f;

        private Rigidbody2D _rigidbody;
        private Vector2 _basePosition;
        private CloneRecording _recording;

        /// <summary>이 박스를 확정으로 소유한 슬롯(-1 = 미소유). 소유 박스는 그 슬롯이 되감기되면 소유가 풀린다.</summary>
        private int _ownerSlot = -1;

        /// <summary>현재 구동 모드.</summary>
        public PushableBoxMode Mode { get; private set; }

        /// <summary>압력판 판정 전용 무게. Rigidbody2D.mass와 무관한 순수 데이터다.</summary>
        public float Weight => _weight;

        /// <summary>이 박스를 확정 소유한 슬롯. 미소유면 -1.</summary>
        public int OwnerSlot => _ownerSlot;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _basePosition = _rigidbody.position;
        }

        /// <summary>
        /// 구동 모드를 바꾼다. Record면 Dynamic(물리로 밀림), 그 외는 Kinematic(정지/재생).
        /// </summary>
        /// <remarks>속도를 먼저 0으로 지운 뒤 bodyType을 바꾼다 — 잔류 속도가 모드 전환 직후 튀는 것을 막는다.</remarks>
        public void SetMode(PushableBoxMode mode)
        {
            Mode = mode;

            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;

            _rigidbody.bodyType = mode == PushableBoxMode.Record
                ? RigidbodyType2D.Dynamic
                : RigidbodyType2D.Kinematic;
        }

        /// <summary>base 위치로 순간 복귀한다. Interpolate/AutoSync 스미어를 피하려고 rb와 transform을 함께 맞춘다.</summary>
        public void ResetToBase()
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.position = _basePosition;
            transform.position = new Vector3(_basePosition.x, _basePosition.y, transform.position.z);
        }

        /// <summary>이 박스에 대한 새 궤적 기록을 시작한다(라이브 운반형이 밀기 시작할 때).</summary>
        public void BeginRecording(int slot)
        {
            _recording = new CloneRecording(slot);
        }

        /// <summary>중앙 틱 tick의 물리 위치를 궤적에 append한다. 클론과 동일하게 틱당 1프레임씩 쌓인다.</summary>
        public void CaptureBoxTick(int tick)
        {
            if (_recording == null) return;

            Debug.Assert(tick == _recording.FrameCount, "[PushableBox] 박스 캡처 틱이 프레임 수와 어긋났다.");

            // facing은 박스에 무의미하므로 true 고정.
            _recording.AddFrame(new RecordedFrame(_rigidbody.position, true));
        }

        /// <summary>중앙 틱 tick을 이 박스의 재생 위치로 적용한다(소유 박스, Replay 모드).</summary>
        /// <remarks>tick+1 규약: ClonePlayback.ApplyTick과 동일하다 — FixedUpdate가 물리 이전이라 목표는 다음 프레임이다.</remarks>
        public void ApplyBoxTick(int tick)
        {
            if (_recording == null || _recording.FrameCount == 0) return;

            int index = Mathf.Clamp(tick + 1, 0, _recording.FrameCount - 1);
            RecordedFrame frame = _recording.GetFrame(index);

            _rigidbody.MovePosition(frame.Position);
        }

        /// <summary>기록된 궤적의 tick 위치를 조회한다(부수효과 0). 기록이 없으면 false.</summary>
        /// <remarks>
        /// 클램프 규약은 ClonePlayback.TryGetFuturePosition과 동일하다 — 궤적 밖은 마지막 프레임에서 정지하므로
        /// 클램프가 곧 정답이다. 리플레이 사전 스캔이 각 틱의 박스 배치를 재현하는 데 쓴다.
        /// </remarks>
        public bool TryGetRecordedPosition(int tick, out Vector2 position)
        {
            if (_recording == null || _recording.FrameCount == 0)
            {
                position = default;
                return false;
            }

            int index = Mathf.Clamp(tick, 0, _recording.FrameCount - 1);
            position = _recording.GetFrame(index).Position;
            return true;
        }

        /// <summary>기록된 궤적이 실제로 박스를 움직였는가. 첫 프레임과 현재 위치 차이로 판정한다.</summary>
        public bool HasDisplacement(float epsilon)
        {
            if (_recording == null || _recording.FrameCount == 0) return false;

            Vector2 start = _recording.GetFrame(0).Position;
            return Vector2.Distance(start, _rigidbody.position) > epsilon;
        }

        /// <summary>기록된 궤적을 확정하고 이 박스를 slot의 소유로 표시한다. 이후 라운드에서 Replay로 되재생된다.</summary>
        public void CommitRecording(int slot)
        {
            _ownerSlot = slot;
        }

        /// <summary>현재 테이크의 기록을 버린다(박스가 움직이지 않았을 때). 소유는 건드리지 않는다.</summary>
        public void DiscardRecording()
        {
            _recording = null;
        }

        /// <summary>소유와 기록을 모두 해제한다(되감기/레벨 초기화).</summary>
        public void ClearOwnership()
        {
            _ownerSlot = -1;
            _recording = null;
        }
    }
}
