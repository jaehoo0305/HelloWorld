using System.Collections;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 적 성향별 AI(공격형, 방어형, 도망형)들이 턴 제어권을 받았을 때 
    /// 공통적으로 작동해야 하는 시퀀스 인터페이스 규격입니다.
    /// </summary>
    public interface IEnemyAIBehavior
    {
        /// <summary>
        /// 지정된 AI 성향에 따라 타겟을 선정하고, A* 이동 및 스킬 시전을 수행하는 코루틴 행동 처리기입니다.
        /// </summary>
        IEnumerator ExecuteBehavior(EnemyUnit enemy, BattleGridManager gridManager, BattleTurnManager turnManager);
    }
}