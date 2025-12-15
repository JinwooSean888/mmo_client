using UnityEngine;

public class NetworkSmooth : MonoBehaviour
{
    public float smoothTime = 0.08f;
    public float snapDistance = 3.0f;

    [Header("Ground Snap")]
    public LayerMask groundMask;          // Ground / Terrain 전용
    public float rayStartHeight = 5f;     // 위에서 쏘기
    public float rayDistance = 30f;       // 아래로
    public float footOffset = 0.05f;      // 바닥에 살짝 띄우기

    Vector3 _targetPos;
    Vector3 _vel;
    bool _inited;
    float _fallVelocity;
    public float gravity = -25f;
    float SampleGroundY(Vector3 pos)
    {
        Vector3 origin = pos + Vector3.up * rayStartHeight;

        if (Physics.Raycast(origin,
                            Vector3.down,
                            out RaycastHit hit,
                            rayDistance,
                            groundMask,
                            QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        // ? 실패 시: 무조건 "아래로"
        return pos.y - 0.5f;
    }


    public void SetServerPosition(Vector3 serverPos)
    {
        // ? 기준은 항상 현재 Transform
        Vector3 basePos = transform.position;

        // 서버 권한 XZ
        basePos.x = serverPos.x;
        basePos.z = serverPos.z;

        // Y는 지면에서만 계산 (누적 금지)
        float groundY = SampleGroundY(basePos);
        basePos.y = groundY + footOffset;

        if (!_inited)
        {
            _inited = true;
            _targetPos = basePos;
            transform.position = basePos;
            _vel = Vector3.zero;
            return;
        }

        _targetPos = basePos;

        // 스냅 판정은 XZ 기준이 더 안전
        Vector2 curXZ = new Vector2(transform.position.x, transform.position.z);
        Vector2 tgtXZ = new Vector2(_targetPos.x, _targetPos.z);

        if (Vector2.Distance(curXZ, tgtXZ) > snapDistance)
        {
            transform.position = _targetPos;
            _vel = Vector3.zero;
        }
    }
    void LateUpdate()
    {
        if (!_inited) return;

        Vector3 pos = transform.position;

        // XZ만 부드럽게
        Vector3 smoothXZ = Vector3.SmoothDamp(
            new Vector3(pos.x, 0, pos.z),
            new Vector3(_targetPos.x, 0, _targetPos.z),
            ref _vel,
            smoothTime
        );

        // Y는 절대 보간하지 말고 고정
        transform.position = new Vector3(
            smoothXZ.x,
            _targetPos.y,
            smoothXZ.z
        );
    }




}
