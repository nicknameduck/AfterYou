using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AfterYou.Replay
{
    /// <summary>
    /// 클리어 리플레이의 고조 연출. 협력 고리가 하나씩 발화할 때마다 단계 카운트를 올리고 화면을 한 번 펄스시킨다.
    /// </summary>
    /// <remarks>
    /// 의도적으로 미니멀하다 — 이번 회차의 목적은 "몇 개의 고리가 어떤 순서로 엮였는가"를 읽히게 하는 것뿐이다.
    /// TextMeshPro Essentials가 프로젝트에 없어 legacy UnityEngine.UI.Text를 쓴다(CharacterSelectUI와 동일 규약).
    /// </remarks>
    public class ReplayFlourishUI : MonoBehaviour
    {
        [Tooltip("고리 단계 카운트 텍스트. 리플레이 중에만 보인다.")]
        [SerializeField] private Text _chainText;

        [Tooltip("화면 전체를 덮는 펄스 이미지. 알파만 코드가 건드리므로 색(RGB)은 씬에서 정한다 — 종이 세계는 배경이 " +
            "흰색이라 어두운 색이라야 읽힌다. ⚠ raycastTarget은 반드시 꺼둘 것 — 투명해도 클릭을 흡수한다.")]
        [SerializeField] private Image _pulse;

        [Tooltip("스킵 안내 텍스트. 리플레이 중에만 보인다.")]
        [SerializeField] private Text _skipHint;

        [Header("Pulse")]
        [Tooltip("펄스가 도달하는 최대 알파. 이 값을 넘기면 화면이 통째로 덮여 궤적이 안 보인다.")]
        [SerializeField] private float _pulsePeak = 0.12f;

        [Tooltip("펄스 1회(0 → peak → 0)에 걸리는 시간(초).")]
        [SerializeField] private float _pulseDuration = 0.25f;

        [Header("Count Punch")]
        [Tooltip("카운트가 갱신될 때 튀어오르는 배율.")]
        [SerializeField] private float _punchScale = 1.35f;

        [Tooltip("튀어오른 배율이 1로 돌아오는 시간(초).")]
        [SerializeField] private float _punchDuration = 0.18f;

        private int _totalChains;
        private Coroutine _pulseRoutine;
        private Coroutine _punchRoutine;

        private void Awake()
        {
            // 씬 저장 상태와 무관하게 "숨김"이 기본이다 — 리플레이 밖에서 잔재가 보이는 것을 원천 차단한다.
            HideAll();
        }

        /// <summary>리플레이 시작. 고리 총 개수를 받아 카운트 표기의 분모로 삼는다.</summary>
        public void BeginReplay(int totalChains)
        {
            _totalChains = totalChains;

            if (_chainText != null)
            {
                _chainText.rectTransform.localScale = Vector3.one;
                _chainText.text = FormatChain(0);
                // 고리가 하나도 없는 레벨(단독 클리어)에서는 카운트 자체를 띄우지 않는다.
                _chainText.gameObject.SetActive(totalChains > 0);
            }

            if (_skipHint != null)
                _skipHint.gameObject.SetActive(true);
        }

        /// <summary>고리 하나가 발화했다. index는 1부터 누적되는 단계 번호다.</summary>
        public void PlayChainStep(int index)
        {
            if (_chainText != null)
            {
                _chainText.text = FormatChain(index);

                if (_punchRoutine != null) StopCoroutine(_punchRoutine);
                _punchRoutine = StartCoroutine(PunchRoutine());
            }

            if (_pulse != null)
            {
                // 고리가 연달아 터지면 이전 펄스를 잘라내고 다시 시작한다 — 겹쳐 재생하면 알파가 누적돼 화면이 탄다.
                if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
                _pulseRoutine = StartCoroutine(PulseRoutine());
            }
        }

        /// <summary>리플레이 종료(완주/스킵 공통). 진행 중인 연출을 잘라내고 전부 숨긴다.</summary>
        public void EndReplay()
        {
            if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
            if (_punchRoutine != null) { StopCoroutine(_punchRoutine); _punchRoutine = null; }

            HideAll();
        }

        private void HideAll()
        {
            if (_chainText != null)
            {
                _chainText.rectTransform.localScale = Vector3.one;
                _chainText.gameObject.SetActive(false);
            }

            if (_skipHint != null)
                _skipHint.gameObject.SetActive(false);

            if (_pulse != null)
            {
                SetPulseAlpha(0f);
                _pulse.gameObject.SetActive(false);
            }
        }

        private string FormatChain(int index) => $"고리 {index} / {_totalChains}";

        private IEnumerator PulseRoutine()
        {
            _pulse.gameObject.SetActive(true);

            float elapsed = 0f;
            float half = Mathf.Max(0.0001f, _pulseDuration * 0.5f);

            while (elapsed < _pulseDuration)
            {
                elapsed += Time.deltaTime;

                // 전반은 0 → peak, 후반은 peak → 0. SmoothStep 이징은 ScreenFader/EndingSequence와 같은 관례다.
                float alpha = elapsed < half
                    ? Mathf.SmoothStep(0f, _pulsePeak, elapsed / half)
                    : Mathf.SmoothStep(_pulsePeak, 0f, (elapsed - half) / half);

                SetPulseAlpha(alpha);
                yield return null;
            }

            SetPulseAlpha(0f);
            _pulse.gameObject.SetActive(false);
            _pulseRoutine = null;
        }

        private IEnumerator PunchRoutine()
        {
            RectTransform rect = _chainText.rectTransform;

            float elapsed = 0f;
            float duration = Mathf.Max(0.0001f, _punchDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float scale = Mathf.SmoothStep(_punchScale, 1f, elapsed / duration);
                rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            rect.localScale = Vector3.one;
            _punchRoutine = null;
        }

        private void SetPulseAlpha(float alpha)
        {
            Color color = _pulse.color;
            color.a = alpha;
            _pulse.color = color;
        }
    }
}
