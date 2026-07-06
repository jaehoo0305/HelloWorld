using UnityEngine;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 전장 격자 위에 생성되는 모든 설치형 장판(효과)의 최상위 컴포넌트입니다.
    /// 파티클 시스템 재생 및 유닛 진입/턴 시작 등의 런타임 트리거를 관리합니다.
    /// </summary>
    public class TileFieldEffect : MonoBehaviour
    {
        [Header("[ 비주얼 설정 ]")]
        [SerializeField] private ParticleSystem effectParticle;

        public Vector2Int Coordinate { get; private set; }
        public string EffectKey { get; private set; }
        public BattleUnit Owner { get; private set; }
        public int Duration { get; private set; }

        /// <summary>
        /// 장판이 격자에 최초 배치될 때 자원을 세팅하고 파티클을 구동합니다.
        /// </summary>
        public virtual void Initialize(Vector2Int coord, string effectKey, BattleUnit owner, int duration)
        {
            Coordinate = coord;
            EffectKey = effectKey;
            Owner = owner;
            Duration = duration;

            if (effectParticle != null)
            {
                effectParticle.Play();
            }
        }

        /// <summary>
        /// 라운드나 턴이 넘어가며 장판의 지속시간이 감소할 때 호출됩니다.
        /// </summary>
        public virtual bool TickDuration()
        {
            if (Duration <= 0) return false; // 무한 지속 장판

            Duration--;
            return Duration <= 0; // true 반환 시 만료되어 파괴되어야 함
        }

        /// <summary>
        /// 적이나 아군이 이 격자 타일을 밟고 지나가거나 들어섰을 때 실행될 고유 로직입니다. (아이사 둔화 기믹 등)
        /// </summary>
        public virtual void OnUnitStepOn(BattleUnit unit) { }

        /// <summary>
        /// 이 격자 위에서 유닛이 턴을 시작할 때 실행될 고유 로직입니다.
        /// </summary>
        public virtual void OnUnitTurnStart(BattleUnit unit) { }

        /// <summary>
        /// 장판이 해제되거나 스킬 소모로 인해 강제 파괴될 때 비주얼을 정리합니다.
        /// </summary>
        public virtual void ClearEffect()
        {
            if (effectParticle != null)
            {
                effectParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            // 파티클 잔상이 자연스럽게 사라지도록 2초 후 오브젝트 완전 파괴
            Destroy(gameObject, 2f);
        }
    }
}