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
    public static void ApplyCombatEvent(CombatEvent ev)
    {
        ulong targetId = ev.TargetId;
        bool targetIsMonster = (ev.TargetType == EntityType.Monster);

        int damage = ev.Damage;
        int remainHp = ev.RemainHp;

        GameObject targetGo = null;
        if (targetIsMonster)
            monsters.TryGetValue(targetId, out targetGo);
        else
            players.TryGetValue(targetId, out targetGo);

        if (targetGo == null)
        {
            Debug.LogWarning($"[CombatEvent] target not found id={targetId} monster={targetIsMonster}");
            return;
        }

        //// HP바 갱신 (네 스크립트 이름에 맞게 바꿔)
        //var hpBar = targetGo.GetComponentInChildren<HpBarUI>();
        //if (hpBar != null)
        //    hpBar.SetHp(remainHp);

        //// 피격 이펙트
        //var hitFx = targetGo.GetComponent<HitEffect>();
        //if (hitFx != null)
        //    hitFx.Play();

        //DamageText.Spawn(
        //    damage,
        //    targetGo.transform.position + Vector3.up * 2f
        //);

        if (remainHp <= 0)
        {
            var anim = targetGo.GetComponentInChildren<Animator>();
            if (anim != null)
                anim.SetTrigger("Die");
        }
    }
    public static void ApplyAiState(AiStateEvent ev)
    { 
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
        ns.SetServerPosition(pos);

        // ✅여기부터 추가
        if (id == MyPlayerId)
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
            inst.SetActive(false); // 비활성화
            inst.tag = "Untagged";
        }
        // ✅ 여기까지

        players[id] = inst;
    }


    // ================== Move ==================
    static void OnMove(ulong id, Vector3 pos, bool isMonster)
    {
        if (isMonster)
        {
            // 🔥 AOI 밖이거나 꺼진 몬스터는 절대 이동 반영 X
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
