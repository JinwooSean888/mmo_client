using UnityEngine;

public class ClientBootstrap : MonoBehaviour
{
    public static ClientBootstrap Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[Client] Bootstrap ready.");
    }
}