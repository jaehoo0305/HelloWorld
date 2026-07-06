using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 전장의 격자별 장판(필드 이펙트) 데이터베이스를 실시간 관리하는 전역 매니저입니다.
    /// </summary>
    public class BattleFieldEffectManager : MonoBehaviour
    {
        public static BattleFieldEffectManager Instance { get; private set; }

        [Header("[ 핵심 매니저 참조 ]")]
        [SerializeField] private BattleGridManager gridManager;
        [SerializeField] private BattleTurnManager turnManager;

        // 격자 좌표별로 깔려있는 장판들을 관리하는 데이터베이스 (한 칸에 여러 장판이 겹칠 수 있으므로 List 처리)
        private Dictionary<Vector2Int, List<TileFieldEffect>> fieldEffects = new Dictionary<Vector2Int, List<TileFieldEffect>>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (gridManager == null) gridManager = FindFirstObjectByType<BattleGridManager>();
            if (turnManager == null) turnManager = FindFirstObjectByType<BattleTurnManager>();

            // 유닛이 이동할 때 장판을 밟았는지 체크하기 위해 그리드 매니저 이벤트 구독
            if (gridManager != null)
            {
                gridManager.OnUnitMoveEnd += HandleUnitMoveEnd;
            }

            // 라운드나 턴이 시작될 때 지속시간 차감 처리를 위해 턴 매니저 이벤트 구독
            if (turnManager != null)
            {
                turnManager.OnTurnStarted += HandleTurnStarted;
            }
        }

        /// <summary>
        /// 특정 좌표에 새로운 장판(프리팹)을 동적으로 생성하고 등록합니다.
        /// </summary>
        public void SpawnFieldEffect(GameObject prefab, Vector2Int coord, string effectKey, BattleUnit owner, int duration)
        {
            if (prefab == null || !gridManager.IsWalkable(coord)) return;

            Vector3 worldPos = gridManager.GetWorldPosition(coord);
            GameObject go = Instantiate(prefab, worldPos, Quaternion.identity, transform);

            TileFieldEffect effect = go.GetComponent<TileFieldEffect>();
            if (effect == null)
            {
                effect = go.AddComponent<TileFieldEffect>();
            }

            effect.Initialize(coord, effectKey, owner, duration);

            if (!fieldEffects.ContainsKey(coord))
            {
                fieldEffects[coord] = new List<TileFieldEffect>();
            }
            fieldEffects[coord].Add(effect);
        }

        /// <summary>
        /// 특정 유닛이 소유한 전장의 모든 고유 장판 리스트를 조회합니다. (아이사 고유 스킬용)
        /// </summary>
        public List<TileFieldEffect> GetEffectsByOwner(BattleUnit owner, string effectKey)
        {
            List<TileFieldEffect> result = new List<TileFieldEffect>();
            foreach (var pair in fieldEffects)
            {
                foreach (var effect in pair.Value)
                {
                    if (effect.Owner == owner && effect.EffectKey == effectKey)
                    {
                        result.Add(effect);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 특정 좌표에 특정 키를 가진 장판이 깔려있는지 확인합니다. (아이사 SP 소모 감소 패시브 체크용)
        /// </summary>
        public bool HasEffectAt(Vector2Int coord, string effectKey)
        {
            if (!fieldEffects.TryGetValue(coord, out List<TileFieldEffect> list)) return false;
            return list.Exists(e => e.EffectKey == effectKey);
        }

        /// <summary>
        /// 특정 좌표의 특정 장판을 확정 파괴하고 데이터베이스에서 격리 제거합니다. (중력장 희생 기믹용)
        /// </summary>
        public void RemoveEffectAt(Vector2Int coord, TileFieldEffect effect)
        {
            if (fieldEffects.TryGetValue(coord, out List<TileFieldEffect> list))
            {
                if (list.Remove(effect))
                {
                    effect.ClearEffect();
                }
                if (list.Count == 0)
                {
                    fieldEffects.Remove(coord);
                }
            }
        }

        private void HandleUnitMoveEnd(BattleUnit unit, Vector2Int coord)
        {
            // 유닛이 이동을 마치고 멈춰 선 타일에 장판이 있다면踏On 이벤트 트리거 실행
            if (fieldEffects.TryGetValue(coord, out List<TileFieldEffect> list))
            {
                // 리스트 수정 중 예외 방지를 위해 역순 루프
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    list[i].OnUnitStepOn(unit);
                }
            }
        }

        private void HandleTurnStarted(BattleUnit activeUnit)
        {
            // 턴이 시작된 유닛의 발밑 장판 효과 발동
            Vector2Int coord = gridManager.GetUnitCoordinate(activeUnit);
            if (fieldEffects.TryGetValue(coord, out List<TileFieldEffect> list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    list[i].OnUnitTurnStart(activeUnit);
                }
            }

            // 전역 장판들의 지속시간 감쇄 (여기서는 단순화를 위해 매 턴 시작 시 전체 차감하거나 소유자 턴 기반 자율 확장 가능)
            List<KeyValuePair<Vector2Int, TileFieldEffect>> expiredEffects = new List<KeyValuePair<Vector2Int, TileFieldEffect>>();

            foreach (var pair in fieldEffects)
            {
                foreach (var effect in pair.Value)
                {
                    // 예시: 장판을 깐 시전자의 턴이 돌아왔을 때만 지속시간이 닳도록 정교하게 필터링
                    if (effect.Owner == activeUnit)
                    {
                        if (effect.TickDuration())
                        {
                            expiredEffects.Add(new KeyValuePair<Vector2Int, TileFieldEffect>(pair.Key, effect));
                        }
                    }
                }
            }

            // 만료된 장판들 청소
            foreach (var expired in expiredEffects)
            {
                RemoveEffectAt(expired.Key, expired.Value);
            }
        }

        private void OnDestroy()
        {
            if (gridManager != null) gridManager.OnUnitMoveEnd -= HandleUnitMoveEnd;
            if (turnManager != null) turnManager.OnTurnStarted -= HandleTurnStarted;
        }
    }
}