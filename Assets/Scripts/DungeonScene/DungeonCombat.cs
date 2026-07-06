namespace DungeonCombat.Data
{
    // 게임 전역에서 사용하는 전투 규칙 상수 (매직 넘버 방지)
    public static class CombatConfig
    {
        public const int MAX_SP = 10;                     // 기본 최대 SP
        public const int MAX_BANK_SP = 5;                // 이월 가능한 추가 저장소 최대 SP
        public const int MAX_OVERHEAT = 100;             // 최대 과열치 (100% 도달 시 과열 상태)
        public const int TURN_START_SP_RECOVERY = 5;     // 턴 시작 시 기본 SP 회복량

        // 사용한 SP 하나 당 누적되는 과열량 전역 규칙
        public const int OVERHEAT_PER_SP = 2;            // 소모한 1 SP 당 과열 게이지 2 상승
    }

    public enum PositionType
    {
        Tank,       // 탱커: 처치 시 SP 100% 충전
        Dealer,     // 딜러: 처치 시 추가 행동
        Healer,     // 힐러: 처치 시 아군 과열 저하
        Boss        // 보스: 라운드 내 다중 행동
    }

    public enum SkillRangeType
    {
        Manhattan,  // 상하좌우 마름모형 (단순 맨해튼 거리)
        Rook,       // 체스의 룩처럼 상하좌우 십자 무한/유한 직선 형태
        Square,     // 사각형 영역 (N x N)
        SelfOnly    // 자기 자신 전용
    }

    public enum SkillSplashType
    {
        Single,     // 스플래시 없음 (단일 타겟)
        Square,     // 대상 중심의 정사각형 확장 영역 (N x N)
        Cross,      // 대상 중심의 직교 상하좌우 십자 연장선 영역
        AllField    // 전장 전체 대상
    }

    public enum TargetType
    {
        EnemySingle,    // 적 단일
        EnemyAll,       // 적 광역
        AllySingle,     // 아군 단일 (아군 공격 스킬 고려 가능)
        AllyAll,        // 아군 광역
        Self,           // 자신
        Anyone          // 피아구분 없음
    }

    public enum EnhanceConditionType
    {
        None,               // 조건 없음 (일반 스킬)
        RoundCount,         // 특정 라운드 이상 경과
        OverheatGauge,      // 과열 게이지 특정치 이상
        CumulativeSP        // 누적 소모 SP 수치 달성   
    }
}