using UnityEngine;

public class MonsterAnimController : MonoBehaviour
{
    private Animator _anim;

    int _hashSpeed;
    int _hashAttackBool;
    int _hashAttackTrigger;
    int _hashDead;

    MonsterAIState _lastState = (MonsterAIState)(-1);

    void Awake()
    {
        _anim = GetComponentInChildren<Animator>();

        _hashSpeed = Animator.StringToHash("MonSpeed");     // float
        _hashAttackBool = Animator.StringToHash("IsAttacking");  // bool
        _hashAttackTrigger = Animator.StringToHash("DoAttack");     // trigger (Animator에 추가 필요)
        _hashDead = Animator.StringToHash("Dead");         // trigger

    }

    public void ApplyAIState(MonsterAIState st)
    {
        if (_anim == null)
            return;

        // ★ Attack은 매번 재생해야 하므로 예외로 두고,
        //    그 외 상태는 동일 상태면 스킵
        if (st == _lastState && st != MonsterAIState.Attack)
            return;

        // -------------------------
        // 1) 이동 여부 -> MonSpeed
        // -------------------------
        bool isMove =
            st == MonsterAIState.Move ||
            st == MonsterAIState.Patrol ||
            st == MonsterAIState.Return ||
            st == MonsterAIState.Chase;

        float speed = isMove ? 1f : 0f;
        _anim.SetFloat(_hashSpeed, speed);

        // -------------------------
        // 2) 공격 상태 처리
        // -------------------------
        if (st == MonsterAIState.Attack)
        {
            // 공격 중 상태 플래그
            _anim.SetBool(_hashAttackBool, true);

            // Trigger 한 번 톡 건드려서 항상 다시 재생되게
            _anim.ResetTrigger(_hashAttackTrigger);
            _anim.SetTrigger(_hashAttackTrigger);
        }
        else
        {
            // 공격 상태가 아닐 때는 플래그 내림
            _anim.SetBool(_hashAttackBool, false);
        }

        // -------------------------
        // 3) 죽음
        // -------------------------
        if (st == MonsterAIState.Dead)
        {
            _anim.SetTrigger(_hashDead);
        }

        _lastState = st;
    }

    public void ResetAnimState()
    {
        _lastState = (MonsterAIState)(-1);
        if (_anim == null) return;

        _anim.Rebind();
        _anim.Update(0f);

        _anim.SetFloat(_hashSpeed, 0f);
        _anim.SetBool(_hashAttackBool, false);
    }

}
