using AfterYou.Level;
using UnityEditor;
using UnityEngine;

namespace AfterYou.EditorTools
{
    /// <summary>
    /// 선택한 오브젝트를 발밑 지형 위에 안착시키는 레벨 편집 툴 (Tools > Snap To Ground, Ctrl+Shift+D).
    /// 물리 레이캐스트 대신 SpriteRenderer bounds 계산을 쓴다 — 편집 모드/프리팹 스테이지에서는
    /// 물리 씬이 동기화되지 않아 레이캐스트가 신뢰 불가하기 때문.
    /// 렌더러 없는 SpawnPoint는 캐릭터 콜라이더 기준의 전용 스냅으로 처리한다(아래 SnapSpawnPointDown).
    /// </summary>
    public static class SnapToGroundTool
    {
        private const float Epsilon = 0.0001f;

        /// <summary>Player.prefab BoxCollider2D(1×1, 오프셋 0 = 피벗 중앙) 기준 반높이/반폭.
        /// 스폰 좌표 관례 = 지면 윗면 + 반높이(캐릭터가 정확히 서서 시작). 콜라이더 크기 변경 시 함께 갱신.</summary>
        private const float CharacterHalfHeight = 0.5f;
        private const float CharacterHalfWidth = 0.5f;

        [MenuItem("Tools/Snap To Ground %#d")]
        private static void SnapSelection()
        {
            foreach (var target in Selection.transforms)
            {
                SnapDown(target);
            }
        }

        [MenuItem("Tools/Snap To Ground %#d", true)]
        private static bool ValidateSnapSelection()
        {
            return Selection.transforms.Length > 0;
        }

        /// <summary>
        /// target의 바닥을, x범위가 겹치는 아래쪽 스프라이트 중 가장 높은 윗면에 맞춰 y만 이동한다.
        /// 안착할 지형이 없으면 false. 테스트/외부 호출용으로 public.
        /// </summary>
        public static bool SnapDown(Transform target)
        {
            var renderer = target.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null)
            {
                // 렌더러 없는 대상 중 SpawnPoint만 전용 스냅으로 처리한다(그 외에는 기존과 동일하게 경고).
                var levelDefinition = target.GetComponentInParent<LevelDefinition>();
                if (levelDefinition != null && levelDefinition.SpawnPoint == target)
                    return SnapSpawnPointDown(target);

                Debug.LogWarning($"[SnapToGround] '{target.name}'에 SpriteRenderer가 없어 스냅할 수 없습니다.");
                return false;
            }

            var bounds = renderer.bounds;
            float bestTop = FindGroundTop(target, bounds.min.x, bounds.max.x, bounds.center.y);

            if (float.IsNegativeInfinity(bestTop))
            {
                Debug.LogWarning($"[SnapToGround] '{target.name}' 아래에 안착할 지형이 없습니다.");
                return false;
            }

            Undo.RecordObject(target, "Snap To Ground");
            target.position += new Vector3(0f, bestTop - bounds.min.y, 0f);
            return true;
        }

        /// <summary>
        /// SpawnPoint 전용 스냅: 렌더러가 없으므로 캐릭터 콜라이더(1×1)를 가상 bounds로 써서,
        /// 스폰될 캐릭터가 정확히 지면 위에 서도록 지면 윗면 + 반높이에 앉힌다(관례: Level_1_1 실측 −7.71+0.5=−7.21).
        /// </summary>
        private static bool SnapSpawnPointDown(Transform target)
        {
            Vector3 position = target.position;
            float bestTop = FindGroundTop(target,
                position.x - CharacterHalfWidth, position.x + CharacterHalfWidth, position.y);

            if (float.IsNegativeInfinity(bestTop))
            {
                Debug.LogWarning($"[SnapToGround] '{target.name}' 아래에 안착할 지형이 없습니다.");
                return false;
            }

            Undo.RecordObject(target, "Snap To Ground");
            target.position = new Vector3(position.x, bestTop + CharacterHalfHeight, position.z);
            return true;
        }

        /// <summary>
        /// x범위 [minX, maxX]와 겹치고 윗면이 centerY보다 아래인 스프라이트 중 가장 높은 윗면 y를 찾는다.
        /// 없으면 NegativeInfinity. 일반 스냅과 SpawnPoint 스냅이 공유한다.
        /// </summary>
        private static float FindGroundTop(Transform target, float minX, float maxX, float centerY)
        {
            float bestTop = float.NegativeInfinity;

            foreach (var root in target.gameObject.scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (candidate.transform == target || candidate.transform.IsChildOf(target))
                    {
                        continue;
                    }

                    var candidateBounds = candidate.bounds;
                    bool overlapsX = candidateBounds.min.x < maxX - Epsilon
                                  && candidateBounds.max.x > minX + Epsilon;
                    // 윗면이 자기 중심보다 아래인 것만 — 옆에서 겹친 벽이나 위쪽 천장에 붙는 오동작 방지
                    if (!overlapsX || candidateBounds.max.y > centerY)
                    {
                        continue;
                    }

                    if (candidateBounds.max.y > bestTop)
                    {
                        bestTop = candidateBounds.max.y;
                    }
                }
            }

            return bestTop;
        }
    }
}
