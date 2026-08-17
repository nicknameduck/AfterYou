using AfterYou.Managers;
using AfterYou.Replay;
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

        [Tooltip("리플레이 감독. 리플레이 중에는 남은 시간/경과를 리플레이 틱 기준으로 표시한다.")]
        [SerializeField] private ReplayDirector _replayDirector;

        [Tooltip("사용 중인 클론 수 값 텍스트 (n / m)")]
        [SerializeField] private Text _cloneValue;

        [Tooltip("녹화 남은 시간 값 텍스트. 녹화 중이 아니면 --.--")]
        [SerializeField] private Text _timeValue;

        [Tooltip("남은 시간 칼럼 전체(라벨+밑줄+값). 무제한 녹화(RoundManager.HasRecordLimit == false)면 통째로 숨긴다.")]
        [SerializeField] private GameObject _timeSection;

        [Tooltip("레벨 시작 후 경과 시간 값 텍스트. 클리어 시 정지한다.")]
        [SerializeField] private Text _elapsedValue;

        private void Start()
        {
            if (_timeSection != null && _roundManager != null)
                _timeSection.SetActive(_roundManager.HasRecordLimit);
        }

        private void Update()
        {
            if (_roundManager == null) return;

            // 리플레이 중엔 라운드가 Cleared로 휴면이라 RoundManager 값이 멈춰 있다.
            // 그 구간만 감독의 틱 기준 시간으로 대체해 되감기 연출과 숫자가 함께 흐르게 한다.
            bool isReplaying = _replayDirector != null && _replayDirector.IsReplaying;

            if (_cloneValue != null)
                _cloneValue.text = $"{_roundManager.ConfirmedCount} / {_roundManager.CloneBudget}";

            if (_timeValue != null)
            {
                if (isReplaying)
                    _timeValue.text = _replayDirector.ReplayRemainingSeconds.ToString("00.00");
                else
                    _timeValue.text = _roundManager.State == RoundState.Recording
                        ? _roundManager.RemainingRecordSeconds.ToString("00.00")
                        : "--.--";
            }

            if (_elapsedValue != null)
                _elapsedValue.text = isReplaying
                    ? _replayDirector.ReplayElapsedSeconds.ToString("00.00")
                    : _roundManager.ElapsedSeconds.ToString("00.00");
        }
    }
}
