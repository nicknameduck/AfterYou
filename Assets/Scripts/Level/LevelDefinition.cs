using AfterYou.Core;
using UnityEngine;

namespace AfterYou.Level
{
    /// <summary>
    /// 레벨 프리팹 루트에 붙는 데이터 홀더. 로직은 없다.
    /// LevelManager가 이 정의를 읽어 캐릭터를 생성하고 SpawnPoint/Exit를 배선한다.
    /// </summary>
    public class LevelDefinition : MonoBehaviour
    {
        [Tooltip("에디터/로그 식별용 레벨 이름.")]
        [SerializeField] private string _levelName;

        [Tooltip("이 레벨에 등장할 캐릭터들의 정체성. 배열 길이 = 캐릭터 수(2~4). LevelManager가 인원수만큼 Player를 생성한다.")]
        [SerializeField] private IdentityData[] _identities;

        [Tooltip("전 캐릭터 공유 스폰 지점. 레벨 프리팹의 자식이다.")]
        [SerializeField] private Transform _spawnPoint;

        [Tooltip("이 레벨의 출구. LevelManager가 런타임에 RoundManager를 주입한다(_roundManager는 비워둠).")]
        [SerializeField] private LevelExit _levelExit;

        [Tooltip("이 레벨의 밀 수 있는 박스들(없으면 빈 배열). RoundManager가 라운드마다 구동한다.")]
        [SerializeField] private PushableBox[] _boxes;

        public string LevelName => _levelName;
        public IdentityData[] Identities => _identities;
        public Transform SpawnPoint => _spawnPoint;
        public LevelExit LevelExit => _levelExit;
        public PushableBox[] Boxes => _boxes;
    }
}
