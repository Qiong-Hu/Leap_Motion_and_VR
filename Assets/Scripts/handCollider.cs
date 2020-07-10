using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class handCollider : MonoBehaviour {

	string colliderName = "";

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public string ColliderName {
		get { return colliderName; }
		set { colliderName = value; }
    }

	void OnCollisionEnter(Collision collision) {
		//Debug.Log(this.name + " collides " + collision.gameObject.name);
		colliderName = collision.gameObject.name;
    }

	void OnCollisionExit(Collision collision) {
		colliderName = "";
    }
}
