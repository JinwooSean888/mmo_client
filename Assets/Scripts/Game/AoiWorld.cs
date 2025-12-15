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
    const string DefaultMonsterPrefab = "SingleTwoHandSwordTemplate";

    // ================== 템플릿 ==================
    static GameObject GetPlayerTemplate()
    {
        if (_playerTemplate == null)
            _playerTemplate = Resources.Load<GameObject>("Player/PaladinTemplate");
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

    // ================== Enter ==================
    static void OnEnter(ulong id, Vector3 pos, bool isMonster, string prefabName)
    {
        if (isMonster)
        {
            if (!string.IsNullOrEmpty(prefabName))
                monsterPrefabById[id] = prefabName;

            // 재사용: 있고, 살아있으면 켜서 워프
            if (monsters.TryGetValue(id, out var m) && m != null)
            {
                m.SetActive(true);
                WarpMonster(m, pendingMonsterPos.TryGetValue(id, out var p) ? p : pos);
                pendingMonsterPos.Remove(id);
                return;
            }
            Debug.Log($"[AOI][NEW MONSTER] id={id} prefab={prefabName} " +$"hasKey={monsters.ContainsKey(id)}");
            // key는 있는데 값이 null(=Destroy된 상태)일 수 있으니 정리
            monsters.Remove(id);

            var prefab = GetMonsterPrefab(prefabName);
            if (prefab == null) return;

            var go = Object.Instantiate(prefab);
            go.name = $"Monster_{id}";
            monsters[id] = go;

            WarpMonster(go, pendingMonsterPos.TryGetValue(id, out var pp) ? pp : pos);
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

        // ✅ 여기부터 추가
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
            if (!monsters.TryGetValue(id, out var m))
            {
                // Enter 전에 온 Move → 좌표만 저장
                pendingMonsterPos[id] = pos;
                return;
            }

            MoveMonster(m, pos);
            return;
        }

        if (players.TryGetValue(id, out var p))
            p.GetComponent<NetworkSmooth>()?.SetServerPosition(pos);
    }

    // ================== Leave ==================
    static void OnLeave(ulong id, bool isMonster)
    {
        if (isMonster)
        {
            if (monsters.TryGetValue(id, out var m) && m != null)
            {
                m.SetActive(false);
            }
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
