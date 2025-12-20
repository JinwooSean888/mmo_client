using UnityEngine;

public class MonsterAnimController : MonoBehaviour
{
    Animator _anim;

    int _hashSpeed;
    int _hashAttackBool;
    int _hashDead;

    void Awake()
    {
        _anim = GetComponentInChildren<Animator>();

        _hashSpeed = Animator.StringToHash("Speed");
        _hashAttackBool = Animator.StringToHash("IsAttacking");
        _hashDead = Animator.StringToHash("Dead");
    }

    public void ApplyAIState(MonsterAIState st)
    {
        if (_anim == null) return;

        switch (st)
        {
            case MonsterAIState.Attack:
                _anim.SetFloat(_hashSpeed, 0f);
                _anim.SetBool(_hashAttackBool, true);   // 공격 ON
                break;

            case MonsterAIState.Idle:
                _anim.SetFloat(_hashSpeed, 0f);
                _anim.SetBool(_hashAttackBool, false);  // 반드시 OFF
                break;

            case MonsterAIState.Patrol:
            case MonsterAIState.Move:
            
                _anim.SetFloat(_hashSpeed, 1f);
                _anim.SetBool(_hashAttackBool, false);  // 여기서도 항상 OFF
                break;

            case MonsterAIState.Return:
                _anim.SetFloat(_hashSpeed, 1f);
                _anim.SetBool(_hashAttackBool, false);  // 여기서도 항상 OFF
                break;

            case MonsterAIState.Dead:
                _anim.SetBool(_hashAttackBool, false);
                _anim.SetTrigger(_hashDead);
                break;
        }
    }

}

