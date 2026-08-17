using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AfterYou.UI
{
    /// <summary>
    /// 풀스크린 오버레이 Image의 알파를 조절해 화면 페이드아웃 → 콜백 → 페이드인을 수행한다.
    /// 레벨 전환(LevelManager.LoadNextLevel) 연출에 쓰인다.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        [Tooltip("페이드에 쓸 풀스크린 오버레이 이미지(검정, 시작 알파 0).")]
        [SerializeField] private Image _overlay;

        [Tooltip("화면이 완전히 가려질 때까지 걸리는 시간(초).")]
        [SerializeField] private float _fadeOutDuration = 0.3f;

        [Tooltip("가려진 화면이 완전히 걷힐 때까지 걸리는 시간(초).")]
        [SerializeField] private float _fadeInDuration = 0.3f;

        /// <summary>페이드 시퀀스 진행 중 여부. 진행 중 재호출을 호출부에서 가드하는 용도.</summary>
        public bool IsFading { get; private set; }

        /// <summary>페이드아웃 → 화면이 완전히 가려진 시점에 onCovered 실행 → 페이드인.</summary>
        public void FadeOutThen(Action onCovered)
        {
            if (IsFading) return;
            StartCoroutine(FadeRoutine(onCovered));
        }

        private IEnumerator FadeRoutine(Action onCovered)
        {
            IsFading = true;
            // 페이드 중에는 오버레이가 클릭을 흡수해 Next 버튼 등 UI 중복 입력을 막는다.
            _overlay.raycastTarget = true;

            yield return Fade(0f, 1f, _fadeOutDuration);

            // 콜백(레벨 로드 등)이 예외를 던져도 페이드 상태를 복구한다 — 예외가 코루틴을 죽이면
            // IsFading이 true로 영구 고착되어 이후 모든 전환이 조용히 무시된다(2026-08-18 실측:
            // 레벨 로드 중 NRE 1회 → "다음 스테이지가 안 넘어감"). 예외는 삼키지 않고 다시 로그한다.
            try
            {
                onCovered?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            yield return Fade(1f, 0f, _fadeInDuration);

            _overlay.raycastTarget = false;
            IsFading = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            Color color = _overlay.color;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // SmoothStep 이징 — 시작/끝을 완만하게 해 선형 보간의 뚝 끊기는 인상을 없앤다.
                color.a = Mathf.SmoothStep(from, to, elapsed / duration);
                _overlay.color = color;
                yield return null;
            }

            color.a = to;
            _overlay.color = color;
        }
    }
}
