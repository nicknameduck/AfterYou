using System.Collections;
using System.Collections.Generic;
using AfterYou.Core;
using AfterYou.UI;
using UnityEngine;

namespace AfterYou.Ending
{
    /// <summary>
    /// 마지막 레벨 클리어 후의 엔딩 연출 총괄. 엔딩 전용 무대를 세우고 해금된 정체성만큼 봇을 풀어놓은 뒤
    /// "ALL CLEAR" 라벨을 화면 위에서 내린다.
    /// </summary>
    /// <remarks>
    /// 씬 상주(=== MANAGERS === 하위)다. 자기 GameObject를 SetActive(false) 하지 않는다 —
    /// 꺼진 오브젝트는 Stop()의 코루틴 정리가 돌지 못해 재시작이 막히는 데드락이 된다.
    /// </remarks>
    public class EndingSequence : MonoBehaviour
    {
        [Tooltip("엔딩 전용 무대 프리팹(바닥 + 좌우 벽). LevelDefinition이 없는 순수 배경이다.")]
        [SerializeField] private GameObject _endingLevelPrefab;

        [Tooltip("정체성 종류마다 1기씩 스폰할 엔딩 봇 프리팹.")]
        [SerializeField] private GameObject _botPrefab;

        [Tooltip("화면 위에서 내려올 ALL CLEAR 라벨(Canvas 하위).")]
        [SerializeField] private RectTransform _allClearLabel;

        [Tooltip("라벨이 목표 위치까지 내려오는 데 걸리는 시간(초).")]
        [SerializeField] private float _labelSlideDuration = 1.2f;

        [Tooltip("라벨의 하강 목표 anchoredPosition.y. 시작 y는 씬에 배치된 값(화면 밖)을 그대로 쓴다.")]
        [SerializeField] private float _labelTargetAnchoredY = -300f;

        [Tooltip("엔딩 동안 숨길 게임플레이 UI(PaperHUD, StatusText 등). Stop에서 전부 되살린다.")]
        [SerializeField] private GameObject[] _hideDuringEnding;

        [Tooltip("엔딩 동안 선택 도크를 봉인할 대상.")]
        [SerializeField] private CharacterSelectUI _characterSelectUI;

        [Tooltip("봇 스폰 x 산개 반경. 이 범위를 좌우로 균등 분배한다.")]
        [SerializeField] private float _botSpawnRangeX = 12f;

        [Tooltip("봇 스폰 y. 엔딩 무대 바닥 윗면(-7.71)보다 위여야 한다.")]
        [SerializeField] private float _botSpawnY = -6.5f;

        /// <summary>엔딩 연출이 진행 중인지. Begin/Stop 중복 호출 가드로도 쓰인다.</summary>
        public bool IsActive { get; private set; }

        /// <summary>엔딩이 소유한 무대 인스턴스. Stop에서 이 참조로만 파괴한다.</summary>
        private GameObject _levelInstance;

        /// <summary>엔딩이 소유한 봇들. 무대 자식이지만 리스트로도 들고 있어야 부분 실패 시에도 정리가 된다.</summary>
        private readonly List<GameObject> _bots = new List<GameObject>();

        private Coroutine _labelRoutine;

        /// <summary>_hideDuringEnding 각 항목의 숨기기 직전 activeSelf. Stop이 "무조건 켜기"가 아니라
        /// "원래대로 되돌리기"를 하도록 보관한다 — StatusText처럼 애초에 꺼져 있던 UI를 엔딩이 켜버리는 것을 막는다.</summary>
        private bool[] _hiddenPreviousStates;

        /// <summary>씬에 배치된 라벨의 초기 anchoredPosition(화면 위 밖). Stop에서 여기로 되돌린다.</summary>
        private Vector2 _labelStartPosition;

