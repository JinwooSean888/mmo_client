using System.Collections.Generic;
using UnityEngine;
using field;

public static class AoiWorld
{
    // ================== 상태 ==================
    static readonly Dictionary<ulong, GameObject> players = new();
    static readonly Dictionary<ulong, GameObject> monsters = new();
    static readonly Dictionary<string, GameObject> prefabCache = new();

    static readonly Dictionary<ulong, string> monsterPrefabById = new();
    static readonly Dictionary<ulong, Vector3> pendingMonsterPos = new();

    public static ulong MyPlayerId;

    static bool _hasLastMyPos;
    static Vector3 _lastMyPos;
    static float _lastMoveTime;

    static GameObject _playerTemplate;
    const string DefaultMonsterPrefab = "SingleTwoHandSwordTemplate_1";

    static Dictionary<ulong, float> _lastFaceTime = new Dictionary<ulong, float>();
    const float FaceCooltime = 0.1f; // 0.1초마다 한 번만 회전 허용

    // ================== 템플릿 ==================
    static GameObject GetPlayerTemplate()
    {
        if (_playerTemplate == null)
            _playerTemplate = Resources.Load<GameObject>("Player/SingleTwoHandSwordTemplate_1");
        return _playerTemplate;
    }

    static GameObject GetMonsterPrefab(string name)
    {
        if (string.IsNullOrEmpty(name))
            name = DefaultMonsterPrefab;

        if (prefabCache.TryGetValue(name, out var p))
            return p;

        var loaded = Resources.Load<GameObject>($"Monster/{name}");
        if (loaded != null)
            prefabCache[name] = loaded;

        return loaded;
    }

    // ================== 좌표 ==================
    static Vector3 ToWorldPos(Vec2 p) => new(p.X, 0f, p.Y);

    // ================== 진입 ==================
    public static void ApplyFieldCmd(FieldCmd cmd)
    {
        if (!cmd.Pos.HasValue)
        {
            Debug.LogWarning($"[FieldCmd] Pos is missing. type={cmd.Type}, id={cmd.EntityId}, et={cmd.EntityType}");
            return;
        }
        
        ulong id = cmd.EntityId;
        bool isMonster = (cmd.EntityType == EntityType.Monster);
        Vector3 pos = ToWorldPos(cmd.Pos.Value);

        switch (cmd.Type)
        {
            case FieldCmdType.Enter:
                OnEnter(id, pos, isMonster, cmd.Prefab);
                break;
            case FieldCmdType.Leave:
                OnLeave(id, isMonster);
                break;
            case FieldCmdType.Move:
                OnMove(id, pos, isMonster);
                break;
        }

        // 내 캐릭 이동 판정
        if (!isMonster && id == MyPlayerId && cmd.Type == FieldCmdType.Move)
        {
            // 유저 움직이는지 판별 (에니메이션 실행)
            if (players.TryGetValue(id, out var go) && go != null)
            {
                var pc = go.GetComponent<PlayerController>();
                if (pc != null)
                    pc.SetServerMoving(true);
            }
            if (!_hasLastMyPos)
            {
                _lastMyPos = pos;
                _hasLastMyPos = true;
            }

            float d = (new Vector2(pos.x, pos.z) -
                       new Vector2(_lastMyPos.x, _lastMyPos.z)).sqrMagnitude;

            _lastMoveTime = d > 0.0001f ? Time.time : _lastMoveTime;
            _lastMyPos = pos;
        }
    }
    static void FaceToTarget(GameObject attackerGo, GameObject targetGo)
    {
        if (attackerGo == null || targetGo == null)
            return;

        var from = attackerGo.transform.position;
        var to = targetGo.transform.position;

        var dir = to - from;
        dir.y = 0f; // 상하 회전은 무시 (수평만 회전)

        if (dir.sqrMagnitude < 0.0001f)
            return;

        // 바라봐야 하는 방향
        var lookRot = Quaternion.LookRotation(dir);

        // Y축만 사용 (기울어지지 않게)
        var targetRot = Quaternion.Euler(0f, lookRot.eulerAngles.y, 0f);


         //targetRot *= Quaternion.Euler(0f, 180f, 0f);

        // 부드럽게 회전하고 싶으면 Slerp 사용
        var currentRot = attackerGo.transform.rotation;
        float lerp = 0.35f; // 0.2~0.4 사이로 조절해봐
        attackerGo.transform.rotation =
            Quaternion.Slerp(currentRot, targetRot, lerp);
    }
    public static void ApplyCombatEvent(CombatEvent ev)
    {
        ulong attackerId = ev.AttackerId;
        ulong targetId = ev.TargetId;

        bool attackerIsMonster = (ev.AttackerType == EntityType.Monster);
        bool targetIsMonster = (ev.TargetType == EntityType.Monster);

        GameObject attackerGo = attackerIsMonster
            ? monsters.GetValueOrDefault(attackerId)
            : players.GetValueOrDefault(attackerId);

        GameObject targetGo = targetIsMonster
            ? monsters.GetValueOrDefault(targetId)
            : players.GetValueOrDefault(targetId);

        if (attackerGo == null)
            return;

        // 1) 타겟이 있으면 방향 맞추고
        if (targetGo != null)
        {
            float now = Time.time;
            if (!_lastFaceTime.TryGetValue(attackerId, out var lastT) ||
                now - lastT >= FaceCooltime)
            {
                FaceToTarget(attackerGo, targetGo);
                _lastFaceTime[attackerId] = now;
            }
        }
        else
        {
            Debug.LogWarning($"[COMBAT] targetGo null: atk={attackerId}, tgt={targetId}");
        }

        // 2)  애니는 항상 실행
        var anim = attackerGo.GetComponentInChildren<Animator>();
        if (anim != null)
            anim.SetBool("IsAttacking", true);
    }


