using UnityEngine;
using UnityEngine.UI;

namespace AfterYou.UI
{
    /// <summary>
    /// 클리어 리플레이 전용 HUD — 스킵 힌트 표시.
    /// </summary>
    /// <remarks>
    /// 상시 활성 부모 + Content 래퍼 패턴: 이 컴포넌트가 붙은 루트는 항상 켜둔 채로 두고,
    /// 보이고/숨기는 것은 자식 _content의 SetActive로만 한다.
    /// 루트를 끄면 ReplayDirector가 잡고 있는 참조로 Show()를 불러도 아무 일도 일어나지 않는다
    /// (비활성 오브젝트의 컴포넌트는 살아 있지만, 이후 켜기 위한 진입점이 사라진다).
    /// </remarks>
    public class ReplayHudUI : MonoBehaviour
    {
        /// <summary>스킵 안내 문구. 리플레이 중 상시 표시된다.</summary>
        private const string SkipHint = "아무 키나 눌러 건너뛰기";

        [Tooltip("리플레이 중에만 켜지는 내용 래퍼. 이 컴포넌트가 붙은 루트는 상시 활성이어야 한다.")]
        [SerializeField] private GameObject _content;

        [Tooltip("\"아무 키나 눌러 건너뛰기\" 힌트 텍스트(화면 우하단).")]
        [SerializeField] private Text _skipHintText;

        /// <summary>리플레이 시작 — 내용 래퍼를 켜고 스킵 힌트를 표시한다.</summary>
        public void Show()
        {
            if (_content != null)
                _content.SetActive(true);

            if (_skipHintText != null)
                _skipHintText.text = SkipHint;
        }

        /// <summary>리플레이 종료(완주/스킵 공통) — 내용 래퍼만 끈다. 루트는 그대로 활성이다.</summary>
        public void Hide()
        {
            if (_content != null)
                _content.SetActive(false);
        }
    }
}
