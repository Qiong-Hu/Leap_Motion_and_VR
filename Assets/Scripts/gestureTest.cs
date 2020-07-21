// This script is attached to Leap Rig
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows;
using Leap;
using FARVR.Design;

public class gestureTest : MonoBehaviour {

    #region For canvas
    // Obtain canvas obj
    GameObject canvas;
	string url;
	List<NameButton> buttonList = new List<NameButton>();
	List<string> nameList = new List<string>();

	// Button trigger params
	float hoverThreshold;
	float touchThreshold;
    #endregion

    // Obtain Leapmotion controller and add listener
    Controller controller = new Controller();
	GestureListener gestureListener = new GestureListener();

	// Define design obj list
	public GameObject designObjPrefab;
	List<DesignObj> designList = new List<DesignObj>();
	private int designCounter = 0;

	#region Variables used during gesture commands
	// For CreateObj
	string selectedButtonName = "";
	string selectedButtonNamePrev = "";
	Vector3 originalPos = new Vector3(0, 10, -0.1f);
	Vector3 originalScale = Vector3.one * 10f;

	// For ExportObj
	string exportPath = "";

	// For GrabObj
	Dictionary<string, dynamic> grabParams = null;
	Dictionary<string, dynamic> grabParamsPrev = null;
	GameObject grabObj = null;
	GameObject contactPoint = null;

	// For SelectObj
	Dictionary<string, dynamic> selectParams = null;
	Dictionary<string, dynamic> selectParamsPrev = null;
	GameObject selectObj = null;
	bool isSelected = false;

	// For DrawRay when try to select
	public GameObject rayPrefab;
	Ray ray;
	GameObject lineObject = null;
	LineRenderer lineRenderer = null;

	// For highlighting obj
	Color originalColor;
	Shader originalShader;
	Color highlightColor = new Color32(255, 0, 255, 255);
	public Shader highlightShader;

	// For StretchObj
	float palmToPalmThreshold = 160f; // Degree
	float palmDis = 0;
	float palmDisRef = 330f; // in mm, default value for natural palm dis as reference
	#endregion

	#region Customized Gesture Determination
	[Header("Customized Gesture Determination")]
	public Gesture.GestureType createGesture = Gesture.GestureType.Gesture_Point;
	public Gesture.GestureType grabGesture = Gesture.GestureType.Gesture_Fist;
	public Gesture.GestureType selectGesture = Gesture.GestureType.Gesture_Gun;
	public Gesture.GestureType confirmGesture = Gesture.GestureType.Gesture_OK;
	public Gesture.GestureType stretchGestureLeft = Gesture.GestureType.Gesture_Palm;
	public Gesture.GestureType stretchGestureRight = Gesture.GestureType.Gesture_Palm;
	#endregion
	
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

		// Load default prefabs if not assigned
		if (designObjPrefab == null) {
			try {
				designObjPrefab = Resources.Load<GameObject>("Prefabs/DesignObj");
            }
			catch {
				Debug.Log("Fail to find Design Object Prefab.");
			}
		}
		if (rayPrefab == null) {
			try {
				rayPrefab = Resources.Load<GameObject>("Prefabs/RayPrefab");
            }
			catch {
				Debug.Log("Fail to find Ray Prefab.");
			}
		}

		// Load default highlight shader
		if (highlightShader == null) {
			try {
				highlightShader = Shader.Find("Materials/Shaders/HighlightShader");
            }
			catch {
				Debug.Log("Fail to find highlight shader.");
            }
        }

