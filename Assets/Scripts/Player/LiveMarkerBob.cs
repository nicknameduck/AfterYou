using UnityEngine;

namespace AfterYou.Player
{
    /// <summary>
    /// 라이브 마커(▼)의 위아래 둥실거림. 출구 ↑ 프롬프트(LevelExit)와 같은 모션 언어.
    /// 렌더 전용 연출 — 마커는 녹화/판정 대상이 아니라 결정성과 무관하다.
    /// </summary>
    public class LiveMarkerBob : MonoBehaviour
    {
        [Tooltip("둥실거림 진폭(로컬 유닛). 출구 프롬프트와 동일 기본값.")]
        [SerializeField] private float _amplitude = 0.08f;

        [Tooltip("둥실거림 속도(Hz). 출구 프롬프트와 동일 기본값.")]
        [SerializeField] private float _frequency = 2.2f;

        // 부모(캐릭터)가 움직이므로 월드가 아닌 "로컬" 기준 위치에 오프셋을 얹는다.
        private Vector3 _baseLocalPosition;

        private void OnEnable()
        {
            _baseLocalPosition = transform.localPosition;
        }

        private void OnDisable()
        {
            // 마커가 꺼졌다 다시 켜질 때 OnEnable이 오프셋 얹힌 위치를 기준으로 재캐시하지 않도록 복원.
            transform.localPosition = _baseLocalPosition;
        }

        private void Update()
        {
            float bob = Mathf.Sin(Time.time * _frequency * 2f * Mathf.PI) * _amplitude;
            transform.localPosition = _baseLocalPosition + new Vector3(0f, bob, 0f);
        }
    }
}
