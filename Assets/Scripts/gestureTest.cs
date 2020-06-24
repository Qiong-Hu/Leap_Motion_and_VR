﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Leap;

public class gestureTest : MonoBehaviour {

	// Use this for initialization
	void Start () {

		Controller controller = new Controller();
		GestureListener listener = new GestureListener();

		controller.Connect += listener.OnServiceConnect;
		controller.Device += listener.OnConnect;
		controller.FrameReady += listener.OnFrame;

		Debug.Log("Gesture Test begins");

	}



	// Update is called once per frame
	void Update () {
		
	}
}



public class GestureListener
{
	public void OnServiceConnect (object sender, ConnectionEventArgs args) {
		Debug.Log("Leapmotion Service Connected");
    }

	public void OnConnect (object sender, DeviceEventArgs args) {
		Debug.Log("Leapmotion Controller Connected");
    }

	public void OnFrame (object sender, FrameEventArgs args) {
		//Debug.Log("Leapmotin Frame Available");

		// Get the most recent frame and report some basic information
		Frame frame = args.frame;

		if (frame.Hands.Count > 0) {
			List<Hand> hands = frame.Hands;
			Gesture leftGesture = new Gesture();
			Gesture rightGesture = new Gesture();

			foreach (Hand hand in hands) {
				if (hand.IsLeft) {
					leftGesture.Type = leftGesture.DetectGestureType(hand);
				}
				else {
					rightGesture.Type = rightGesture.DetectGestureType(hand);
				}
			}
			Debug.Log("left: " + leftGesture.Type + " right: " + rightGesture.Type);
        }

		
    }
}

// TODO: "Gesture" class in separate script
public class Gesture {
	public enum GestureType {
		Gesture_Grab,
		Gesture_Palm,
		Gesture_Gun,
		Gesture_OK,
		Gesture_Point,
		Gesture_None
	}

	private GestureType gestureType = GestureType.Gesture_None;
	public GestureType Type {
		get { return gestureType; }
		set { gestureType = value; }
	}

	// Defined Gesture Parameters {IsExtended (5), PinchStrength, GrabStrength}
	private List<Dictionary<string, ArrayList>> gesture_param_list = new List<Dictionary<string, ArrayList>>();
	private void RegisterGestureParams() { 
		Dictionary<string, ArrayList> gesture_grab_param = new Dictionary<string, ArrayList>() {
			{ "IsExtended", new ArrayList { false, false, false, false, false } },
			{ "GrabStrength", new ArrayList { 1f} }
		};
		Dictionary<string, ArrayList> gesture_palm_param = new Dictionary<string, ArrayList>() {
			{ "IsExtended", new ArrayList { true, true, true, true, true} },
			{ "GrabStrength", new ArrayList { 0f} }
		};
		Dictionary<string, ArrayList> gesture_gun_param = new Dictionary<string, ArrayList>() {
			{ "IsExtended", new ArrayList { true, true, false, false, false} }
		};
		Dictionary<string, ArrayList> gesture_ok_param = new Dictionary<string, ArrayList>() {
			{ "IsExtended", new ArrayList { null, null, true, true, true} },
			{ "PinchStrength", new ArrayList { 1f} }
		};
		Dictionary<string, ArrayList> gesture_point_param = new Dictionary<string, ArrayList>() {
			{ "IsExtended", new ArrayList { false, true, false, false, false} }
		};

		gesture_param_list.Add(gesture_grab_param);
		gesture_param_list.Add(gesture_palm_param);
		gesture_param_list.Add(gesture_gun_param);
		gesture_param_list.Add(gesture_ok_param);
		gesture_param_list.Add(gesture_point_param);
	}


	// Current gesture param
	private Dictionary<string, ArrayList> gesture_param = new Dictionary<string, ArrayList>() {
		{ "IsExtended", new ArrayList {null, null, null, null, null} },
		{ "GrabStrength", new ArrayList { null} },
		{ "PinchStrength", new ArrayList { null} }
	};

	private void GetGestureParams(Hand hand) {
		List<Finger> fingers = hand.Fingers;
		foreach (Finger finger in fingers) {
			if (finger.Type == Finger.FingerType.TYPE_THUMB) {
				gesture_param["IsExtended"][0] = finger.IsExtended;
			}
			if (finger.Type == Finger.FingerType.TYPE_INDEX) {
				gesture_param["IsExtended"][1] = finger.IsExtended;
			}
			if (finger.Type == Finger.FingerType.TYPE_MIDDLE) {
				gesture_param["IsExtended"][2] = finger.IsExtended;
			}
			if (finger.Type == Finger.FingerType.TYPE_RING) {
				gesture_param["IsExtended"][3] = finger.IsExtended;
			}
			if (finger.Type == Finger.FingerType.TYPE_PINKY) {
				gesture_param["IsExtended"][4] = finger.IsExtended;
			}
		}

		gesture_param["GrabStrength"][0] = hand.GrabStrength;
		gesture_param["PinchStrength"][0] = hand.PinchStrength;
	}

	public GestureType DetectGestureType(Hand hand) {
		RegisterGestureParams();
		GetGestureParams(hand);

		gestureType = GestureType.Gesture_None;
		for (int i = 0; i < gesture_param_list.Count; i++) {
			if (CompareDict(gesture_param, gesture_param_list[i])) {
				gestureType = (GestureType)i;
				break;
            }
        }

		return gestureType;
	}

	void Grab() {
		Debug.Log("Begin grabbing");
	}

	void Select() {

    }

	void Confirm() {

    }

	void Stretch() {

    }

	// list1 = current ArrayList, list2 = reference ArrayList
	private bool CompareArrayList(ArrayList list1, ArrayList list2) {
		if (list1.Count != list2.Count) {
			return false;
		}
		else {
			for (int i = 0; i < list1.Count; i++) {
				if (Object.Equals(list2[i], null)) {
					continue;
                }
				if (!Object.Equals(list1[i], list2[i])) {
					return false;
				}
			}
			return true;
		}
	}

	// dict1 = current gesture param dict, dict2 = reference gesture param dict
	private bool CompareDict(Dictionary<string, ArrayList> dict1, Dictionary<string, ArrayList> dict2) {
		bool flag = true;
		foreach(string key in dict2.Keys) {
			if (!CompareArrayList(dict1[key], dict2[key])) {
				flag = false;
				break;
            }
        }
		return flag;
    }
}
