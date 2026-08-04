using System.Collections.Generic;
using AfterYou.Clone;
using AfterYou.Core;
using AfterYou.Level;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterYou.Managers
{
    /// <summary>
    /// 레벨 = 프리팹 구조의 구동자. 씬 전환 없이 레벨 프리팹을 교체하고,
    /// 레벨 정의(LevelDefinition)의 정체성 수만큼 Player를 생성해 RoundManager에 주입한다.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Tooltip("순서대로 로드될 레벨 프리팹들. 마지막 다음은 처음으로 순환한다.")]
        [SerializeField] private LevelDefinition[] _levels;

        [Tooltip("레벨마다 인원수만큼 Instantiate할 캐릭터 프리팹(Player.prefab).")]
        [SerializeField] private GameObject _playerPrefab;

        [Tooltip("생성된 캐릭터들의 부모(=== PLAYER ===). 비활성 부모 트릭에 쓴다.")]
        [SerializeField] private Transform _charactersParent;

        [Tooltip("레벨 프리팹 인스턴스의 부모(=== LEVEL ===).")]
        [SerializeField] private Transform _levelParent;

        [Tooltip("라운드 진행을 구동할 씬의 RoundManager.")]
        [SerializeField] private RoundManager _roundManager;

        /// <summary>현재 로드된 캐릭터들. 다음 레벨 로드 시 이 리스트만으로 전부 파괴한다(클론으로 reparent돼도 커버).</summary>
        private readonly List<CharacterActor> _spawnedActors = new List<CharacterActor>();

        /// <summary>세션 동안 해금된 정체성 풀(해금 순서 유지 — 카드 순서가 전 레벨에서 일관된다).
        /// 레벨의 Identities에 등장하면 그 레벨 로드 시 해금되고, 이후 레벨에서도 계속 선택지에 나온다.</summary>
        private readonly List<IdentityData> _unlockedIdentities = new List<IdentityData>();

        private LevelDefinition _currentLevel;
        private int _currentIndex;

        private void Start()
        {
            LoadLevel(0);
        }

        /// <summary>레벨을 교체한다. 이전 레벨/캐릭터를 정리하고 새 레벨을 로드해 라운드를 구동한다.</summary>
        public void LoadLevel(int index)
        {
            // 1) 진행 중인 라운드를 안전하게 종료한다. 참조(_characters)를 비우기 전에 RoundManager부터 정리해야
            //    전환 순간 LevelExit이 LiveCharacter를 읽어도 NRE가 나지 않는다.
            _roundManager.Teardown();

            // 2) 이전 레벨 정리 — 캐릭터(클론 포함, reparent됐어도 리스트 기반이라 커버)와 레벨 인스턴스를 파괴한다.
            for (int i = 0; i < _spawnedActors.Count; i++)
            {
                if (_spawnedActors[i] != null)
                    Destroy(_spawnedActors[i].gameObject);
            }
            _spawnedActors.Clear();

            if (_currentLevel != null)
                Destroy(_currentLevel.gameObject);

            // 3) 새 레벨 프리팹 인스턴스화.
            _currentIndex = index;
            _currentLevel = Instantiate(_levels[index], _levelParent);

            IdentityData[] identities = _currentLevel.Identities;
            Vector3 spawnPos = _currentLevel.SpawnPoint != null
                ? _currentLevel.SpawnPoint.position
                : Vector3.zero;

            // 해금: 이 레벨의 Identities를 풀에 추가(중복 제거, 해금 순서 유지).
            for (int i = 0; i < identities.Length; i++)
            {
                if (!_unlockedIdentities.Contains(identities[i]))
                    _unlockedIdentities.Add(identities[i]);
            }

            // 사용 가능 종류 = 해금 풀 − 이 레벨의 금지 목록. 클론 예산은 레벨 인원수 그대로.
            // 같은 종류를 예산 한도 내에서 반복 사용할 수 있어야 하므로,
            // 종류 × 예산만큼 사전 스폰한다 — 정체성 주입은 Awake에서만 가능해 런타임 재주입이 불가하기 때문.
            IdentityData[] banned = _currentLevel.BannedIdentities;
            List<IdentityData> types = new List<IdentityData>();
            for (int i = 0; i < _unlockedIdentities.Count; i++)
            {
                bool isBanned = false;
                for (int b = 0; banned != null && b < banned.Length; b++)
                {
                    if (banned[b] == _unlockedIdentities[i]) { isBanned = true; break; }
                }
                if (!isBanned)
                    types.Add(_unlockedIdentities[i]);
            }
            int cloneBudget = identities.Length;

            // 4) 비활성 부모 트릭 — 부모를 끈 채로 캐릭터를 생성·정체성 주입한 뒤 한 번에 켠다.
            //    이렇게 해야 CharacterActor.Awake(정체성 적용)가 "주입 완료 후"에 실행된다.
            _charactersParent.gameObject.SetActive(false);

            for (int t = 0; t < types.Count; t++)
            {
                for (int b = 0; b < cloneBudget; b++)
                {
                    GameObject go = Instantiate(_playerPrefab, spawnPos, Quaternion.identity, _charactersParent);
                    go.name = $"Character_T{t + 1}_{b + 1}";
                    CharacterActor actor = go.GetComponent<CharacterActor>();
                    actor.InjectIdentity(types[t]);
                    _spawnedActors.Add(actor);
                }
            }

            _charactersParent.gameObject.SetActive(true); // 이 순간 전원의 Awake가 주입된 정체성으로 실행된다.

            // 5) 레벨 출구에 씬의 RoundManager를 주입한다(프리팹은 씬 참조를 직렬화할 수 없다).
            if (_currentLevel.LevelExit != null)
                _currentLevel.LevelExit.BindRoundManager(_roundManager);

            // 5-1) 환경 기믹 수집 + 킬존 주입. (true)로 비활성 상태로 설치된 기믹도 수집한다 — 스위치로 켜질 문 등.
            //      킬존은 LevelExit처럼 씬의 RoundManager를 직렬화 참조할 수 없어 여기서 주입한다.
            ITickGimmick[] gimmicks = _currentLevel.GetComponentsInChildren<ITickGimmick>(true);
            KillZone[] killZones = _currentLevel.GetComponentsInChildren<KillZone>(true);
            for (int i = 0; i < killZones.Length; i++)
                killZones[i].BindRoundManager(_roundManager);

            // 6) 라운드 구동 — 반드시 SetActive(true) 이후여야 한다.
            //    Awake가 끝난 뒤라야 Initialize의 OverrideSpawnPosition이 Awake의 rb.position 캡처를 덮어쓴다.
            _roundManager.Initialize(_spawnedActors.ToArray(), _currentLevel.SpawnPoint, _currentLevel.Boxes, gimmicks,
                types.ToArray(), cloneBudget);
        }

        /// <summary>클리어 상태에서 다음 레벨로 넘어간다. N키와 Next 버튼(CharacterSelectUI)이 공용으로 호출한다.</summary>
        public void LoadNextLevel()
        {
            if (_roundManager.State != RoundState.Cleared) return;

            LoadLevel((_currentIndex + 1) % _levels.Length);
        }

        private void Update()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // 디버그 전용 강제 스테이지 이동(임시 — 테스트 끝나면 제거 예정). 클리어 여부 무시.
            Keyboard debugKeyboard = Keyboard.current;
            if (debugKeyboard != null)
            {
                if (debugKeyboard.pageDownKey.wasPressedThisFrame)
                {
                    LoadLevel((_currentIndex + 1) % _levels.Length);
                    return;
                }
                if (debugKeyboard.pageUpKey.wasPressedThisFrame)
                {
                    LoadLevel((_currentIndex - 1 + _levels.Length) % _levels.Length);
                    return;
                }
            }
#endif

            if (_roundManager.State != RoundState.Cleared) return;

            // New Input System 전용 프로젝트라 구 Input.GetKeyDown은 예외를 던진다.
            Keyboard keyboard = Keyboard.current;
            // Enter는 Recording 상태의 클론 확정(RoundManager)에만 쓰여 Cleared 상태에선 충돌이 없다.
            if (keyboard != null && (keyboard.nKey.wasPressedThisFrame
                || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                LoadNextLevel();
        }
    }
}