        private void Awake()
        {
            if (_allClearLabel != null)
            {
                // 씬 배치값을 "화면 밖 시작점"의 단일 진실로 삼는다 — 재시작을 몇 번 해도 같은 자리에서 시작한다.
                _labelStartPosition = _allClearLabel.anchoredPosition;
                _allClearLabel.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 엔딩 연출을 시작한다. LevelManager가 마지막 레벨 정리를 끝낸 뒤(무대가 빈 상태에서) 호출한다.
        /// </summary>
        /// <param name="identities">세션 동안 해금된 정체성들. 종류마다 봇 1기가 나온다.</param>
        /// <param name="levelParent">무대 인스턴스를 붙일 부모(=== LEVEL ===).</param>
        public void Begin(IdentityData[] identities, Transform levelParent)
        {
            if (IsActive) return;
            IsActive = true;

            _levelInstance = Instantiate(_endingLevelPrefab, levelParent);

            for (int i = 0; i < identities.Length; i++)
            {
                // 종류 수에 맞춰 좌우로 균등 분배한다. 1기뿐이면 중앙(0.5)에 세운다.
                float t = identities.Length > 1 ? (float)i / (identities.Length - 1) : 0.5f;
                float x = Mathf.Lerp(-_botSpawnRangeX, _botSpawnRangeX, t);

                GameObject bot = Instantiate(_botPrefab, new Vector3(x, _botSpawnY, 0f),
                    Quaternion.identity, _levelInstance.transform);
                bot.name = $"EndingBot_{i + 1}";
                bot.GetComponent<EndingBot>().Init(identities[i]);
                _bots.Add(bot);
            }

            _hiddenPreviousStates = new bool[_hideDuringEnding.Length];
            for (int i = 0; i < _hideDuringEnding.Length; i++)
            {
                if (_hideDuringEnding[i] == null) continue;
                _hiddenPreviousStates[i] = _hideDuringEnding[i].activeSelf;
                _hideDuringEnding[i].SetActive(false);
            }

            if (_characterSelectUI != null)
                _characterSelectUI.SetSuppressed(true);

            _labelRoutine = StartCoroutine(SlideLabelRoutine());
        }

        /// <summary>
        /// 엔딩 연출을 걷어내고 시작 전 상태로 되돌린다. 재시작(Enter/N)과 디버그 스테이지 이동이 호출한다.
        /// </summary>
        public void Stop()
        {
            if (!IsActive) return;

            if (_labelRoutine != null)
            {
                StopCoroutine(_labelRoutine);
                _labelRoutine = null;
            }

            if (_allClearLabel != null)
            {
                _allClearLabel.anchoredPosition = _labelStartPosition;
                _allClearLabel.gameObject.SetActive(false);
            }

            // 봇을 먼저 지운다 — 무대가 먼저 파괴돼도 자식이라 같이 사라지지만, 리스트를 비우는 책임은 여기 있다.
            for (int i = 0; i < _bots.Count; i++)
            {
                if (_bots[i] != null)
                    Destroy(_bots[i]);
            }
            _bots.Clear();

            if (_levelInstance != null)
            {
                Destroy(_levelInstance);
                _levelInstance = null;
            }

            for (int i = 0; i < _hideDuringEnding.Length; i++)
            {
                if (_hideDuringEnding[i] != null && _hiddenPreviousStates != null && i < _hiddenPreviousStates.Length)
                    _hideDuringEnding[i].SetActive(_hiddenPreviousStates[i]);
            }
            _hiddenPreviousStates = null;

            if (_characterSelectUI != null)
                _characterSelectUI.SetSuppressed(false);

            IsActive = false;
        }

        /// <summary>라벨을 화면 위 밖(씬 배치값)에서 목표 y까지 내린다.</summary>
        private IEnumerator SlideLabelRoutine()
        {
            if (_allClearLabel == null) yield break;

            _allClearLabel.anchoredPosition = _labelStartPosition;
            _allClearLabel.gameObject.SetActive(true);

            Vector2 target = new Vector2(_labelStartPosition.x, _labelTargetAnchoredY);

            float elapsed = 0f;
            while (elapsed < _labelSlideDuration)
            {
                elapsed += Time.deltaTime;
                // SmoothStep 이징 — ScreenFader와 같은 관례로 시작/끝을 완만하게 만든다.
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _labelSlideDuration);
                _allClearLabel.anchoredPosition = Vector2.LerpUnclamped(_labelStartPosition, target, t);
                yield return null;
            }

            _allClearLabel.anchoredPosition = target;
            _labelRoutine = null;
        }
    }
}
