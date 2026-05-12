using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    // 1. 코드 어디서든 접근 가능 (Singleton)
    public static SceneLoader Instance { get; private set; }

    // 3. 유기적인 부가 기능 추가를 위한 이벤트 (Action)
    public static event Action OnLoadStarted;   // 로딩 시작 시 (예: 페이드 아웃)
    public static event Action<float> OnProgress; // 로딩 중 (예: 프로그레스 바)
    public static event Action OnLoadCompleted; // 로딩 완료 시 (예: 페이드 인)

    // 외부에서 로딩 여부를 확인할 수 있도록 프로퍼티 (KingGridMovement 등에서 사용)
    public bool IsLoading { get; private set; } = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 2. 유지보수 비용 제로 (이름 기반 로딩)
    public void LoadScene(string sceneName)
    {
        // 이미 로딩 중이라면 중복 호출을 방지하기 위해 리턴
        if (IsLoading) return;

        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        // 로딩 시작 상태로 변경
        IsLoading = true;

        // 로딩 시작 이벤트 전파
        OnLoadStarted?.Invoke();

        // 부가 기능(예: 페이드 아웃)이 끝날 때까지 대기할 시간이 필요하다면 여기에 추가 가능
        yield return new WaitForSeconds(0.5f);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false; // 로딩 완료 후 바로 전환되지 않게 설정

        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            OnProgress?.Invoke(progress);

            // 로딩이 거의 다 되었을 때 (0.9f)
            if (op.progress >= 0.9f)
            {
                op.allowSceneActivation = true;
            }

            yield return null;
        }

        // 로딩 완료 이벤트 전파
        OnLoadCompleted?.Invoke();

        // 로딩 완료 후 상태 해제
        IsLoading = false;
    }
}

// 로딩 화면 UI를 제어하는 매니저 클래스
public class LoadingScreenManager : MonoBehaviour
{
    [SerializeField] private GameObject loadingUI;

    private void OnEnable()
    {
        // 씬 로더의 이벤트를 구독 (유기적 연결)
        SceneLoader.OnLoadStarted += ShowUI;
        SceneLoader.OnLoadCompleted += HideUI;
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        SceneLoader.OnLoadStarted -= ShowUI;
        SceneLoader.OnLoadCompleted -= HideUI;
    }

    private void ShowUI()
    {
        if (loadingUI != null) loadingUI.SetActive(true);
    }

    private void HideUI()
    {
        if (loadingUI != null) loadingUI.SetActive(false);
    }
}