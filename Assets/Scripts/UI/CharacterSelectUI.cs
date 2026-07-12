using AfterYou.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace AfterYou.UI
{
    /// <summary>
    /// Phase 1 검증용 최소 UI. 캐릭터 슬롯 버튼 3개 + 상태 표시.
    /// </summary>
    /// <remarks>
    /// 의도적으로 최소 구현이다 — Phase 5에서 제대로 다시 만든다. 여기에 연출/애니메이션을 넣지 말 것.
    /// TextMeshPro Essentials가 프로젝트에 임포트되어 있지 않아 legacy UnityEngine.UI.Text를 쓴다.
    /// </remarks>
    public class CharacterSelectUI : MonoBehaviour
    {
        [SerializeField] private RoundManager _roundManager;
        [SerializeField] private Button[] _slotButtons;
        [SerializeField] private Text _statusText;

        private const string HelpLine = "1/2/3 Select | Enter Confirm | R Retake | Backspace Rewind";

        private void Awake()
        {
            if (_roundManager == null) return;

            for (int i = 0; i < _slotButtons.Length; i++)
            {
                // 클로저 캡처 주의: 루프 변수 i를 그대로 캡처하면 모든 버튼이 마지막 값을 쓴다. 반드시 지역 복사.
                int index = i;
                _slotButtons[i].onClick.AddListener(() => _roundManager.SelectCharacter(index));
            }
        }

        private void Update()
        {
            if (_roundManager == null) return;

            bool canSelect = _roundManager.State == RoundState.Selecting;

            for (int i = 0; i < _slotButtons.Length; i++)
                _slotButtons[i].interactable = canSelect && !_roundManager.IsSlotConfirmed(i);

            if (_statusText != null)
            {
                _statusText.text =
                    $"{_roundManager.State}  |  Clones {_roundManager.ConfirmedCount}/{_roundManager.SlotCount}\n{HelpLine}";
            }
        }
    }
}
