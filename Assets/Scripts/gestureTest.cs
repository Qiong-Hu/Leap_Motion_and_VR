// This script is attached to Leap Rig
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Leap;
using FARVR.Design;

public class gestureTest : MonoBehaviour {

	// Obtain canvas obj
	GameObject canvas;
	string url;
	List<NameButton> buttonList = new List<NameButton>();
	List<string> nameList = new List<string>();

	// Button trigger params
	float hoverThreshold;
	float touchThreshold;

	// Obtain Leapmotion controller and add listener
	Controller controller = new Controller();
	GestureListener gestureListener = new GestureListener();

    #region params used during gesture commands
	// For CreateObj
    string creationName = "";
	string creationNamePrev = "";

	// For GrabObj
	Dictionary<string, dynamic> grabParams = null;
	Dictionary<string, dynamic> grabParamsPrev = null;
	GameObject grabObj = null;
	GameObject contactPoint = null;
	#endregion

	// Define design obj list
	public GameObject designObjPrefab;
	List<DesignObj> designList = new List<DesignObj>();
	private int designCounter = 0;

	// Use this for initialization
	void Start () {
		controller.Connect += gestureListener.OnServiceConnect;
		controller.Device += gestureListener.OnConnect;
		controller.FrameReady += gestureListener.OnFrame;
		Debug.Log("Gesture detection begins.");

		Init();
	}

	void Init() {
		// Obtain canvas obj and params defined in canvasCreate script
		canvas = GameObject.Find("Canvas");
		url = canvas.GetComponent<canvasCreate>().url;
		hoverThreshold = canvas.GetComponent<canvasCreate>().hoverThreshold;
		touchThreshold = canvas.GetComponent<canvasCreate>().touchThreshold;
	}


	// Update is called once per frame
	void Update () {
		// Obtain buttonList in Update() instead of Init() 
		// because canvasCreate need some time to generate buttonList
		// initial buttonList in canvasCreate is empty
		if (buttonList.Count == 0) {
			buttonList = canvas.GetComponent<canvasCreate>().buttonList;
			nameList = canvas.GetComponent<canvasCreate>().nameList;
        } 
		// Only allow gesture commands after buttonList is generated and obtained
		else {
			if (gestureListener.leftGesture.Type != Gesture.GestureType.Gesture_None ||
				gestureListener.rightGesture.Type != Gesture.GestureType.Gesture_None) {
				GestureCommands(gestureListener.leftGesture, gestureListener.rightGesture);

				// Act on GestureCommands
				CreateObj();
				GrabObj();

            }
		}

	}

