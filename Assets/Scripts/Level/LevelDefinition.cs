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

        [Tooltip("이 레벨에 등장할 캐릭터들의 정체성. 배열 길이 = 클론 예산(2~4). 여기 등록된 정체성은 이 레벨 로드 시 해금되어 이후 레벨에서도 계속 선택지에 나온다.")]
        [SerializeField] private IdentityData[] _identities;

        [Tooltip("이 레벨에서 사용 금지할 정체성(없으면 빈 배열). 해금 풀에 있어도 이 레벨의 선택지에서 제외된다 — 퍼즐 통제용.")]
        [SerializeField] private IdentityData[] _bannedIdentities;

        [Tooltip("전 캐릭터 공유 스폰 지점. 레벨 프리팹의 자식이다.")]
        [SerializeField] private Transform _spawnPoint;

        [Tooltip("이 레벨의 출구. LevelManager가 런타임에 RoundManager를 주입한다(_roundManager는 비워둠).")]
        [SerializeField] private LevelExit _levelExit;

        [Tooltip("이 레벨의 밀 수 있는 박스들(없으면 빈 배열). RoundManager가 라운드마다 구동한다.")]
        [SerializeField] private PushableBox[] _boxes;

        public string LevelName => _levelName;
        public IdentityData[] Identities => _identities;
        public IdentityData[] BannedIdentities => _bannedIdentities;
        public Transform SpawnPoint => _spawnPoint;
        public LevelExit LevelExit => _levelExit;
        public PushableBox[] Boxes => _boxes;

#if UNITY_EDITOR
        [Header("Editor - 카메라 프레임 기즈모")]
        [Tooltip("씬 뷰에 그릴 카메라 가시 범위의 중심. 현재 고정 카메라(SampleScene Main Camera) 위치 = (0, 2). " +
                 "팔로우 카메라 도입 후에는 이 값을 옮겨 '캐릭터가 여기 있을 때 보이는 범위'를 미리 볼 수 있다.")]
        [SerializeField] private Vector2 _cameraFrameCenter = new Vector2(0f, 2f);

        [Tooltip("카메라 orthographicSize(= 세로 반높이). SampleScene Main Camera의 11.4와 동기 유지 — 카메라 튜닝 시 함께 갱신할 것.")]
        [SerializeField] private float _cameraOrthoSize = 11.4f;

        [Tooltip("화면 가로세로비. Steam PC 타깃 16:9.")]
        [SerializeField] private float _cameraAspect = 16f / 9f;

        /// <summary>
        /// 레벨 프리팹 편집 중(프리팹 모드 포함) 씬 뷰에 카메라 가시 범위를 그린다.
        /// 값은 씬 카메라의 상수 복제다 — 프리팹 모드에는 카메라가 없어 참조 동기화가 불가능하므로
        /// 정직하게 상수로 두고 카메라 튜닝 시 함께 갱신한다.
        /// </summary>
        private void OnDrawGizmos()
        {
            float halfH = _cameraOrthoSize;
            float halfW = _cameraOrthoSize * _cameraAspect;
            Vector3 center = transform.position + new Vector3(_cameraFrameCenter.x, _cameraFrameCenter.y, 0f);

            // 프레임 사각형
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            Gizmos.DrawWireCube(center, new Vector3(halfW * 2f, halfH * 2f, 0f));

            // 중앙 십자 마크(작게) — 프레임 중심 식별용
            const float cross = 0.4f;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.4f);
            Gizmos.DrawLine(center + Vector3.left * cross, center + Vector3.right * cross);
            Gizmos.DrawLine(center + Vector3.down * cross, center + Vector3.up * cross);
        }
#endif
    }
}
