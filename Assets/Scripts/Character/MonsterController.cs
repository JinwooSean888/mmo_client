using UnityEngine;

public class MonsterController : MonoBehaviour
{
    [Header("Ground")]
    public LayerMask groundMask;
    public float rayStartUp = 50f;
    public float rayDown = 200f;
    public float groundOffset = 0.05f;

    [Header("Move")]
    public float gravity = -25f;      // 공중에 떠있을 때 아래로 끌어주는 값
    public float maxFallSpeed = -60f; // 낙하 속도 제한
    public float snapDistance = 3.0f; // 서버 좌표가 너무 멀면 보간하지 말고 워프

    private CharacterController _cc;
    private float _vy;                // 현재 수직 속도(Y축)

    // 서버에서 받은 "목표 좌표"
    public Vector3 TargetPos;
    // 목표 좌표가 유효한지 여부
    public bool HasTarget;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();

        // 인스펙터에서 안 넣었으면 기본적으로 Ground 레이어를 사용
        if (groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground");
    }

    // 스폰 직후나, 위치가 크게 튀었을 때 강제로 자리 잡는 용도
    // (이때는 바닥 높이도 같이 맞춰서 넣는다)
    public void WarpTo(Vector3 serverPos)
    {
        Vector3 pos = serverPos;

        // 서버는 보통 XZ만 의미 있고, Y는 클라에서 지형 기준으로 맞춘다
        if (TrySampleGroundY(pos, out float y))
            pos.y = y + groundOffset;

        // CharacterController 켜진 상태에서 transform.position 직접 넣으면 꼬일 때가 있어서 토글
        if (_cc != null) _cc.enabled = false;
        transform.position = pos;
        if (_cc != null) _cc.enabled = true;

        // 텔레포트 후에는 낙하 속도 리셋
        _vy = 0f;
    }

    // 서버에서 계속 오는 좌표를 "따라가는" 이동
    // 큰 오차면 워프, 작은 오차면 CC.Move로 부드럽게 따라간다
    public void MoveTo(Vector3 serverPos, float dt)
    {
        if (_cc == null)
        {
            transform.position = serverPos;
            return;
        }

        Vector3 cur = transform.position;

        // 수평 이동량(XZ)만 계산
        Vector3 deltaXZ = new Vector3(serverPos.x - cur.x, 0f, serverPos.z - cur.z);

        // 너무 멀면 중간 보간 의미 없고, 그냥 워프하는 게 더 안정적
        if (deltaXZ.sqrMagnitude > snapDistance * snapDistance)
        {
            WarpTo(serverPos);
            return;
        }

        // 서버 좌표 기준으로 바닥 높이를 다시 샘플링해서 목표 Y 만들기
        float targetY = cur.y;
        if (TrySampleGroundY(serverPos, out float groundY))
            targetY = groundY + groundOffset;

        // 수평은 서버 delta를 그대로 따라감 (원하면 여기서 Lerp/SmoothDamp로 더 부드럽게 가능)
        Vector3 move = deltaXZ;

        // 목표 Y와 현재 Y 차이
        float dy = targetY - cur.y;

        // 바닥에 붙어있으면 살짝 아래로 누르는 값(-1) 정도 주는 게 흔들림이 덜함
        if (_cc.isGrounded)
        {
            _vy = -1f;
        }
        else
        {
            _vy += gravity * dt;
            if (_vy < maxFallSpeed) _vy = maxFallSpeed;
        }

        // 지형이 위로 올라가는 상황(경사/턱/언덕)은 바로 맞춰주는 편이 안정적
        if (dy > 0f)
            _vy = Mathf.Max(_vy, dy / Mathf.Max(dt, 0.0001f));

        move.y = _vy;

        // dt를 곱해서 실제 프레임 이동량으로 만든 뒤 CC로 이동
        _cc.Move(move * dt);
    }

    // 해당 위치에서 아래로 Raycast해서 바닥 높이를 얻는다
    private bool TrySampleGroundY(Vector3 pos, out float y)
    {
        Vector3 origin = pos + Vector3.up * rayStartUp;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
            rayStartUp + rayDown, groundMask, QueryTriggerInteraction.Ignore))
        {
            y = hit.point.y;
            return true;
        }

        y = pos.y;
        return false;
    }

    // 서버에서 목표 좌표가 들어오는 동안은 매 프레임 따라가기
    void Update()
    {
        if (!HasTarget) return;
        MoveTo(TargetPos, Time.deltaTime);
    }
}