		// Init export filefolder name
		exportPath = Application.persistentDataPath + "/Export_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
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
			// Make changes only when at least one hand is in the scene
			if (gestureListener.leftGesture.Type != Gesture.GestureType.Gesture_None ||
				gestureListener.rightGesture.Type != Gesture.GestureType.Gesture_None) {
				GestureCommands(gestureListener.leftGesture, gestureListener.rightGesture);

				// Act on GestureCommands
				ButtonFunctions(); // = create + export + delete + exit
				GrabObj();
				SelectObj();
				
				if (selectObj != null && isSelected == true) {
					StretchWholeObj(selectObj);
				}
            }
		}
	}

	void GestureCommands (Gesture leftGesture, Gesture rightGesture) {
		// Create (right hand prior to left)
		// TODO (bug info): if both hands == createGesture and try to use left hand to create, will fail (only detect right hand in this situation)
		if (rightGesture.Type == createGesture) {
			selectedButtonName = rightGesture.Create(buttonList, hoverThreshold, touchThreshold);
		}
		else if (leftGesture.Type == createGesture) {
			selectedButtonName = leftGesture.Create(buttonList, hoverThreshold, touchThreshold);
		}
		else {
			CreateReset();
        }

		// Grab (right hand prior to left)
		if (rightGesture.Type == grabGesture) {
			grabParams = rightGesture.Grab();
		} 
		else if (leftGesture.Type == grabGesture) {
			grabParams = leftGesture.Grab();
		}
		else {
			GrabReset();
		}

		// Select (right hand prior to left)
		if (rightGesture.Type == selectGesture) {
			selectParams = rightGesture.Select();
		}
		else if (leftGesture.Type == selectGesture) {
			selectParams = leftGesture.Select();
		}
		else {
			SelectReset();
        }

		// Confirm (right hand prior to left)
		if (rightGesture.Type == confirmGesture) {
			isSelected = false;
		}
		else if (leftGesture.Type == confirmGesture) {
			isSelected = false;
		}

		// Stretch (both hands)
		if (rightGesture.Type == stretchGestureRight && leftGesture.Type == stretchGestureLeft) {
			palmDis = rightGesture.Stretch(leftGesture.currHand, rightGesture.currHand, palmToPalmThreshold);
		}
		else {
			palmDis = 0;
        }
	}

	#region Functional buttons
	// Button functions include: create, export, delete, exit system (TODO: connect to photon server)
	void ButtonFunctions() {
		if (selectedButtonName != "") {
			// if prev==sth+"-hover" and curr==sth, then create sth
			if (selectedButtonNamePrev.Equals(selectedButtonName + "-hover")) {
				// Reset selected obj to default params, pos, rot, scale
				if (selectedButtonName == "Reset") {
					if (selectObj != null && isSelected == true) {
						ExportObj(selectObj, selectObj.name + System.DateTime.Now.ToString("_yyyy-MM-dd_HH-mm-ss") + "(auto_save_at_reset)");
						ResetObj(selectObj);
					} else {
						Debug.Log("No object is selected for resetting.");
                    }
				}
				// Export stl file, and save parameters to a text file
				else if (selectedButtonName == "Export") {
					if (selectObj != null && isSelected == true) {
						ExportObj(selectObj);
                    } else {
						Debug.Log("No object is selected for exporting.");
                    }
				}
				// Auto save selected obj before deleting
				else if (selectedButtonName == "Delete") {
					if (selectObj != null && isSelected == true) {
						ExportObj(selectObj, selectObj.name + "(auto_save_at_delete)"); // wouldn't repeat itself so no need to add time string
						DeleteObj(selectObj);

						selectParams = null;
						isSelected = false;
						selectObj = null;
					} else {
						Debug.Log("No object is selected for deleting.");
                    }
				}
				// Auto save all objs in scene and exit program
				else if (selectedButtonName == "Exit") {
					Debug.Log("Auto save all objects and exit.");

					foreach (DesignObj designObj in designList) {
						ExportObj(designObj.GetGameobject(), designObj.GetName() + "(auto_save_at_exit)"); // wouldn't repeat itself so no need to add time string
					}

					ExitSys();
				}
				// Actually create new objects here
				else {
					Debug.Log("Begin creating " + selectedButtonName + "...");
					CreateObj(selectedButtonName, designCounter++);

					FindParams(selectedButtonName); // For debug
                }
			}
		}
		selectedButtonNamePrev = selectedButtonName;
	}

	// For debug, test it here, implement it in DesignObj
	// read from url's type.json, save as dictionary
	// in order to remove "FurnitureCatalog"
	void FindParams(string type) {

    }

	// Call compiler, retrieve stl of the design obj, add to designList
	void CreateObj(string type, int id) {
		GameObject gameobj;
		gameobj = Instantiate(designObjPrefab) as GameObject;
		DesignObj designObj = gameobj.GetComponent<DesignObj>();
		designObj.RegisterNameList(nameList);
		designObj.MakeDesign(url, type, id, originalPos, originalScale);
		
		designList.Add(designObj);
		Debug.Log(designObj.GetName() + " is created.");
	}

	void CreateReset() {
		// Reset buttonlist color
		foreach (NameButton nameButton in buttonList) {
			nameButton.ChangeColor("normal");
		}
		selectedButtonName = "";
	}

	void ExportObj(GameObject gameObject) {
		if (exportPath != "") {
			if (!Directory.Exists(exportPath)) {
				Directory.CreateDirectory(exportPath);
            }
			
			gameObject.GetComponent<DesignObj>().Export(exportPath);
		}
		else {
			Debug.Log("Fail to find export filefolder path.");
        }
    }

	// Reload ExportObj to distinguish whether an export is an auto save
	void ExportObj(GameObject gameObject, string filename) {
		if (exportPath != "") {
			if (!Directory.Exists(exportPath)) {
				Directory.CreateDirectory(exportPath);
			}

			gameObject.GetComponent<DesignObj>().Export(exportPath, filename);
		}
		else {
			Debug.Log("Fail to find export filefolder path.");
		}
	}

	void ResetObj(GameObject gameObject) {
		gameObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;

		gameObject.transform.position = originalPos;
		gameObject.transform.rotation = Quaternion.identity;
		gameObject.transform.localScale = originalScale;

		// TODO: Reset all params to default values from compiler's xxx.json

		gameObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;

		// De-select
		isSelected = false;
		SelectReset();

		Debug.Log(gameObject.name + " is reset.");
    }

	void DeleteObj(GameObject gameObject) {
		string objname = gameObject.name;
		try {
			designList.Remove(gameObject.GetComponent<DesignObj>());
			gameObject.GetComponent<DesignObj>().RemoveDesign();

			Debug.Log(objname + " is deleted.");
        }
		catch {
			Debug.Log("Fail to delete" + objname + ".");
        } 
	}

	void ExitSys() {
		#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
		#else
			Application.Quit();
		#endif
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
		grabParams = null;

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

	#region Select a design object to modify
	void SelectObj() {
		// Steps:
		// 1. if gesture = selectGesture, then draw ray, else not draw ray
		// 2. if ray hit obj, then highlight the obj, change ray color
		// 3. return the highlighted selected obj (pass it through global variable)
		// 4. if gesture = confirmGesture, then de-highlight obj, selectObj = null

		GameObject currObj = null;

		// Update ray based on gesture
		if (selectParams != null && selectParamsPrev == null) {
			DrawRayInit();
			currObj = DrawRayUpdate(selectParams["fingerbasePos"], selectParams["fingertipPos"]);
		} 
		else if (selectParams != null && selectParamsPrev != null) {
			currObj = DrawRayUpdate(selectParams["fingerbasePos"], selectParams["fingertipPos"]);
		}
		else {
			DrawRayReset();
        }

		// Update select info based on hit info
		if (currObj != null && selectObj == null) {
			selectObj = currObj;
			isSelected = true;
			HighlightObj(selectObj);
        }
		else if (currObj != null && selectObj != currObj) {
			DeHighlightObj(selectObj);

			selectObj = currObj;
			isSelected = true;
			HighlightObj(selectObj);
        }

		selectParamsPrev = selectParams;
    }
	
	void DrawRayInit() {
		float lineWidth = 0.02f;

		lineObject = Instantiate(rayPrefab) as GameObject;
		lineRenderer = lineObject.GetComponent<LineRenderer>();
		lineRenderer.enabled = true;

		lineRenderer.startWidth = lineWidth;
		lineRenderer.endWidth = lineWidth;
	}

    GameObject DrawRayUpdate(Vector3 originPos, Vector3 endPos) {
		float farEndDistance = 100f;
		
		ray = new Ray(endPos, endPos - originPos);
		RaycastHit hitInfo;

		if (Physics.Raycast(ray, out hitInfo, Mathf.Infinity) && hitInfo.transform.gameObject.tag == "Design") {
			lineRenderer.SetPositions(new Vector3[] { originPos, hitInfo.point });
			lineRenderer.material = Resources.Load<Material>("Materials/SimpleColors/LightGreen");
			return hitInfo.transform.gameObject;
		}
		else if (Physics.Raycast(ray, out hitInfo, Mathf.Infinity)) {
			lineRenderer.SetPositions(new Vector3[] { originPos, hitInfo.point });
			lineRenderer.material = Resources.Load<Material>("Materials/SimpleColors/LightBlue");
		}
		else {
			lineRenderer.SetPositions(new Vector3[] { originPos, originPos + ray.direction * farEndDistance });
			lineRenderer.material = Resources.Load<Material>("Materials/SimpleColors/LightBlue");
		}

		return null;
	}

	void DrawRayReset() {
		if (lineObject != null) {
			Destroy(lineRenderer);
			Destroy(lineObject);
			
			lineObject = null;
			lineRenderer = null;
        }
    }

	void HighlightObj(GameObject gameObject) {
		Renderer objRender = gameObject.GetComponent<Renderer>();
		originalColor = objRender.material.color;
		originalShader = objRender.material.shader;

		objRender.material.shader = highlightShader;
		objRender.material.SetColor("_RimColor", highlightColor);
		objRender.material.SetColor("_MainColor", originalColor);

		Debug.Log(gameObject.name + " is selected.");
	}

	void DeHighlightObj(GameObject gameObject) {
		gameObject.GetComponent<Renderer>().material.color = originalColor;
		gameObject.GetComponent<Renderer>().material.shader = originalShader;

		Debug.Log(gameObject.name + " is deselected.");
	}

	void SelectReset() {
		DrawRayReset();

		if (isSelected == false && selectObj != null) {
			DeHighlightObj(selectObj);
			selectObj = null;
		}

		selectParams = null;
	}

	#endregion

	// Limitation info: limited range of scaling, because hands can't separate too distant before Leapmotion loses tracks
	void StretchWholeObj(GameObject gameObject) {
		if (palmDis != 0) {
			gameObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
			gameObject.transform.localScale = originalScale * palmDis / palmDisRef;
			gameObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;
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

		leftGesture.Type = Gesture.GestureType.Gesture_None;
		rightGesture.Type = Gesture.GestureType.Gesture_None;
    }

	public void OnFrame (object sender, FrameEventArgs args) {
		//Debug.Log("Leapmotin Frame Available.");

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
		Gesture_Fist,
		Gesture_Palm,
		Gesture_Gun,
		Gesture_OK,
		Gesture_Point,
		Gesture_Thumbup,
		Gesture_None, 
		Gesture_Unidentified
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
		Dictionary<string, ArrayList> gesture_fist_param = new Dictionary<string, ArrayList>() {
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

		gesture_param_list.Add(gesture_fist_param);
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

		bool isIdentified = false;
		gestureType = GestureType.Gesture_None;
		for (int i = 0; i < gesture_param_list.Count; i++) {
			if (CompareDict(gesture_param, gesture_param_list[i])) {
				gestureType = (GestureType)i;
				isIdentified = true;
				break;
            }
        }

		if (isIdentified == false) {
			gestureType = GestureType.Gesture_Unidentified;
        }

		return gestureType;
	}

	#region Define gesture commands
	public string Create(List<NameButton> buttonList, float hoverThreshold, float touchThreshold) {
		// Steps: 
		// 1. find index fingertip pos
		// 2. find button within range
		// 3. change within-range button color based on vertical dis
		// 4. send button name to CallCompiler 
		// 5. set flag to avoid callcompiler repeatedly (only create when button state turn from "hover" to "select")
		// 6. after created, reset all button color and flag
		// (Step 5-6 in Update)

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
		string selectedButtonName = "";
		foreach (NameButton currButton in buttonList) {
			if (currButton.WithinRange(fingertipPos)) {

				// Step 3. Change within-range button color based on vertical dis
				// later TODO: after selected when finger raise, don't show "hover" color (use flag to control?)
				if (currButton.VerticalDis(fingertipPos) <= hoverThreshold &&
					currButton.VerticalDis(fingertipPos) > touchThreshold) {
					currButton.ChangeColor("hover");
					selectedButtonName = currButton.name + "-hover";
				}
				else if (currButton.VerticalDis(fingertipPos) <= touchThreshold &&
					currButton.VerticalDis(fingertipPos) >= -touchThreshold) {
					currButton.ChangeColor("select");
					selectedButtonName = currButton.name;
				}
				else {
					currButton.ChangeColor("normal");
				}
            } else {
				currButton.ChangeColor("normal");
            }
        }

		// Step 4. send button name to CallCompiler (in Update)
		return selectedButtonName;
	}

    public Dictionary<string, dynamic> Grab() {
		Dictionary<string, dynamic> grabParams = new Dictionary<string, dynamic>();
		if (currHand.IsLeft) {
			try {
				grabParams["handPosition"] = GameObject.Find("L_Palm").transform.position;
				grabParams["handRotation"] = GameObject.Find("L_Palm").transform.eulerAngles;
			} catch {
				Debug.Log("Fail to find left palm.");
				return null;
            }
			try {
				// Only detect collision between palm and object for now (finger colliders exist, unused)
				GameObject palmCollider = GameObject.Find("L_Palm/palm");
				string colliderName = palmCollider.GetComponent<handCollisionManagement>().ColliderName;
				grabParams["colliderName"] = colliderName;
				grabParams["contactPosition"] = palmCollider.GetComponent<handCollisionManagement>().ContactPosition;
			} catch {
				Debug.Log("Fail to find left palm collider.");
				return null;
            }
        } else {
			try {
				grabParams["handPosition"] = GameObject.Find("R_Palm").transform.position;
				grabParams["handRotation"] = GameObject.Find("R_Palm").transform.eulerAngles;
			} catch {
				Debug.Log("Fail to find right palm.");
				return null;
            }
			try {
				// Only detect collision between palm and object for now (finger colliders exist, unused)
				GameObject palmCollider = GameObject.Find("R_Palm/palm");
				string colliderName = palmCollider.GetComponent<handCollisionManagement>().ColliderName;
				grabParams["colliderName"] = colliderName;
				grabParams["contactPosition"] = palmCollider.GetComponent<handCollisionManagement>().ContactPosition;
			} catch {
				Debug.Log("Fail to find right palm collider.");
				return null;
            }
        }

		return grabParams;
	}

	public Dictionary<string, dynamic> Select() {
		Dictionary<string, dynamic> selectParams = new Dictionary<string, dynamic>();
		if (currHand.IsLeft) {
			try {
				selectParams["fingertipPos"] = GameObject.Find("L_index_end").transform.position;
			}
			catch {
				Debug.Log("Fail to find left fingertip position.");
				return null;
			}
			try {
				selectParams["fingerbasePos"] = GameObject.Find("L_index_a").transform.position;
			}
			catch {
				Debug.Log("Fail to find left fingerbase position.");
				return null;
			}
		}
		else {
			try {
				selectParams["fingertipPos"] = GameObject.Find("R_index_end").transform.position;
			}
			catch {
				Debug.Log("Fail to find left fingertip position.");
				return null;
			}
			try {
				selectParams["fingerbasePos"] = GameObject.Find("R_index_a").transform.position;
			}
			catch {
				Debug.Log("Fail to find left fingerbase position.");
				return null;
			}
		}

		return selectParams;
    }

	public float Stretch(Hand leftHand, Hand rightHand, float palmToPalmThreshold) {
		// If palm-to-palm, return palm pos dis
		if (leftHand.PalmNormal.AngleTo(rightHand.PalmNormal) * Mathf.Rad2Deg >= palmToPalmThreshold) {
			return leftHand.PalmPosition.DistanceTo(rightHand.PalmPosition); // in mm
		}
		else {
			return 0;
        }
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