    public static void ApplyAiState(AiStateEvent ev)
    {
        ulong id = ev.EntityId;
        bool isMonster = (ev.EntityType == EntityType.Monster);
        var netState = ev.State;   // field.AiStateType

        if (isMonster)
        {
            // ===== 몬스터 처리 (기존 그대로) =====
            if (!monsters.TryGetValue(id, out var go) || go == null)
            {
                Debug.LogWarning($"[MONSTER][ApplyAiState] monster not found. id={id}");
                return;
            }

            // field.AiStateType -> MonsterAIState 매핑
            MonsterAIState st = ConvertState(netState);

            Debug.Log($"[MONSTER][ApplyAiState] id={id}, state={netState} => {st}");

            // 머리 위 HUD
            var hud = go.GetComponentInChildren<MonsterHudUI>();
            if (hud != null)
                hud.SetAIState(st);

            // 애니메이션
            var anim = go.GetComponentInChildren<MonsterAnimController>();
            if (anim != null)
            {
                Debug.Log($"[AI] Monster {st} state={st}");
                anim.ApplyAIState(st);
            }

            return;
        }

        // ====== 여기부터 플레이어 처리 ======
        if (!players.TryGetValue(id, out var pgo) || pgo == null)
        {
            Debug.LogWarning($"[PLAYER][ApplyAiState] player not found. id={id}");
            return;
        }

        Debug.Log($"[PLAYER][ApplyAiState] id={id}, state={netState}");

        // 1) 애니메이션 컨트롤러에 전달
        var pAnim = pgo.GetComponentInChildren<PlayerAnimController>();
        if (pAnim != null)
        {
            // PlayerAnimController에서 field.AiStateType 그대로 받게 해도 되고,
            // 필요하면 내부에서 AiStateType -> 자체 enum 매핑
            pAnim.ApplyNetworkState(netState);
        }

        // 2) 로컬 플레이어 이동 플래그까지 서버 상태로 맞추고 싶다면:
        if (id == MyPlayerId)
        {
            var pc = pgo.GetComponent<PlayerController>();
            if (pc != null)
            {
                bool moving =
                    netState == AiStateType.Patrol ||
                    netState == AiStateType.Move ||
                    netState == AiStateType.Return;

                pc.SetServerMoving(moving);
            }
        }
    }


    private static MonsterAIState ConvertState(AiStateType s)
    {
        switch (s)
        {
            case AiStateType.Idle: return MonsterAIState.Idle;
            case AiStateType.Patrol: return MonsterAIState.Patrol;
            case AiStateType.Move: return MonsterAIState.Move;
            case AiStateType.Attack: return MonsterAIState.Attack;
            case AiStateType.Return: return MonsterAIState.Return;
            case AiStateType.Dead: return MonsterAIState.Dead;
            default: return MonsterAIState.Idle;
        }
    }
    public static void ForceAllMonstersOff(string reason)
    {
        foreach (var kv in monsters)
        {
            if (kv.Value != null && kv.Value.activeSelf)
            {
                kv.Value.SetActive(false);
                Debug.Log($"[MONSTER][FORCE OFF] {kv.Value.name} reason={reason}");
            }
        }
    }
    static void SetMonsterActive(GameObject go, bool active, ulong id, string reason)
    {
        if (go == null) return;

        if (go.activeSelf == active)
        {
            // 이미 같은 상태인데도 호출됐다 = 누가 계속 만지고 있다
            if (active)
            {
                Debug.Log($"[MONSTER][ALREADY ON] id={id} name={go.name} reason={reason}\n{System.Environment.StackTrace}");
            }
            return;
        }

        go.SetActive(active);

        if (active)
        {
            Debug.Log($"[MONSTER][ON] id={id} name={go.name} reason={reason}\n{System.Environment.StackTrace}");
        }
        else
        {
            Debug.Log($"[MONSTER][OFF] id={id} name={go.name} reason={reason}");
        }
    }

