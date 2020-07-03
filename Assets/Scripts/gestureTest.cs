using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Leap;

public class gestureTest : MonoBehaviour {

	GameObject canvas;
	string url;
	List<NameButton> buttonList = new List<NameButton>();

	Controller controller = new Controller();
	GestureListener gestureListener = new GestureListener();

	// Use this for initialization
	void Start () {
		controller.Connect += gestureListener.OnServiceConnect;
		controller.Device += gestureListener.OnConnect;
		controller.FrameReady += gestureListener.OnFrame;
		Debug.Log("Gesture Test begins.");

		Init();
	}

	void Init() {
		canvas = GameObject.Find("Canvas");
		url = canvas.GetComponent<canvasCreate>().url;
	}


	// Update is called once per frame
	void Update () {
		// Obtain buttonList in Update() instead of Init() 
		// because canvasCreate need some time to generate buttonList
		// initial buttonList in canvasCreate is empty
		if (buttonList.Count == 0) {
			buttonList = canvas.GetComponent<canvasCreate>().buttonList;
        } 
		// Only allow gesture commands after buttonList is generated and obtained
		else {
			GestureCommands(gestureListener.leftGesture, gestureListener.rightGesture);
        }

	}

	// TODO, for debug
	void GestureCommands (Gesture leftGesture, Gesture rightGesture) {
		// Grab (right hand prior to left)
		if (rightGesture.Type == Gesture.GestureType.Gesture_Grab) {
			rightGesture.Grab();
		}
		else if (leftGesture.Type == Gesture.GestureType.Gesture_Grab) {
			leftGesture.Grab();
		}

		// Point (right hand prior to left)
		if (rightGesture.Type == Gesture.GestureType.Gesture_Point) {
			rightGesture.Create(buttonList);
		}
		else if (leftGesture.Type == Gesture.GestureType.Gesture_Point) {
			leftGesture.Create(buttonList);
		}

		// Gun (right hand prior to left)
		if (rightGesture.Type == Gesture.GestureType.Gesture_Gun) {
			rightGesture.Select();
		}
		else if (leftGesture.Type == Gesture.GestureType.Gesture_Gun) {
			leftGesture.Select();
		}

		// Confirm (right hand prior to left)
		if (rightGesture.Type == Gesture.GestureType.Gesture_OK) {
			rightGesture.Confirm();
		}
		else if (leftGesture.Type == Gesture.GestureType.Gesture_OK) {
			leftGesture.Confirm();
		}

		// Stretch (both hands)
		if (rightGesture.Type == Gesture.GestureType.Gesture_Palm &&
			leftGesture.Type == Gesture.GestureType.Gesture_Palm) {
			rightGesture.Stretch(leftGesture.currHand, rightGesture.currHand);
		}
	}

}

public class GestureListener
{
	public Gesture leftGesture = new Gesture();
	public Gesture rightGesture = new Gesture();

	public void OnServiceConnect (object sender, ConnectionEventArgs args) {
		Debug.Log("Leapmotion Service Connected.");
    }

	public void OnConnect (object sender, DeviceEventArgs args) {
		Debug.Log("Leapmotion Controller Connected.");
		
		GestureInit();
    }

	public void GestureInit() {
		leftGesture.RegisterGestureParams();
		rightGesture.RegisterGestureParams();
    }

