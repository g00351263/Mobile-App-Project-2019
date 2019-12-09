using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Move2D : MonoBehaviour {
    public float moveSpeed = 5f;
    private Vector2 lookDirection;
    private float lookAngle;
	
	// Update is called once per frame
	void Update () {
        lookDirection = Camera.main.ScreenToWorldPoint(Input.mousePosition) - transform.position;
        lookAngle = Mathf.Atan2(lookDirection.y, lookDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, lookAngle - 20f);
        //Jump();
        //Vector3 movement = new Vector3(Input.GetAxis("Horizontal"), 0f, 0f);
       // transform.position += movement * Time.deltaTime * moveSpeed;
	}
    
    void Jump()
    {
        if (Input.GetKeyDown("space"))
        {
            gameObject.GetComponent<Rigidbody2D>().AddForce(new Vector2(0f, 5f), ForceMode2D.Impulse);
            }
        }
}
