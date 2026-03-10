using UnityEngine;

public class PauseObserver : MonoBehaviour
{
    [Header("Options")]
    [SerializeField] private bool pauseGlobale = true;
    private float previousTimeScale = 1f;
    private float defaultFixedDeltaTime;

    private void Awake()
    {
        previousTimeScale = Time.timeScale;
        defaultFixedDeltaTime = Time.fixedDeltaTime; 
    }

    private void OnEnable()
    {
        PlayerManager.OnMenuStateChanged += OnMenuChanged;
    }

    private void OnDisable()
    {
        PlayerManager.OnMenuStateChanged -= OnMenuChanged;
    }

    private void OnMenuChanged(bool menuOn)
    {
        if (!pauseGlobale) return;

        if (menuOn)
        {
            previousTimeScale = Time.timeScale; 
            SetTimeScale(0f);                   
        }
        else
        {
            var restore = (previousTimeScale <= 0f) ? 1f : previousTimeScale;
            SetTimeScale(restore);
        }
    }

    private void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * scale;
    }
}