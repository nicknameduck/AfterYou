using System.Collections.Generic;
using AfterYou.Managers;
using AfterYou.Replay;
using UnityEngine;
using UnityEngine.UI;

namespace AfterYou.UI
{
    /// <summary>
    /// Phase 1 검증용 최소 UI. 정체성 종류 카드(동적 생성) + 상태 표시.
    /// </summary>
    /// <remarks>
    /// 의도적으로 최소 구현이다 — Phase 5에서 제대로 다시 만든다. 여기에 연출/애니메이션을 넣지 말 것.
    /// TextMeshPro Essentials가 프로젝트에 임포트되어 있지 않아 legacy UnityEngine.UI.Text를 쓴다.
    /// 카드는 템플릿 1개를 종류 수만큼 복제한다 — 새 정체성 종류가 추가돼도 씬 수정이 필요 없다.
    /// 배치는 카드 컨테이너의 VerticalLayoutGroup이 담당한다(화면 좌측 세로 목록).
    /// </remarks>
    public class CharacterSelectUI : MonoBehaviour
    {
        [SerializeField] private RoundManager _roundManager;

        [Tooltip("카드 원본(비활성 유지). 레벨의 정체성 종류 수만큼 복제된다.")]
        [SerializeField] private Button _cardTemplate;

        [Tooltip("카드들이 배치될 컨테이너. VerticalLayoutGroup이 좌측 세로 배치를 담당한다.")]
        [SerializeField] private Transform _cardContainer;

        [SerializeField] private Text _statusText;
        [SerializeField] private LevelManager _levelManager;
        [SerializeField] private Button _nextButton;

        [Tooltip("클리어 리플레이 구동자. 리플레이 중에는 Next 버튼과 안내 문구를 봉인한다.")]
        [SerializeField] private ReplayDirector _replayDirector;

        private const string HelpLine = "1/2/3 · ↑↓ 캐릭터 선택 → Enter 시작 | 녹화 중: Enter 확정 · R 재촬영 | Backspace 되감기";

        /// <summary>
        /// 카드 루트 Image는 배경이 아니라 "테두리"다(Inner가 안쪽을 덮는 구조).
        /// pending 카드는 흰 테두리, 그 외에는 거의 투명한 테두리로 하이라이트를 표현한다.
        /// 도크가 검은 바닥 띠(우측 하단) 위에 앉으므로 밝은 테두리여야 보인다.
        /// </summary>
        private static readonly Color HighlightColor = Color.white;
        private static readonly Color NormalColor = new Color(1f, 1f, 1f, 0.15f);

        private readonly List<Button> _cards = new List<Button>();
        private readonly List<Image> _cardIcons = new List<Image>();

        /// <summary>외부(엔딩 연출)가 도크를 강제로 숨기는 스위치. RoundState와 무관하게 선택 UI를 봉인한다.</summary>
        private bool _isSuppressed;

        /// <summary>
        /// 선택 도크 강제 숨김 토글. EndingSequence가 엔딩 진입 시 true, 종료 시 false로 되돌린다.
        /// RoundManager 상태를 건드리지 않고 표시만 막는 용도다.
        /// </summary>
        public void SetSuppressed(bool suppressed)
        {
            _isSuppressed = suppressed;
        }

        private void Awake()
        {
            if (_cardTemplate != null)
                _cardTemplate.gameObject.SetActive(false);

            if (_nextButton != null && _levelManager != null)
                _nextButton.onClick.AddListener(() => _levelManager.LoadNextLevel());
        }

        private void Update()
        {
            if (_roundManager == null) return;

            // 레벨 전환으로 종류 수가 바뀌면 카드를 다시 만든다 (Teardown 중엔 0 → 전부 제거).
            int typeCount = _roundManager.IdentityTypeCount;
            if (_cards.Count != typeCount)
                RebuildCards(typeCount);

            bool canSelect = !_isSuppressed && _roundManager.State == RoundState.Selecting;
            bool hasBudget = _roundManager.ConfirmedCount < _roundManager.CloneBudget;

            // 선택 도크는 선택 시점에만 보인다 — 녹화/클리어 중엔 숨겨 화면을 비운다.
            // SetActive는 상태가 실제로 바뀔 때만 호출한다(매 프레임 호출 방지).
            if (_cardContainer != null && _cardContainer.gameObject.activeSelf != canSelect)
                _cardContainer.gameObject.SetActive(canSelect);

            for (int i = 0; i < _cards.Count; i++)
            {
                // 같은 종류는 예산이 남는 한 몇 번이든 재선택 가능 — 종류별 소진 개념이 없다.
                _cards[i].interactable = canSelect && hasBudget;

                var identity = _roundManager.GetIdentityType(i);
                if (_cardIcons[i] != null && identity != null)
                    _cardIcons[i].color = identity.TintColor;

                // Selecting 상태의 pending 카드만 배경을 밝힌다. Recording/Cleared에선 전부 꺼진 배경.
                if (_cards[i].image != null)
                {
                    _cards[i].image.color = canSelect && _roundManager.PendingTypeIndex == i
                        ? HighlightColor
                        : NormalColor;
                }
            }

            // 리플레이 중(시작 대기 구간 포함)에는 다음 레벨 유도를 통째로 감춘다 — 안내와 실제 입력 가능 여부가
            // 어긋나면(버튼은 보이는데 눌리지 않음) 그게 더 나쁜 상태다. 스킵 안내는 리플레이 UI가 담당한다.
            bool isReplaying = _replayDirector != null && _replayDirector.BlocksClearedInput;

            if (_nextButton != null)
                _nextButton.gameObject.SetActive(_roundManager.State == RoundState.Cleared && !isReplaying);

            if (_statusText != null)
            {
                _statusText.text = _roundManager.State == RoundState.Cleared
                    ? (isReplaying ? string.Empty : "클리어! 완료 버튼 / Enter / N키로 다음 레벨")
                    : HelpLine;
            }
        }

        /// <summary>카드를 종류 수에 맞춰 재생성한다. 위치·간격은 레이아웃 그룹이 계산하므로 좌표 코드가 없다.</summary>
        private void RebuildCards(int typeCount)
        {
            // 추적 리스트가 아니라 컨테이너의 실제 자식을 청소한다 —
            // 도메인 리로드로 리스트만 초기화되고 카드 오브젝트가 살아남으면 중복 생성되기 때문(자가 치유).
            if (_cardContainer != null && _cardTemplate != null)
            {
                for (int i = _cardContainer.childCount - 1; i >= 0; i--)
                {
                    GameObject child = _cardContainer.GetChild(i).gameObject;
                    if (child != _cardTemplate.gameObject)
                        Destroy(child);
                }
            }
            _cards.Clear();
            _cardIcons.Clear();

            if (_cardTemplate == null || _cardContainer == null) return;

            for (int i = 0; i < typeCount; i++)
            {
                Button card = Instantiate(_cardTemplate, _cardContainer);
                card.gameObject.SetActive(true);

                // 클로저 캡처 주의: 루프 변수 i를 그대로 캡처하면 모든 카드가 마지막 값을 쓴다. 반드시 지역 복사.
                int typeIndex = i;
                card.onClick.AddListener(() => _roundManager.SetPendingIdentity(typeIndex));

                Transform icon = card.transform.Find("Icon");
                _cardIcons.Add(icon != null ? icon.GetComponent<Image>() : null);

                // 아이콘 전용 카드 — 라벨 텍스트는 쓰지 않는다(도움말은 상태 텍스트가 담당).
                Text label = card.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.gameObject.SetActive(false);

                _cards.Add(card);
            }
        }
    }
}