    // ================== Enter ==================
    static void OnEnter(ulong id, Vector3 pos, bool isMonster, string prefabName)
    {

        Debug.Log($"[OnEnter] my={AoiWorld.MyPlayerId} enterId={id} isMonster={isMonster} pos={pos}");
        if (isMonster)
        {
            // prefabName은 항상 캐시
            if (!string.IsNullOrEmpty(prefabName))
                monsterPrefabById[id] = prefabName;

            // pending 좌표 우선
            Vector3 spawnPos =
                pendingMonsterPos.TryGetValue(id, out var pending) ? pending : pos;

            // 이미 있으면: AOI 안으로 들어온 것이므로 켜기만
            if (monsters.TryGetValue(id, out var m))
            {
                if (m != null)
                {
                    if (!m.activeSelf)
                    {
                        SetMonsterActive(m, true, id, "AOI Enter/Snapshot reuse");
                       // m.SetActive(true);
                    }

                    WarpMonster(m, spawnPos);
                    pendingMonsterPos.Remove(id);
                    return;
                }

                // key는 있는데 null이면 정리
                monsters.Remove(id);
            }

            // 새로 생성 (AOI Enter/Snapshot일 때만)
            if (!monsterPrefabById.TryGetValue(id, out var pf))
                return;

            var prefab = GetMonsterPrefab(pf);
            if (prefab == null) return;

            var go = Object.Instantiate(prefab);
            go.name = $"Monster_{id}";
            //SetMonsterActive(go, true, id, "AOI Enter/Snapshot new");
             go.SetActive(false);

            monsters[id] = go;
            WarpMonster(go, spawnPos);
            pendingMonsterPos.Remove(id);
            return;
        }


        // ---------- 플레이어 ----------
        if (players.ContainsKey(id))
            return;

        var tpl = GetPlayerTemplate();
        if (tpl == null) return;

        var inst = Object.Instantiate(tpl);
        inst.name = $"Player_{id}";

        var ns = inst.GetComponent<NetworkSmooth>() ?? inst.AddComponent<NetworkSmooth>();
        ns.IsLocal = (id == AoiWorld.MyPlayerId);
        ns.SetServerPosition(pos);

        bool isSelf = (AoiWorld.MyPlayerId != 0 && id == AoiWorld.MyPlayerId);

        if (isSelf)
        {
            // 기존 LocalPlayer 태그 정리 (중복 방지)
            var olds = GameObject.FindGameObjectsWithTag("LocalPlayer");
            foreach (var old in olds)
            {
                if (old != null)
                    old.tag = "Untagged";
            }

            inst.tag = "LocalPlayer";

            // 카메라에 즉시 바인딩
            var cam = Object.FindObjectOfType<QuarterViewCamera>();
            if (cam != null)
            {
                cam.BindTarget(inst.transform, resetYaw: false);
            }
        }
        else
        {
            //inst.SetActive(false); // 비활성화
            inst.tag = "Untagged";
        }        

        players[id] = inst;



        var ctrl = inst.GetComponent<PlayerController>();
        if (ctrl != null)
        {
            ctrl.IsLocal = isSelf;
        }
    }


    // ================== Move ==================
    static void OnMove(ulong id, Vector3 pos, bool isMonster)
    {
        if (isMonster)
        {
            // AOI 밖이거나 꺼진 몬스터는 절대 이동 반영 X
            if (!monsters.TryGetValue(id, out var m) || m == null || !m.activeSelf)
            {
                pendingMonsterPos[id] = pos; // 좌표만 저장
                return;
            }

            MoveMonster(m, pos);
            return;
        }

        if (players.TryGetValue(id, out var p) && p != null)
            p.GetComponent<NetworkSmooth>()?.SetServerPosition(pos);
    }

    // ================== Leave ==================
    static void OnLeave(ulong id, bool isMonster)
    {
        if (isMonster)
        {
            if (monsters.TryGetValue(id, out var m) && m != null)
                SetMonsterActive(m, false, id, "AOI Leave");
            //m.SetActive(false);

            // (선택) pending pos 정리하면 메모리 깔끔
            pendingMonsterPos.Remove(id);
            return;
        }

        if (players.TryGetValue(id, out var p) && p != null)
        {
            players.Remove(id);
            Object.Destroy(p);
        }
    }


    // ================== 몬스터 이동 ==================
    static void WarpMonster(GameObject go, Vector3 pos)
    {
        var mc = go.GetComponent<MonsterController>();
        if (mc != null) mc.WarpTo(pos);
        else go.transform.position = pos;
    }

    static void MoveMonster(GameObject go, Vector3 pos)
    {
        var mc = go.GetComponent<MonsterController>();
        if (mc != null)
        {
            mc.TargetPos = pos;
            mc.HasTarget = true;
        }
        else
        {
            var t = go.transform;
            t.position = new Vector3(pos.x, t.position.y, pos.z);
        }
    }

    // ================== 가장 가까운 몬스터 찾기 ==================
    public static ulong FindClosestMonster(Vector3 pos, float range)
    {
        ulong bestId = 0;
        float bestDistSq = range * range;

        foreach (var kv in monsters)
        {
            var go = kv.Value;
            if (go == null || !go.activeSelf)
                continue;

            float d = (go.transform.position - pos).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                bestId = kv.Key;
            }
        }

        return bestId;
    }

}
