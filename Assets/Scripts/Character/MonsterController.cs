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

    [Header("Anim")]
    public string speedParam = "Speed";   // Animator Float 파라미터 이름
    public float speedScale = 1.0f;       // v(m/s) -> 애니 Speed 스케일
    public float movingEps = 0.01f;       // 너무 작은 떨림 제거

    private Animator _anim;

    [Header("Rotate")]
    public bool rotateToMoveDir = true;
    public float rotateSpeed = 720f;     // deg/sec
    public float rotateEps = 0.001f;     // 너무 작은 방향 변화 무시


    [Header("MonsterState")]
    public int Hp { get; private set; }
    public int MaxHp { get; private set; }
    public int Sp { get; private set; }
    public int MaxSp { get; private set; }
    public MonsterAIState AiState { get; private set; }

    MonsterHudUI _hud;
    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _anim = GetComponentInChildren<Animator>();
        _hud = GetComponentInChildren<MonsterHudUI>();
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
            if (_anim) _anim.SetFloat(speedParam, 0f);
            return;
        }

        Vector3 cur = transform.position;
        Vector3 deltaXZ = new Vector3(serverPos.x - cur.x, 0f, serverPos.z - cur.z);

        // ★ 이동 방향으로 회전
        if (rotateToMoveDir && deltaXZ.sqrMagnitude > rotateEps)
        {
            Quaternion targetRot = Quaternion.LookRotation(deltaXZ.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                rotateSpeed * dt
            );
        }

        // ★ 애니 Speed 계산 (m/s)
        float v = deltaXZ.magnitude / Mathf.Max(dt, 0.0001f);
        float animSpeed = (v > movingEps) ? (v * speedScale) : 0f;
        if (_anim) _anim.SetFloat(speedParam, animSpeed);

        if (deltaXZ.sqrMagnitude > snapDistance * snapDistance)
        {
            WarpTo(serverPos);
            if (_anim) _anim.SetFloat(speedParam, 0f);
            return;
        }

        float targetY = cur.y;
        if (TrySampleGroundY(serverPos, out float groundY))
            targetY = groundY + groundOffset;

        Vector3 move = deltaXZ;
        float dy = targetY - cur.y;

        if (_cc.isGrounded) _vy = -1f;
        else
        {
            _vy += gravity * dt;
            if (_vy < maxFallSpeed) _vy = maxFallSpeed;
        }

        if (dy > 0f)
            _vy = Mathf.Max(_vy, dy / Mathf.Max(dt, 0.0001f));

        move.y = _vy;
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
        if (!HasTarget)
        {
            if (_anim) _anim.SetFloat(speedParam, 0f);
            return;
        }
        MoveTo(TargetPos, Time.deltaTime);
    }

    // 서버에서 몬스터 상태 패킷 들어왔을 때 호출
    public void ApplyServerState(int hp, int maxHp, int sp, int maxSp, MonsterAIState state)
    {
        Hp = hp;
        MaxHp = maxHp;
        Sp = sp;
        MaxSp = maxSp;
        AiState = state;

        if (_hud != null)
        {
            _hud.SetMaxStats(MaxHp, MaxSp);
            _hud.SetHpSp(Hp, Sp);
            _hud.SetAIState(AiState);
        }
    }
    // 상태패킷 처리
    //void OnRecvMonsterState(S_MonsterState msg)
    //{
    //    // id → MonsterController 찾는 딕셔너리 있다고 가정
    //    if (_monsters.TryGetValue(msg.Id, out var monster))
    //    {
    //        monster.ApplyServerState(
    //            msg.Hp, msg.MaxHp,
    //            msg.Sp, msg.MaxSp,
    //            (MonsterAIState)msg.AiState
    //        );
    //    }
    //}

}
