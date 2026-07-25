using AfterYou.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace AfterYou.UI
{
    /// <summary>
    /// 녹화 중(RoundState.Recording)임을 화면 우상단 REC 오버레이로 알린다.
    /// </summary>
    /// <remarks>
    /// 캠코더 REC 표시의 관습을 그대로 빌린다 — 빨간 점만 1Hz로 점멸하고 "REC" 라벨은 상시 표시.
    /// 라벨까지 같이 깜빡이면 글자가 읽히지 않아 상태 전달이 오히려 느려진다.
    /// </remarks>
    public class RecIndicatorUI : MonoBehaviour
    {
        /// <summary>점멸 주기(초). 1초에 한 번, 절반 켜고 절반 끈다.</summary>
        private const float BlinkPeriod = 1f;

        [Tooltip("녹화 상태를 읽어올 라운드 매니저. 미할당이면 오버레이는 아무 동작도 하지 않는다.")]
        [SerializeField] private RoundManager _roundManager;

        [Tooltip("REC 오버레이 전체(점 + 라벨의 부모). 녹화 중일 때만 활성화된다.")]
        [SerializeField] private GameObject _root;

        [Tooltip("점멸시킬 빨간 점. 라벨은 점멸 대상이 아니다.")]
        [SerializeField] private Image _dot;

        private void Update()
        {
            if (_roundManager == null) return;

            bool isRecording = _roundManager.State == RoundState.Recording;

            // 매 프레임 SetActive를 부르면 자식 전체에 OnEnable/OnDisable이 도는 낭비다. 상태가 바뀔 때만 호출한다.
            if (_root != null && _root.activeSelf != isRecording)
                _root.SetActive(isRecording);

            // Time.time 기준이라 프레임레이트와 무관하게 정확히 1Hz로 점멸한다.
            if (isRecording && _dot != null)
                _dot.enabled = Time.time % BlinkPeriod < BlinkPeriod * 0.5f;
        }
    }
}