	public void OnFrame (object sender, FrameEventArgs args) {
		//Debug.Log("Leapmotin Frame Available");

		// Get the most recent frame and report some basic information
		Frame frame = args.frame;

		// Detect gestures
		if (frame.Hands.Count == 2) {
			List<Hand> hands = frame.Hands;

			foreach (Hand hand in hands) {
				if (hand.IsLeft) {
					leftGesture.Type = leftGesture.DetectGestureType(hand);
				} else {
					rightGesture.Type = rightGesture.DetectGestureType(hand);
				}
			}
        } else if (frame.Hands.Count == 1) {
			List<Hand> hands = frame.Hands;

			foreach (Hand hand in hands) {
				if (hand.IsLeft) {
					leftGesture.Type = leftGesture.DetectGestureType(hand);
					rightGesture.Type = Gesture.GestureType.Gesture_None;
				} else {
					rightGesture.Type = rightGesture.DetectGestureType(hand);
					leftGesture.Type = Gesture.GestureType.Gesture_None;
				}
			}
		} else {
			leftGesture.Type = Gesture.GestureType.Gesture_None;
			rightGesture.Type = Gesture.GestureType.Gesture_None;
        }

    }
}

// TODO: "Gesture" class in a separate script
public class Gesture {
	public enum GestureType {
		Gesture_Grab,
		Gesture_Palm,
		Gesture_Gun,
		Gesture_OK,
		Gesture_Point,
		Gesture_Thumbup,
		Gesture_None
	}

	// Current gesture type
	private GestureType gestureType = GestureType.Gesture_None;
	public GestureType Type {
		get { return gestureType; }
		set { gestureType = value; }
	}

	// Pre-defined Gesture Parameters {IsExtended (5 bool/null), PinchStrength (0-1), GrabStrength (0-1)}
	private List<Dictionary<string, ArrayList>> gesture_param_list = new List<Dictionary<string, ArrayList>>();
	public void RegisterGestureParams() { 
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
		Dictionary<string, ArrayList> gesture_thumbup_param = new Dictionary<string, ArrayList>() {
			{ "IsExtended", new ArrayList { true, false, false, false, false} }
		};

		gesture_param_list.Add(gesture_grab_param);
		gesture_param_list.Add(gesture_palm_param);
		gesture_param_list.Add(gesture_gun_param);
		gesture_param_list.Add(gesture_ok_param);
		gesture_param_list.Add(gesture_point_param);
		gesture_param_list.Add(gesture_thumbup_param);
	}

	// Current gesture param
	private Dictionary<string, ArrayList> gesture_param = new Dictionary<string, ArrayList>() {
		{ "IsExtended", new ArrayList {null, null, null, null, null} },
		{ "GrabStrength", new ArrayList { null} },
		{ "PinchStrength", new ArrayList { null} }
	};
	public Hand currHand = new Hand();

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

		currHand = hand;
	}

	// Get current gesture type from hand
	public GestureType DetectGestureType(Hand hand) {
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

	// Define gesture commands
	
	public void Grab() {
		Debug.Log("Begin grabbing...");
	}

	public void Create(List<NameButton> buttonList) {
		// Steps: 
		// 1. find index fingertip pos
		// 2. find button within range
		// 3. change within-range button color based on vertical dis
		// 4. send button name to CallCompiler 
		// 5. set flag to avoid callcompiler repeatedly (only create when button state turn from "hover" to "select")

		Vector3 fingerPos = new Vector3();
		foreach (Finger finger in currHand.Fingers) {
			if (finger.Type == Finger.FingerType.TYPE_INDEX) {
				fingerPos = new Vector3(finger.TipPosition.x, finger.TipPosition.y, finger.TipPosition.z) / 100f; // unit=cm in Leapmotion
			}
			break;
		}
		
		// Need debug: pos not right
		Debug.Log(fingerPos);
		//Debug.Log("withinRange:" + nameButton.WithinRange(fingerPos));
		//Debug.Log("verticalDis:" + nameButton.VerticalDis(fingerPos));

		// For debug
		//CallCompiler("");
	}

	// maybe call compiler in main's Update() ?
	public void CallCompiler(string name) {
		Debug.Log("Begin creating...");
    }

	public void Select() {
		Debug.Log("Begin selecting...");
    }

	public void Confirm() {
		Debug.Log("Confirmed.");
    }

	public void Stretch(Hand leftHand, Hand rightHand) {
		Debug.Log("Begin stretching...");
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
