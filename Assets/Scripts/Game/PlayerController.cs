using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;   // 지금은 직접 이동에 안 씀 (원하면 나중에 클라 연출용으로만 사용)

    private Vector2 _lastSentInput;
    private float _lastSendTime;

    private Animator _anim;
    PlayerHudUI _hud;
    private bool _isMovingServer;
    private float _lastServerMoveTime;

    public float stopTimeout = 0.25f;
    private Camera _cam;
    public bool IsLocal;

    [Header("PlayerState")]
    public int Hp { get; private set; }
    public int MaxHp { get; private set; }
    public int Sp { get; private set; }
    public int MaxSp { get; private set; }    
    void Start()
    {
        _lastSentInput = Vector2.zero;
        _lastSendTime = 0f;

        _anim = GetComponentInChildren<Animator>(); // 자식 본에 있어도 찾도록
        Debug.Log($"[ANIM] obj={_anim.gameObject.name}, root={_anim.transform.root.name}, controller={_anim.runtimeAnimatorController?.name}");
        _cam = Camera.main;
        _hud = GetComponentInChildren<PlayerHudUI>();
    }
    public void SetCamera(Camera cam)
    {
        _cam = cam;
    }
    public void SetServerMoving(bool moving)
    {
        if (moving)
        {
            _lastServerMoveTime = Time.time;
        }
        _isMovingServer = moving;
        // Debug.Log($"[PAL] SetServerMoving: {moving}");
    }
    public void ApplyServerStats(int hp, int maxHp, int sp, int maxSp)
    {
        Hp = hp;
        MaxHp = maxHp;
        Sp = sp;
        MaxSp = maxSp;


        if (_hud != null)
        {
            _hud.ApplyStats(hp, maxHp, sp, maxSp);
        }
    }

    // 스킬 입력은 PlayerController 와 똑같이 LateUpdate 에서 처리
    void LateUpdate()
    {
        if (!IsLocal)
            return;
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var targetId = AoiWorld.FindClosestMonster(transform.position, 2.0f);
            if ( NetworkManager.Instance != null && NetworkManager.Instance._inField)
                NetworkManager.Instance.SendSkillAttack(targetId);

            if (_anim != null)
                _anim.SetTrigger("Combo01"); // 파라미터명
        }
    }
    bool CanMove()
    {
        if (_anim == null) return true;

        var st = _anim.GetCurrentAnimatorStateInfo(0);
        return st.IsTag("Locomotion");   // idle walk등 상태일 때만 이동 허용
    }
    void Update()
    {
        if (!IsLocal)
               return;
        if (!CanMove())
       {
        // 콤보/공격/피격 중 → 방향키, 서버 이동 전송 전부 차단
            return;
        }
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // =========================
        // 카메라 기준 회전 방향 계산
        // =========================
        Vector3 moveDir = Vector3.zero;


        if (_cam != null)
        {
            Vector3 camForward = _cam.transform.forward;
            Vector3 camRight = _cam.transform.right;

            // 수평면 기준으로만 사용
            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            // W/S = 카메라 전후, A/D = 카메라 좌우
            moveDir = camForward * v + camRight * h;
        }
        else
        {
            // 카메라 없을 때 fallback
            moveDir = new Vector3(h, 0f, v);
        }

        // 실제로 입력이 있을 때만 회전
        if (moveDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(moveDir.normalized);
        }
        Vector2 input2D = new Vector2(moveDir.x, moveDir.z);
        if (_anim != null)
        {
            bool effectiveMoving = _isMovingServer &&
                       (Time.time - _lastServerMoveTime <= stopTimeout);

            float targetSpeed = effectiveMoving ? 1f : 0f;
            _anim.SetFloat("Speed", targetSpeed);
        }

        // -------------------------
        // 3) 서버로 입력만 전송 (PlayerController 와 동일 로직)
        // -------------------------
        if (NetworkManager.Instance != null && NetworkManager.Instance._inField)
        {
            // 1) 입력이 거의 0일 때: 멈출 때만 STOP 패킷 한 번 보내기
            if (input2D.sqrMagnitude < 0.01f)
            {
                if (_lastSentInput.sqrMagnitude >= 0.01f)
                {
                    NetworkManager.Instance.SendFieldMove(Vector2.zero);
                    _lastSentInput = Vector2.zero;
                    _lastSendTime = Time.time;
                }
                return;
            }

            // 2) 입력이 0이 아닐 때만, 방향 변화/시간 기준으로 전송
            bool dirChanged = (input2D - _lastSentInput).sqrMagnitude > 0.001f;
            bool timePassed = Time.time - _lastSendTime > 0.1f;

            if (dirChanged || timePassed)
            {
                NetworkManager.Instance.SendFieldMove(input2D);
                _lastSentInput = input2D;
                _lastSendTime = Time.time;
            }
        }
    }
}
