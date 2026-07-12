using System.Collections.Generic;
using UnityEngine;

namespace AfterYou.Core
{
    /// <summary>
    /// 캐릭터 1명의 확정된 테이크 = 클론 1기의 재생 원본.
    /// </summary>
    /// <remarks>
    /// Phase 3 undo 스택의 "한 층"이 될 자리다.
    /// (클론 1개 = 궤적 + 그 테이크가 일으킨 환경 변경 리스트 → 되감기 시 함께 롤백)
    /// </remarks>
    public class CloneRecording
    {
        private readonly List<RecordedFrame> _frames = new List<RecordedFrame>();

        /// <summary>이 궤적을 만든 캐릭터 슬롯(0~2).</summary>
        public int SlotIndex { get; private set; }

        public IReadOnlyList<RecordedFrame> Frames => _frames;

        public int FrameCount => _frames.Count;

        public CloneRecording(int slotIndex)
        {
            SlotIndex = slotIndex;
        }

        /// <summary>기록 중 매 틱 1개씩 append. List는 Count 초과 인덱스에 대입할 수 없으므로 Add만 쓴다.</summary>
        public void AddFrame(RecordedFrame frame)
        {
            _frames.Add(frame);
        }

        /// <summary>
        /// 틱을 프레임으로 변환. 범위를 벗어나면 클램프한다.
        /// (재생 길이가 다른 클론들이 같은 중앙 틱을 공유하므로, 짧은 궤적은 마지막 프레임에서 정지 유지)
        /// </summary>
        public RecordedFrame GetFrame(int tick)
        {
            int index = Mathf.Clamp(tick, 0, _frames.Count - 1);
            return _frames[index];
        }
    }
}