	void GestureCommands (Gesture leftGesture, Gesture rightGesture) {
		// Grab (right hand prior to left)
		if (rightGesture.Type == Gesture.GestureType.Gesture_Grab) {
			grabParams = rightGesture.Grab();
		}
		else if (leftGesture.Type == Gesture.GestureType.Gesture_Grab) {
			grabParams = leftGesture.Grab();
		}
		else {
			GrabReset();
			grabParams = null;
		}

		// Point (right hand prior to left)
		if (rightGesture.Type == Gesture.GestureType.Gesture_Point) {
			creationName = rightGesture.Create(buttonList, hoverThreshold, touchThreshold);
		}
		else if (leftGesture.Type == Gesture.GestureType.Gesture_Point) {
			creationName = leftGesture.Create(buttonList, hoverThreshold, touchThreshold);
		} else {
			CreateReset();
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

    #region Generate new design object
    void CreateObj() {
		if (creationName != "") {
			// if prev==sth+"-hover" and curr==sth, then create sth
			if (creationNamePrev.Equals(creationName + "-hover")) {
				if (creationName == "Export") {
					// TODO: selectedObj.Export()
					Debug.Log(creationName);
                } else if (creationName == "Delete") {
					// TODO: selectedObj.RemoveDesign()
					Debug.Log(creationName);
				} else if (creationName == "Exit") {
					// TODO: auto save all stl; exit program
					Debug.Log(creationName);
                } else {
					// really actually create here
					Debug.Log("Begin creating " + creationName + "...");
					CallCompiler(creationName, designCounter++);
                }
			}
		}
		creationNamePrev = creationName;
	}

	// Call compiler, retrieve stl of the design obj, add to designList
	void CallCompiler(string type, int id) {
		GameObject gameobj;
		gameobj = Instantiate(designObjPrefab) as GameObject;
		DesignObj designObj = gameobj.GetComponent<DesignObj>();
		designObj.RegisterNameList(nameList);
		designObj.MakeDesign(url, type, id, new Vector3(0, 10, 0), Vector3.one * 10);
		
		designList.Add(designObj);
		Debug.Log(designObj.GetFType() + " is created.");
	}

	void CreateReset() {
		// Reset buttonlist color
		foreach (NameButton nameButton in buttonList) {
			nameButton.ChangeColor("normal");
		}
		creationName = "";
	}
	#endregion

	#region Grab design object
	// TODO (bug info 1): if hand disappear during grab, obj new init pos/rot = obj pos/rot when gesture reappear (init pos shouldn't change)
	// TODO (bug info 2): if after grab and move, obj collider are below ground, when grab release, obj will be blown away (should detect collision before grab release)
	void GrabObj() {
		// Begin grabbing
		if (grabParams != null && grabParamsPrev == null && grabObj == null && grabParams["colliderName"] != "") {
			GrabInit();
		}
		// Update grabbing
		else if (grabParams != null && grabParamsPrev != null && grabObj != null){
			GrabUpdate();
        }
		// End grabbing
		else if (grabParams == null && grabParamsPrev != null && grabObj != null) {
			GrabEnd();
			GrabReset();
		}
		// Reset
		else {
			GrabReset();
        }
		
		grabParamsPrev = grabParams;
    }

	void GrabInit() {
		grabObj = GameObject.Find(grabParams["colliderName"]);

		contactPoint = new GameObject("Contact Point");
		contactPoint.transform.position = grabParams["handPosition"];
		contactPoint.transform.eulerAngles = grabParams["handRotation"];

		// Record obj's ending pos/rot after grab and move
		GameObject grabObjRepre = new GameObject("Grab obj represent");
		grabObjRepre.transform.position = grabObj.transform.position;
		grabObjRepre.transform.rotation = grabObj.transform.rotation;
		grabObjRepre.transform.SetParent(contactPoint.transform);

		try {
			GameObject.Find("L_Palm/palm").GetComponent<Collider>().enabled = false;
		}
		catch { }
		try {
			GameObject.Find("R_Palm/palm").GetComponent<Collider>().enabled = false;
		}
		catch { }

	}

	void GrabUpdate() {
		contactPoint.transform.position = grabParams["handPosition"];
		contactPoint.transform.eulerAngles = grabParams["handRotation"];
		
		grabObj.transform.position = contactPoint.transform.GetChild(0).position;
		grabObj.transform.eulerAngles = contactPoint.transform.GetChild(0).eulerAngles;

	}

	void GrabEnd() {
		contactPoint.transform.position = grabParamsPrev["handPosition"];
		contactPoint.transform.eulerAngles = grabParamsPrev["handRotation"];

		grabObj.transform.position = contactPoint.transform.GetChild(0).position;
		grabObj.transform.eulerAngles = contactPoint.transform.GetChild(0).eulerAngles;
	}

	void GrabReset() {
		if (grabObj != null) {
			grabObj = null;
       }
		if (contactPoint != null) {
			Destroy(contactPoint.transform.GetChild(0).gameObject);
			Destroy(contactPoint);
			contactPoint = null;
        }

		try {
			GameObject.Find("L_Palm/palm").GetComponent<Collider>().enabled = true;
		}
		catch { }
		try {
			GameObject.Find("R_Palm/palm").GetComponent<Collider>().enabled = true;
		}
		catch { }

    }
	#endregion

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

		leftGesture.Type = Gesture.GestureType.Gesture_None;
		rightGesture.Type = Gesture.GestureType.Gesture_None;
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

	// Pre-defined Gesture Parameters: {IsExtended (5 bool/null), PinchStrength (0-1), GrabStrength (0-1)}
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

	/// <summary>
    /// Get current gesture type from hand
    /// </summary>
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

    #region Define gesture commands

    public Dictionary<string, dynamic> Grab() {
		Dictionary<string, dynamic> grabParams = new Dictionary<string, dynamic>();
		if (currHand.IsLeft) {
			try {
				grabParams["handPosition"] = GameObject.Find("L_Palm").transform.position;
				grabParams["handRotation"] = GameObject.Find("L_Palm").transform.eulerAngles;
			} catch {
				Debug.Log("Fail to find left palm");
				return null;
            }
			try {
				// Only detect collision between palm and object for now (finger colliders exist, unused)
				GameObject palmCollider = GameObject.Find("L_Palm/palm");
				string colliderName = palmCollider.GetComponent<handCollisionManagement>().ColliderName;
				grabParams["colliderName"] = colliderName;
				grabParams["contactPosition"] = palmCollider.GetComponent<handCollisionManagement>().ContactPosition;
			} catch {
				Debug.Log("Fail to find left palm collider");
				return null;
            }
        } else {
			try {
				grabParams["handPosition"] = GameObject.Find("R_Palm").transform.position;
				grabParams["handRotation"] = GameObject.Find("R_Palm").transform.eulerAngles;
			} catch {
				Debug.Log("Fail to find right palm");
				return null;
            }
			try {
				// Only detect collision between palm and object for now (finger colliders exist, unused)
				GameObject palmCollider = GameObject.Find("R_Palm/palm");
				string colliderName = palmCollider.GetComponent<handCollisionManagement>().ColliderName;
				grabParams["colliderName"] = colliderName;
				grabParams["contactPosition"] = palmCollider.GetComponent<handCollisionManagement>().ContactPosition;
			} catch {
				Debug.Log("Fail to find right palm collider");
				return null;
            }
        }

		return grabParams;
	}

	public string Create(List<NameButton> buttonList, float hoverThreshold, float touchThreshold) {
		// Steps: 
		// 1. find index fingertip pos
		// 2. find button within range
		// 3. change within-range button color based on vertical dis
		// 4. send button name to CallCompiler 
		// 5. set flag to avoid callcompiler repeatedly (only create when button state turn from "hover" to "select")
		// 6. after created, reset all button color and flag

		// Step 1. Find index fingertip pos
		// Leapmotion's inbuilt tipPosition returns wrong pos
		Vector3 fingertipPos = new Vector3();
		if (currHand.IsLeft) {
			try {
				fingertipPos = GameObject.Find("L_index_end").transform.position;
            } catch {
				Debug.Log("Fail to find left fingertip position.");
				return null;
            }
        } else {
			try {
				fingertipPos = GameObject.Find("R_index_end").transform.position;
			}
			catch {
				Debug.Log("Fail to find right fingertip position.");
				return null;
			}
		}

		// Step 2. Find button within range
		string creationName = "";
		foreach (NameButton currButton in buttonList) {
			if (currButton.WithinRange(fingertipPos)) {

				// Step 3. Change within-range button color based on vertical dis
				// later TODO: after selected when finger raise, don't show "hover" color (use flag to control?)
				if (currButton.VerticalDis(fingertipPos) <= hoverThreshold &&
					currButton.VerticalDis(fingertipPos) > touchThreshold) {
					currButton.ChangeColor("hover");
					creationName = currButton.name + "-hover";
				}
				else if (currButton.VerticalDis(fingertipPos) <= touchThreshold &&
					currButton.VerticalDis(fingertipPos) >= -touchThreshold) {
					currButton.ChangeColor("select");
					creationName = currButton.name;
				}
				else {
					currButton.ChangeColor("normal");
				}
            } else {
				currButton.ChangeColor("normal");
            }
        }

		// Step 4. send button name to CallCompiler (in Update)
		return creationName;
	}

	public void Select() {
		//Debug.Log("Begin selecting...");
    }

	public void Confirm() {
		//Debug.Log("Confirmed.");
    }

	public void Stretch(Hand leftHand, Hand rightHand) {
		//Debug.Log("Begin stretching...");
    }

    #endregion

	/// <summary>
    /// Compare curr ArrayList to ref ArrayList
    /// </summary>
    /// <param name="list1"> current ArrayList </param>
    /// <param name="list2"> reference ArrayList </param>
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

	/// <summary>
    /// Compare curr Dict to ref Dict
    /// </summary>
    /// <param name="dict1"> current gesture param dict </param>
    /// <param name="dict2"> reference gesture param dict </param>
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
