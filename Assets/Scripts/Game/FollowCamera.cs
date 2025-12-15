using UnityEngine;

[RequireComponent(typeof(Camera))]
public class QuarterViewCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                      // 현재 추적 대상 (로그인 후 LocalPlayer)
    public string localPlayerTag = "LocalPlayer"; // 런타임 생성 LocalPlayer 태그
    public Transform fallbackTarget;              // 로그인 전 맵을 보여줄 더미 타겟(예: CameraAnchor)

    [Header("Pivot")]
    public Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);
    public float pivotSmoothTime = 0.10f;         // 떨림 완화 핵심
    public float teleportCutDistance = 3.0f;      // 서버 큰 보정은 스냅

    [Header("Angle")]
    public float yaw = 0f;
    public float pitch = 35f;

    [Header("Distance")]
    public float distance = 22f;
    public float minDistance = 10f;
    public float maxDistance = 45f;
    public float zoomSpeed = 6f;

    [Header("Rotate")]
    public float rotateSpeed = 900f;
    public bool rotateWhileRightMouse = true;
    public float rightMouseSpeedMul = 3.0f;       // 우클릭 가속 배수

    [Header("Camera Smooth")]
    public float cameraSmoothTime = 0.10f;

    // internal
    Vector3 _pivotPos;
    Vector3 _pivotVel;
    Vector3 _camVel;
    bool _inited;

    Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void Start()
    {
        // 로그인 전 맵을 보여주고 싶으니 fallbackTarget을 확보
        if (fallbackTarget == null)
        {
            var anchor = GameObject.Find("CameraAnchor");
            if (anchor) fallbackTarget = anchor.transform;
        }

        if (target == null && fallbackTarget != null)
            BindTarget(fallbackTarget, resetYaw: false);

        // 디버그: 시작 시점 LocalPlayer는 보통 0이 정상 (로그인 후 생성이니까)
        var locals = GameObject.FindGameObjectsWithTag(localPlayerTag);
        Debug.Log($"[Cam] Start LocalPlayer count={locals.Length}, target={(target ? target.name : "null")}, fallback={(fallbackTarget ? fallbackTarget.name : "null")}");
    }

    /// <summary>
    /// 외부(로그인 성공/플레이어 생성)에서 호출 권장.
    /// resetYaw=false면 현재 시야(회전 각도)를 유지하면서 타겟만 교체.
    /// </summary>
    public void BindTarget(Transform newTarget, bool resetYaw = false)
    {
        if (newTarget == null) return;

        target = newTarget;

        // 피벗/속도 초기화(점프/떨림 최소화)
        _pivotPos = target.position + targetOffset;
        _pivotVel = Vector3.zero;
        _camVel = Vector3.zero;

        if (resetYaw)
            _inited = false; // 다음 LateUpdate에서 yaw 재계산
        else
            _inited = true;  // yaw 유지하고 바로 따라가게

        // 카메라는 항상 켜 둠(로그인 전에도 맵 보여줘야 하니까)
        if (_cam && !_cam.enabled) 
            _cam.enabled = true;

        var pc = target.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.SetCamera(_cam);
        }

        Debug.Log($"[Cam] BindTarget => {target.name} (resetYaw={resetYaw})");
    }

    void LateUpdate()
    {
        // 1) 로그인 후 LocalPlayer가 생기면 자동으로 타겟 교체
        //    (외부에서 BindTarget 호출해주면 이 루프는 사실상 보험용)
        if (target == null || target == fallbackTarget)
        {
            var go = GameObject.FindGameObjectWithTag(localPlayerTag);
            if (go && go.transform != target)
            {
                // 시야 유지하면서 LocalPlayer로 스위치
                BindTarget(go.transform, resetYaw: false);
            }
        }

        // 2) 타겟이 끝까지 없다면(Anchor도 없고 플레이어도 없음) 그냥 종료
        if (target == null)
            return;

        // Pivot 계산
        Vector3 rawPivot = target.position + targetOffset;

        if (!_inited)
        {
            _pivotPos = rawPivot;

            // 시작 yaw: 현재 카메라 위치에서 계산 (점프 방지)
            Vector3 pivotToCam = (transform.position - _pivotPos);
            pivotToCam.y = 0f;
            if (pivotToCam.sqrMagnitude > 0.001f)
                yaw = Quaternion.LookRotation(pivotToCam.normalized, Vector3.up).eulerAngles.y;

            _inited = true;
        }
        else
        {
            float jump = Vector3.Distance(_pivotPos, rawPivot);
            if (jump > teleportCutDistance)
            {
                _pivotPos = rawPivot;
                _pivotVel = Vector3.zero;
            }
            else
            {
                _pivotPos = Vector3.SmoothDamp(_pivotPos, rawPivot, ref _pivotVel, pivotSmoothTime);
            }
        }

        // Zoom
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.001f)
        {
            distance -= wheel * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        // Rotate
        bool rotating = !rotateWhileRightMouse || Input.GetMouseButton(1);
        if (rotating)
        {
            float mx = Input.GetAxis("Mouse X");
            float mul = Input.GetMouseButton(1) ? rightMouseSpeedMul : 1.0f;
            yaw += mx * rotateSpeed * mul * Time.deltaTime;
        }

        // Camera placement
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = _pivotPos + rot * (Vector3.back * distance);

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _camVel, cameraSmoothTime);
        transform.rotation = Quaternion.LookRotation((_pivotPos - transform.position).normalized, Vector3.up);
    }
}
