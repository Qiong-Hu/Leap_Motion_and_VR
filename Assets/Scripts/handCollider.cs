using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class handCollider : MonoBehaviour {

	string colliderName = "";
	Vector3 contactPosition = new Vector3();

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

	public Vector3 ContactPosition {
		get { return contactPosition; }
		set { contactPosition = value; }
    }

	void OnCollisionEnter(Collision collision) {
		//Debug.Log(this.name + " collides " + collision.gameObject.name);
		colliderName = collision.gameObject.name;
		contactPosition = collision.contacts[0].point;
    }

	void OnCollisionExit(Collision collision) {
		colliderName = "";
		contactPosition = new Vector3();
    }
}
