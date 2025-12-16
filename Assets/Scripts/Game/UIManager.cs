using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public Canvas loginCanvas;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // 씬 전환하면 필요
    }

    public void HideLoginUI()
    {
        Debug.Log($"loginCanvas = {(loginCanvas ? "OK" : "NULL")}");
        if (loginCanvas == null)
        {
            Debug.LogError("[UIManager] loginCanvas is null (Inspector 할당 필요)");
            return;
        }
        loginCanvas.gameObject.SetActive(false); // 이게 더 확실
        // loginCanvas.enabled = false; // 이것도 가능
    }
}