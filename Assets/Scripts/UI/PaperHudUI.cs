using AfterYou.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace AfterYou.UI
{
    /// <summary>
    /// 종이 세계 프로토타입 HUD — 상단 3분할 스탯(클론 수 / 남은 시간 / 총 시간).
    /// </summary>
    /// <remarks>
    /// CharacterSelectUI와 같은 폴링 방식이다 — Phase 5 UI 리워크 전까지의 임시 규약.
    /// 값 텍스트만 갱신하고 레이아웃/타이틀은 씬에 정적으로 둔다.
    /// </remarks>
    public class PaperHudUI : MonoBehaviour
    {
        [SerializeField] private RoundManager _roundManager;

        [Tooltip("사용 중인 클론 수 값 텍스트 (n / m)")]
        [SerializeField] private Text _cloneValue;

        [Tooltip("녹화 남은 시간 값 텍스트. 녹화 중이 아니면 --.--")]
        [SerializeField] private Text _timeValue;

        [Tooltip("레벨 시작 후 경과 시간 값 텍스트. 클리어 시 정지한다.")]
        [SerializeField] private Text _elapsedValue;

        private void Update()
        {
            if (_roundManager == null) return;

            // 클리어 후에는 마지막 값을 홀드한다. 리플레이가 끝나며 확정 스택을 리셋하므로,
            // 갱신을 계속하면 "사용한 클론 3기"가 클리어 화면에서 0으로 튄다.
            if (_cloneValue != null && _roundManager.State != RoundState.Cleared)
                _cloneValue.text = $"{_roundManager.ConfirmedCount} / {_roundManager.CloneBudget}";

            if (_timeValue != null)
                _timeValue.text = _roundManager.State == RoundState.Recording
                    ? _roundManager.RemainingRecordSeconds.ToString("00.00")
                    : "--.--";

            if (_elapsedValue != null)
                _elapsedValue.text = _roundManager.ElapsedSeconds.ToString("00.00");
        }
    }
}
