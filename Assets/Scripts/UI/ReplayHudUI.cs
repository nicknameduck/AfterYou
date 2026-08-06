using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AfterYou.UI
{
    /// <summary>
    /// 클리어 리플레이 전용 HUD — 스킵 힌트 + 협력 고리 고조 단계 표시.
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

        [Tooltip("협력 고리 고조 단계 표시(상단 중앙). 고리 수만큼 pip을 찍고 도달한 만큼 채운다.")]
        [SerializeField] private Text _escalationText;

        /// <summary>이번 리플레이의 총 협력 고리 수. Show가 세팅한다.</summary>
        private int _totalRings;

        /// <summary>pip 문자열 조립용 재사용 버퍼. 고리 도달마다 다시 만드므로 매번 new string을 쌓지 않는다.</summary>
        private readonly StringBuilder _pipBuilder = new StringBuilder();

        /// <summary>리플레이 시작 — 총 고리 수를 받아 pip을 전부 미도달(○)로 그리고 힌트를 켠다.</summary>
        public void Show(int totalRings)
        {
            _totalRings = Mathf.Max(0, totalRings);

            if (_content != null)
                _content.SetActive(true);

            if (_skipHintText != null)
                _skipHintText.text = SkipHint;

            SetStage(0);
        }

        /// <summary>고조 단계를 설정한다(도달한 고리 수). pip이 앞에서부터 stage개만큼 채워진다.</summary>
        public void SetStage(int stage)
        {
            if (_escalationText == null) return;

            int filled = Mathf.Clamp(stage, 0, _totalRings);

            _pipBuilder.Clear();
            for (int i = 0; i < _totalRings; i++)
                _pipBuilder.Append(i < filled ? '●' : '○');

            _escalationText.text = _pipBuilder.ToString();
        }

        /// <summary>리플레이 종료(완주/스킵 공통) — 내용 래퍼만 끈다. 루트는 그대로 활성이다.</summary>
        public void Hide()
        {
            if (_content != null)
                _content.SetActive(false);
        }
    }
}
