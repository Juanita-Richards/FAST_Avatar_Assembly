using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Blinking : MonoBehaviour
{
    //get reference to animator
    public Animator animator;


    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        StartCoroutine(BlinkWait(Random.Range(3,10)));
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public IEnumerator BlinkWait(float delay) 
    {

        yield return new WaitForSeconds(delay);
        animator.SetBool("blink", true); //sets the blink parameter true
        yield return new WaitForSeconds(1); //wait before we set it false
        animator.SetBool("blink", false);
        StartCoroutine(BlinkWait(Random.Range(3, 10)));
    }
}
