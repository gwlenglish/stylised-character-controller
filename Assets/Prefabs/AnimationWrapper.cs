using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationWrapper : MonoBehaviour
{
    Animator animator;
    private void Awake()
    {
        animator = GetComponent<Animator>();
    }
    public void CrossFade(string name, float duration = .25f)
    {
        animator.CrossFadeInFixedTime(name, duration);
    }
    public void PlayAnimation(string name)
    {
        animator.Play(name);
    }
    // Start is called before the first frame update
  
}
