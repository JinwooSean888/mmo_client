using UnityEngine;

[RequireComponent(typeof(Camera))]
public class QuarterViewCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public string localPlayerTag = "LocalPlayer";
    public Transform fallbackTarget;

    [Header("Pivot")]
    public Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);
    public float pivotSmoothTime = 0.10f;
    public float teleportCutDistance = 3.0f;

    [Header("Angle")]
    public float yaw = 0f;
    public float pitch = 35f;

    [Header("Pitch Clamp")]
    public float minPitch = 15f;
    public float maxPitch = 75f;
    public bool invertY = false;

    [Header("Distance")]
    public float distance = 22f;
    public float minDistance = 10f;
    public float maxDistance = 45f;
    public float zoomSpeed = 6f;

    [Header("Rotate")]
    public float rotateSpeed = 180f;          // (권장) 초당 각도 느낌으로 낮춤
    public bool rotateWhileRightMouse = true; // 우클릭 중에만 회전
    public float rightMouseSpeedMul = 1.0f;   // 우클릭 가속(원하면 2~3)

    [Header("Right Mouse Lock")]
    public bool lockCursorWhileRotating = true;

    [Header("Camera Smooth")]
    public float cameraSmoothTime = 0.10f;

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
        if (fallbackTarget == null)
        {
            var anchor = GameObject.Find("CameraAnchor");
            if (anchor) fallbackTarget = anchor.transform;
        }

        if (target == null && fallbackTarget != null)
            BindTarget(fallbackTarget, resetYaw: false);

        var locals = GameObject.FindGameObjectsWithTag(localPlayerTag);
        Debug.Log($"[Cam] Start LocalPlayer count={locals.Length}, target={(target ? target.name : "null")}, fallback={(fallbackTarget ? fallbackTarget.name : "null")}");
    }

    public void BindTarget(Transform newTarget, bool resetYaw = false)
    {
        if (newTarget == null) return;

        target = newTarget;

        _pivotPos = target.position + targetOffset;
        _pivotVel = Vector3.zero;
        _camVel = Vector3.zero;

        _inited = !resetYaw;

        if (_cam && !_cam.enabled)
            _cam.enabled = true;

        var pc = target.GetComponent<PlayerController>();
        if (pc != null) pc.SetCamera(_cam);

        Debug.Log($"[Cam] BindTarget => {target.name} (resetYaw={resetYaw})");
    }

    void LateUpdate()
    {
        if (target == null || target == fallbackTarget)
        {
            var go = GameObject.FindGameObjectWithTag(localPlayerTag);
            if (go && go.transform != target)
                BindTarget(go.transform, resetYaw: false);
        }

        if (target == null) return;

        Vector3 rawPivot = target.position + targetOffset;

        if (!_inited)
        {
            _pivotPos = rawPivot;

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

        // ===== Rotate with Right Mouse Drag =====
        bool rotating = rotateWhileRightMouse ? Input.GetMouseButton(1) : true;

        // 우클릭 눌렀을 때 커서 잠금/해제 (드래그 회전 느낌 개선)
        if (rotateWhileRightMouse && lockCursorWhileRotating)
        {
            if (Input.GetMouseButtonDown(1))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Input.GetMouseButtonUp(1))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        if (rotating)
        {
            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            float mul = (rotateWhileRightMouse && Input.GetMouseButton(1)) ? rightMouseSpeedMul : 1.0f;

            yaw += mx * rotateSpeed * mul * Time.deltaTime;

            float ySign = invertY ? 1f : -1f;          // 기본: 위로 움직이면 pitch 증가(카메라 내려다봄)
            pitch += my * rotateSpeed * mul * ySign * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // Camera placement
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = _pivotPos + rot * (Vector3.back * distance);

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _camVel, cameraSmoothTime);
        transform.rotation = Quaternion.LookRotation((_pivotPos - transform.position).normalized, Vector3.up);
    }
}
