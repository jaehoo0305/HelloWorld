using UnityEngine;

namespace DungeonCombat.Data
{
    /// <summary>
    /// SkillDataSO에 기입된 EnhanceLogicKey를 기반으로 
    /// 런타임에 캐릭터의 현재 상태를 실시간 평가하여 Z스킬 강화 가능 여부를 반환하는 시스템 헬퍼입니다.
    /// </summary>
    public static class SkillEnhancementEvaluator
    {
        // 인스펙터 [ Enhance Logic Key ] 란에 기입할 추천 핵심 문자열 상수 정의
        public const string ISA_MASS_COLLAPSE_KEY = "Isa_Cond_MassCollapse";
        public const string ISA_HEAT_DEATH_KEY = "Isa_Cond_HeatDeath";

        /// <summary>
        /// 캐릭터의 실시간 전투 데이터와 스킬의 강화 로직 키를 매칭하여 강화 조건 충족 여부를 판별합니다.
        /// </summary>
        /// <param name="logicKey">스킬 SO에 기입된 식별 문자열 키</param>
        /// <param name="cumulativeSpentSP">해당 캐릭터가 이번 전투에서 누적으로 소모한 총 SP</param>
        /// <param name="activeGravityFieldCount">현재 필드 격자 상에 배치되어 작동 중인 중력장의 실시간 개수</param>
        /// <param name="currentRound">현재 진행 중인 전투 라운드 수 (1라운드부터 시작)</param>
        public static bool IsEnhancementConditionMet(string logicKey, int cumulativeSpentSP, int activeGravityFieldCount, int currentRound)
        {
            // 예외 방어 코드: 키가 비어있거나 일반 스킬인 경우 조건 없음(강화 불가)으로 판정
            if (string.IsNullOrEmpty(logicKey))
            {
                return false;
            }

            // 대소문자 오타 방지를 위해 일괄 소문자 변환 후 스위치 분기 처리
            switch (logicKey.ToLower().Trim())
            {
                case "isa_cond_masscollapse":
                    // [질량 축퇴 해금 조건]: 중력장 최소 하나 존재 (Count >= 1) && 소모한 누적 SP 10개 이상
                    bool massCollapseGravityField = activeGravityFieldCount >= 1;
                    bool massCollapseSpentSP = cumulativeSpentSP >= 10;
                    return massCollapseGravityField && massCollapseSpentSP;

                case "isa_cond_heatdeath":
                    // [열죽음 해금 조건]: 중력장 최소 하나 존재 (Count >= 1) && 4라운드 이상 경과 (Round >= 4)
                    bool heatDeathGravityField = activeGravityFieldCount >= 1;
                    bool heatDeathRound = currentRound >= 4;
                    return heatDeathGravityField && heatDeathRound;

                default:
                    Debug.LogWarning($"[SkillEnhancementEvaluator] 정의되지 않은 강화 로직 키가 입력되었습니다: {logicKey}");
                    return false;
            }
        }
    }
}