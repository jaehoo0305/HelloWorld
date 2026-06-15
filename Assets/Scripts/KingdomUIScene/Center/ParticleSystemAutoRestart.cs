using System.Collections;
using UnityEngine;

/// <summary>
/// 파티클 시스템이 완전히 종료(소멸)되면 중첩 없이 깔끔하게 처음부터 다시 재생해주는 스크립트입니다.
/// Looping 옵션을 켰을 때 이펙트가 무한히 중첩되어 뭉개지는 현상을 완벽히 해결합니다.
/// </summary>
public class ParticleSystemAutoRestart : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("자동 재시작을 제어할 파티클 시스템입니다. 비워두면 본인 오브젝트에서 자동으로 찾습니다.")]
    [SerializeField] private ParticleSystem targetParticleSystem;

    [Header("Restart Settings")]
    [Tooltip("이펙트가 완전히 꺼진 후, 다시 시작할 때까지의 대기 시간(초)입니다.")]
    [SerializeField] private float restartDelay = 0.5f;

    private Coroutine _monitorCoroutine;

    private void Awake()
    {
        // 1. 인스펙터에서 할당하지 않았다면 본인 컴포넌트에서 자동 확보
        if (targetParticleSystem == null)
        {
            targetParticleSystem = GetComponent<ParticleSystem>();
        }

        if (targetParticleSystem == null)
        {
            Debug.LogError($"[AutoRestart] {gameObject.name}에 연결된 ParticleSystem이 없습니다!");
            return;
        }

        // 2. 중요! 이펙트 중첩을 방지하기 위해 강제로 Looping 설정을 코드 상에서 꺼버립니다.
        var mainModule = targetParticleSystem.main;
        mainModule.loop = false;
    }

    private void OnEnable()
    {
        if (targetParticleSystem != null)
        {
            // 오브젝트가 켜지면 모니터링 코루틴 가동
            _monitorCoroutine = StartCoroutine(MonitorParticleRoutine());
        }
    }

    private void OnDisable()
    {
        if (_monitorCoroutine != null)
        {
            StopCoroutine(_monitorCoroutine);
            _monitorCoroutine = null;
        }
    }

    /// <summary>
    /// 파티클의 생존 상태를 감시하고 최적의 타이밍에 재시작을 수행하는 코루틴입니다.
    /// </summary>
    private IEnumerator MonitorParticleRoutine()
    {
        // 첫 시작 시 플레이
        targetParticleSystem.Play(true);

        while (true)
        {
            // targetParticleSystem.IsAlive(true)의 'true'는 하위 자식 파티클들의 생존 여부까지 모두 감시합니다.
            // 모든 파티클 입자가 완전히 사라질 때까지 대기합니다.
            yield return new WaitUntil(() => !targetParticleSystem.IsAlive(true));

            // 지정한 지연 시간이 있다면 대기합니다.
            if (restartDelay > 0f)
            {
                yield return new WaitForSeconds(restartDelay);
            }

            // 자식 파티클들까지 포함하여 처음부터 산뜻하고 깨끗하게 재시작!
            targetParticleSystem.Play(true);

            Debug.Log($"[AutoRestart] '{gameObject.name}' 이펙트가 깔끔하게 재시작되었습니다.");
        }
    }
}