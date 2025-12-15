using UnityEngine;
using UnityEngine.UI;

public class MainUI : MonoBehaviour
{
    public InputField userIdInput;   // 인스펙터에서 연결

    public void OnClick_Connect()
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("NetworkManager.Instance is null");
            return;
        }

        var id = userIdInput != null ? userIdInput.text.Trim() : "";

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("UserId is empty");
            return;
        }

        // ★ 여기서 NetworkManager의 userId 설정
        NetworkManager.Instance.userId = id;

        // 필요하면 토큰도 여기서 만들거나 입력받아도 됨
        // NetworkManager.Instance.token = ...

        NetworkManager.Instance.Connect();
    }
}
