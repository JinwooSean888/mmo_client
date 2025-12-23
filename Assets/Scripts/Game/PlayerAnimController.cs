using UnityEngine;
using field;

public class PlayerAnimController : MonoBehaviour
{
    Animator _anim;
    int _hashSpeed;
    int _hashAttack;
    int _hashDead;

    void Awake()
    {
        _anim = GetComponentInChildren<Animator>();
        _hashSpeed = Animator.StringToHash("Speed");
        _hashAttack = Animator.StringToHash("Attack");
        _hashDead = Animator.StringToHash("Dead");
    }

    public void ApplyNetworkState(AiStateType st)
    {
        if (_anim == null) return;

        switch (st)
        {
            case AiStateType.Idle:
                _anim.SetFloat(_hashSpeed, 0f);
                _anim.SetBool(_hashAttack, false);
                break;

            case AiStateType.Patrol:
            case AiStateType.Chase:
            case AiStateType.Return:
                _anim.SetFloat(_hashSpeed, 1f);
                _anim.SetBool(_hashAttack, false);
                break;

            case AiStateType.Attack:
                _anim.SetFloat(_hashSpeed, 0f);

                _anim.ResetTrigger("Combo01"); 
                _anim.SetTrigger("Combo01");
                break;

            case AiStateType.Dead:
                _anim.SetBool(_hashAttack, false);
                _anim.SetTrigger(_hashDead);
                break;
        }
    }
}
