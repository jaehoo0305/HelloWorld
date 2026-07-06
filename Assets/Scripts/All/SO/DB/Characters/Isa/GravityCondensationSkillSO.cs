using System;
using System.Collections;
using UnityEngine;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 아이사 고유 스킬 1: '중력 응축' (일반 버전)을 발동시키는 실시간 로직 스크립트입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_GravityCondensation", menuName = "Dungeon/Skills/Gravity Condensation", order = 1)]
    public class GravityCondensationSkillSO : SkillDataSO
    {
        [Header("[ 3D 투사체 파티클 에셋 세팅 ]")]
        [Tooltip("날아갈 투사체 파티클 프리팹을 드래그앤드롭 하세요.")]
        [SerializeField] private GameObject projectilePrefab;
        [Tooltip("적에게 명착 타격 시 피어날 피격 파티클 프리팹을 드래그앤드롭 하세요.")]
        [SerializeField] private GameObject hitPrefab;

        [Header("[ 연출 세부 설정 ]")]
        [SerializeField] private float flightSpeed = 12f;

        /// <summary>
        /// 일반 능력: 십자 방향 중 한 곳으로 최대 8칸 거리까지 투사체를 날려 피해(Dmg 80%)를 줍니다.
        /// </summary>
        public override void Execute(PlayerUnit caster, Vector2Int targetCoord, int level, Action onComplete)
        {
            BattleGridManager gridManager = FindFirstObjectByType<BattleGridManager>();
            if (gridManager == null)
            {
                Debug.LogError("[중력 응축] 씬에서 BattleGridManager를 찾을 수 없어 시전을 중단합니다.");
                onComplete?.Invoke();
                return;
            }

            Vector3 startWorldPos = caster.transform.position + Vector3.up * 1.0f; // 시전자 가슴 높이 오프셋
            Vector3 targetWorldPos = gridManager.GetWorldPosition(targetCoord) + Vector3.up * 1.0f;

            if (projectilePrefab != null)
            {
                // 1. 투사체 실시간 생성 및 비주얼 피지컬 구동
                GameObject projObj = Instantiate(projectilePrefab, startWorldPos, Quaternion.identity);
                GravityProjectile projController = projObj.AddComponent<GravityProjectile>();

                projController.Launch(startWorldPos, targetWorldPos, flightSpeed, () =>
                {
                    // 2. 투사체 낙하지점 도착 시 폭발 피격 파티클 연출
                    if (hitPrefab != null)
                    {
                        GameObject hitObj = Instantiate(hitPrefab, targetWorldPos, Quaternion.identity);
                        Destroy(hitObj, 2.5f); // 넉넉히 2.5초 후 피격 파티클 완전 파괴
                    }

                    // 3. 피해 연산 및 적중 타겟 라이프 데미지 공식 대입
                    BattleUnit targetUnit = gridManager.GetUnitAt(targetCoord);
                    if (targetUnit != null)
                    {
                        SkillLevelData lvlData = GetLevelData(level);
                        float dmgMod = (lvlData != null) ? lvlData.DamageModifier : 0.8f; // {dmg:80} 감지 -> 0.8배

                        // 시전자의 기초 공격력 스탯을 읽어와서 최종 연산
                        int baseAttack = (caster.CharacterData != null) ? caster.CharacterData.Attack : 35;
                        int finalRawDamage = Mathf.RoundToInt(baseAttack * dmgMod);

                        Debug.Log($"[중력 응축 타격] {caster.UnitName} -> {targetUnit.UnitName}에게 {finalRawDamage} (배율: {dmgMod * 100}%)의 피해를 입힙니다.");
                        targetUnit.TakeDamage(finalRawDamage);
                    }

                    // 4. 발사 완료 후 턴 마감 콜백 당겨서 전투 시퀀스 진행 보장
                    onComplete?.Invoke();
                });
            }
            else
            {
                // 프리팹이 안 끼워져 있을 때의 폴백 안정성 연산
                BattleUnit targetUnit = gridManager.GetUnitAt(targetCoord);
                if (targetUnit != null)
                {
                    SkillLevelData lvlData = GetLevelData(level);
                    float dmgMod = (lvlData != null) ? lvlData.DamageModifier : 0.8f;
                    int baseAttack = (caster.CharacterData != null) ? caster.CharacterData.Attack : 35;
                    int finalRawDamage = Mathf.RoundToInt(baseAttack * dmgMod);
                    targetUnit.TakeDamage(finalRawDamage);
                }
                onComplete?.Invoke();
            }
        }
    }

    /// <summary>
    /// 투사체 오브젝트에 런타임 탑재되어 등속 직진 비행을 처리해 주는 등대 컴포넌트입니다.
    /// </summary>
    public class GravityProjectile : MonoBehaviour
    {
        private Vector3 startPos;
        private Vector3 destinationPos;
        private float speed;
        private Action onArrivedCallback;

        private float elapsed = 0f;
        private float flightDuration = 1f;

        public void Launch(Vector3 start, Vector3 target, float launchSpeed, Action onArrived)
        {
            startPos = start;
            destinationPos = target;
            speed = launchSpeed;
            onArrivedCallback = onArrived;

            // 목적지까지의 총 거리 계산
            float distance = Vector3.Distance(start, target);
            flightDuration = Mathf.Max(0.1f, distance / speed);

            // 등속도 지향 각도 정렬
            transform.LookAt(target);

            StartCoroutine(CoPlayFlight());
        }

        private IEnumerator CoAnimateFlight()
        {
            float elapsed = 0f;
            while (elapsed < 1.0f)
            {
                elapsed += Time.deltaTime / flightDuration;
                transform.position = Vector3.Lerp(startPos, destinationPos, elapsed);
                yield return null;
            }

            transform.position = destinationPos;
            onArrivedCallback?.Invoke();
            Destroy(gameObject);
        }

        private IEnumerator CoPlayFlight()
        {
            yield return StartCoroutine(CoAnimateFlight());
        }
    }
}