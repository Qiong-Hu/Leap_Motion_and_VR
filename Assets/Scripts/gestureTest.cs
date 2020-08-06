// This script is attached to Leap Rig
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Windows;
using Leap;
using FARVR.Design;
using SimpleJSON;

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
	static Vector3 originalPos = new Vector3(0, 10, -0.1f);
	static Vector3 originalScale = Vector3.one * 10f;

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
	static Color highlightColor = new Color32(255, 0, 255, 255);
	public Shader highlightShader;

	// For changing obj params
	Dictionary<string, dynamic> currParams;

	// For storing gesture params on plane/line/point
	Dictionary<string, Vector3> leftPlane = null;
	Dictionary<string, Vector3> leftLine = null;
	Dictionary<string, Vector3> leftPoint = null;
	Dictionary<string, Vector3> rightPlane = null;
	Dictionary<string, Vector3> rightLine = null;
	Dictionary<string, Vector3> rightPoint = null;

	// For changing discrete params (leg num, boat n, etc) => TODO: need improvement
	Dictionary<string, float> tuneParams = null;
	bool isTuned = false;
	static float palmAngleTHLD = 5f; // Degree
	#endregion

	#region Customized Gesture Determination
	[Header("Customized Gesture Determination")]
	public Gesture.GestureType createGesture = Gesture.GestureType.Gesture_Point;
	public Gesture.GestureType grabGesture = Gesture.GestureType.Gesture_Fist;
	public Gesture.GestureType selectGesture = Gesture.GestureType.Gesture_Gun;
	public Gesture.GestureType confirmGesture = Gesture.GestureType.Gesture_OK;
	public Gesture.GestureType planeGesture = Gesture.GestureType.Gesture_Palm;
	public Gesture.GestureType lineGesture = Gesture.GestureType.Gesture_DoublePoint;
	public Gesture.GestureType pointGesture = Gesture.GestureType.Gesture_Pinch;
	#endregion

	// Use this for initialization
	void Start () {
		controller.Connect += gestureListener.OnServiceConnect;
		controller.Device += gestureListener.OnConnect;
		controller.FrameReady += gestureListener.OnFrame;
		Debug.Log("Virtual design begins.");

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
					//TuneObj(selectObj.GetComponent<DesignObj>());
				}
            }
		}
	}

	// Register gestures to determined gesture commands
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

		// Modify plane
		if (leftGesture.Type == planeGesture) {
			leftPlane = leftGesture.PlaneParams();
		}
		else {
			leftPlane = null;
        }
		if (rightGesture.Type == planeGesture) {
			rightPlane = rightGesture.PlaneParams();
        }
		else {
			rightPlane = null;
        }

		// Modify line
		if (leftGesture.Type == lineGesture) {
			leftLine = leftGesture.LineParams();
        }
		else {
			leftLine = null;
        }
		if (rightGesture.Type == lineGesture) {
			rightLine = rightGesture.LineParams();
        }
		else {
			rightLine = null;
        }

		// Modify point
		if (leftGesture.Type == pointGesture) {
			leftPoint = leftGesture.PointParams();
        }
		else {
			leftPoint = null;
        }
		if (rightGesture.Type == pointGesture) {
			rightPoint = rightGesture.PointParams();
        }
		else {
			rightPoint = null;
        }

		#region // Tune discrete parameters => TODO: need improvement
		//  (right hand prior to left)
		//if (rightGesture.Type == tuneDiscreteGesture) {
		//	tuneParams = rightGesture.TuneDiscrete();
		//}
		//else if (leftGesture.Type == tuneDiscreteGesture) {
		//	tuneParams = leftGesture.TuneDiscrete();
		//}
		//else {
		//	TuneReset();
		//}
		#endregion

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
                }
			}
		}
		selectedButtonNamePrev = selectedButtonName;
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

		// Reset all params to default values
		DesignObj designObj = gameObject.GetComponent<DesignObj>();
		designObj.TransformDesign(originalPos, Quaternion.identity, originalScale);
		designObj.UpdateDesign(designObj.GetDefaultsCurr());

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

		currParams = gameObject.GetComponent<DesignObj>().GetParameters();
		Debug.Log(gameObject.name + " is selected.");
	}

	void DeHighlightObj(GameObject gameObject) {
		gameObject.GetComponent<Renderer>().material.color = originalColor;
		gameObject.GetComponent<Renderer>().material.shader = originalShader;

		Debug.Log(gameObject.name + " is deselected.");
	}

	// De-select, Confirm
	void SelectReset() {
		DrawRayReset();

		if (isSelected == false && selectObj != null) {
			DeHighlightObj(selectObj);
			selectObj = null;

			// Reset anything related to change obj params
			currParams = null;
			TuneReset();
		}

		selectParams = null;
	}

	#endregion

	// Limitation info: limited range of scaling, because hands can't separate too distant before Leapmotion loses tracks
	// TODO: working on it
	//void StretchWholeObj(GameObject gameObject) {
	//	if (palmDis != 0) {
	//		gameObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
	//		gameObject.transform.localScale = originalScale * palmDis / palmDisRef;
	//		gameObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;
	//	}
    //}

    #region Tune discrete parameters
	// TODO: need improvement => more intuitive, extendible
	void TuneObj(DesignObj designObj) {
		// Steps:
		// 0. find integer param
		// 1. Detect hand gesture is thumb up
		// 2. When hand gesture point horizontal, trigger isChangeDiscrete
		// 3. When hand gesture point upwards, integer + 1; downwards, integer - 1
		// 4. Update obj; Reset isChangeDiscrete
		// 5. TuneReset condition: finish add/minus, or detect confirm gesture (de-select)

		// Find obj's discrete param name according to paramtype
		// Future bug info: what if obj has more than one discrete param?
		string discreteParam = "";
		Dictionary<string, string> paramtype = designObj.GetParamType();
		if (paramtype.Count > 0) {
			foreach (KeyValuePair<string, string> kvp in paramtype) {
				if (kvp.Value == "count") {
					discreteParam = kvp.Key;
					break;
                }
            }
        } else {
			// No discrete param for tuning.
        }

		int action = 0;
		if (tuneParams != null && discreteParam != "") {
			TuneInit();
			action = TuneUpdate(); // action = 0, +1, -1

			if (isTuned == true && action != 0) {
				// Update design obj here
				currParams[discreteParam] = currParams[discreteParam] + action;

				try {
					designObj.UpdateDesign(currParams);
					HighlightObj(designObj.gameObject);
					Debug.Log("Modified " + discreteParam + " of " + designObj.GetName() + ".");
                }
				catch {
					Debug.Log("Fail to modify " + discreteParam + " of " + designObj.GetName() + ".");
                }

				TuneReset();
            } else {
				// No tuning action is taken.
            }
		}
    }

	void TuneInit() {
		if (isTuned == false && Mathf.Abs(tuneParams["palmAngle"] - 90f) <= palmAngleTHLD) {
			isTuned = true;
        }
    }

	int TuneUpdate() {
		// If point upwards, return +1; if point downwards, return -1; else, return 0
		int action = 0;

		if (isTuned == true && tuneParams["palmAngle"] <= palmAngleTHLD) {
			action = 1;
        }
		else if (isTuned == true && tuneParams["palmAngle"] >= 180f - palmAngleTHLD) {
			action = -1;
        }
		
		return action;
    }

	void TuneReset() {
		tuneParams = null;
		isTuned = false;
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
		
		ListenerInit();
    }

	public void ListenerInit() {
		leftGesture.GestureInit();
		rightGesture.GestureInit();
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
		Gesture_DoublePoint,
		Gesture_Thumbup,
		Gesture_Pinch,
		Gesture_None,
		Gesture_Unidentified
	}

	// Current gesture type
	private GestureType gestureType = GestureType.Gesture_None;
	public GestureType Type {
		get { return gestureType; }
		set { gestureType = value; }
	}

	// Current hand and gesture params
	public Hand currHand = new Hand();
	private Dictionary<string, ArrayList> gesture_param = new Dictionary<string, ArrayList>() {
		{ "IsExtended", new ArrayList {null, null, null, null, null} },
		{ "GrabStrength", new ArrayList { null} },
		{ "PinchStrength", new ArrayList { null} }
	};
	private string handPolarity = "";

	public void GestureInit() {
		gestureType = GestureType.Gesture_None;
		RegisterGestureParams();
    }

    #region For detecting gesture type
    // Pre-defined Gesture Parameters: {IsExtended (5 bool/null), PinchStrength (0-1), GrabStrength (0-1)}
    private List<Dictionary<string, ArrayList>> gesture_param_list = new List<Dictionary<string, ArrayList>>();
	private void RegisterGestureParams() { 
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
		Dictionary<string, ArrayList> gesture_double_point_param = new Dictionary<string, ArrayList>() {
			{ "IsExtended", new ArrayList { false, true, true, false, false} }
		}; // TODO: later if add "yes" gesture, careful might cause confusion, for now: didn't constraint two fingers to stay together
		Dictionary<string, ArrayList> gesture_thumbup_param = new Dictionary<string, ArrayList>() {
			{ "IsExtended", new ArrayList { true, false, false, false, false} }
		};
		Dictionary<string, ArrayList> gesture_pinch_param = new Dictionary<string, ArrayList>() {
			{ "IsExtended", new ArrayList { null, null, false, false, false} },
			{ "PinchStrength", new ArrayList { 1f} }
		};

		gesture_param_list.Add(gesture_fist_param);
		gesture_param_list.Add(gesture_palm_param);
		gesture_param_list.Add(gesture_gun_param);
		gesture_param_list.Add(gesture_ok_param);
		gesture_param_list.Add(gesture_point_param);
		gesture_param_list.Add(gesture_double_point_param);
		gesture_param_list.Add(gesture_thumbup_param);
		gesture_param_list.Add(gesture_pinch_param);
	}

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
		if (hand.IsLeft) {
			handPolarity = "Left";
        }
		else {
			handPolarity = "Right";
        }
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
    
	#endregion

	#region Define gesture commands
	/// <summary>
    /// Input buttonList, returns selected button name
    /// </summary>
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

		try {
			fingertipPos = GameObject.Find(handPolarity[0] + "_index_end").transform.position;
        } catch {
			Debug.Log("Fail to find " + handPolarity.ToLower() + " fingertip position.");
			return null;
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

	/// <summary>
    /// Returns "handPosition", "handRotation", "colliderName", "contactPosition"
    /// </summary>
    public Dictionary<string, dynamic> Grab() {
		Dictionary<string, dynamic> grabParams = new Dictionary<string, dynamic>();

		try {
			grabParams["handPosition"] = GameObject.Find(handPolarity[0] + "_Palm").transform.position;
			grabParams["handRotation"] = GameObject.Find(handPolarity[0] + "_Palm").transform.eulerAngles;
		} catch {
			Debug.Log("Fail to find " + handPolarity.ToLower() + " palm.");
			return null;
        }
		try {
			// Only detect collision between palm and object for now (finger colliders exist, unused)
			GameObject palmCollider = GameObject.Find(handPolarity[0] + "_Palm/palm");
			string colliderName = palmCollider.GetComponent<handCollisionManagement>().ColliderName;
			grabParams["colliderName"] = colliderName;
			grabParams["contactPosition"] = palmCollider.GetComponent<handCollisionManagement>().ContactPosition;
		} catch {
			Debug.Log("Fail to find " + handPolarity.ToLower() + " palm collider.");
			return null;
        }

		return grabParams;
	}

	/// <summary>
    /// Returns "fingertipPos", "fingerbasePos"
    /// </summary>
	public Dictionary<string, dynamic> Select() {
		Dictionary<string, dynamic> selectParams = new Dictionary<string, dynamic>();

		try {
			selectParams["fingertipPos"] = GameObject.Find(handPolarity[0] + "_index_end").transform.position;
		}
		catch {
			Debug.Log("Fail to find " + handPolarity.ToLower() + " fingertip position.");
			return null;
		}
		try {
			selectParams["fingerbasePos"] = GameObject.Find(handPolarity[0] + "_index_a").transform.position;
		}
		catch {
			Debug.Log("Fail to find " + handPolarity.ToLower() + " fingerbase position.");
			return null;
		}

		return selectParams;
    }

	/// <summary>
    /// [Obsolete methods]
    /// returns distance (in mm) between two parallel palms
    /// </summary>
	public float Stretch(Hand leftHand, Hand rightHand, float palmToPalmNormTHLD, float palmToPalmRotTHLD) {
		// If palm-to-palm, return palm pos dis
		// Palm-to-palm def: palm rot parallel, center of palms face-to-face
		if (leftHand.PalmNormal.AngleTo(rightHand.PalmNormal) * Mathf.Rad2Deg >= palmToPalmNormTHLD && 
			leftHand.Direction.AngleTo(rightHand.Direction) * Mathf.Rad2Deg <= palmToPalmRotTHLD) {
			return leftHand.PalmPosition.DistanceTo(rightHand.PalmPosition); // in mm
		}
		else {
			return 0;
        }
	}

	// Pass params of plane (using palm) to main Update()
	/// <summary>
    /// Returns "position", "forwardDir", "normalDir"
    /// </summary>
	public Dictionary<string, Vector3> PlaneParams() {
		Dictionary<string, Vector3> planeParams = new Dictionary<string, Vector3>();

		try {
			planeParams["position"] = GameObject.Find(handPolarity[0] + "_Palm").transform.position;
		}
		catch {
			Debug.Log("Fail to find " + handPolarity.ToLower() + " palm position.");
			return null;
		}

		planeParams["forwardDir"] = new Vector3(currHand.Direction.x, currHand.Direction.y, currHand.Direction.z);
		planeParams["normalDir"] = new Vector3(currHand.PalmNormal.z, currHand.PalmNormal.y, currHand.PalmNormal.z);
		
		return planeParams;
	}

	// Pass params of line (using finger as representative) to main Update()
	/// <summary>
    /// Returns "position", "direction"
    /// </summary>
	public Dictionary<string, Vector3> LineParams() {
		Dictionary<string, Vector3> lineParams = new Dictionary<string, Vector3>();

		Vector3 fingertipPos = new Vector3();
		try {
			fingertipPos = GameObject.Find(handPolarity[0] + "_index_end").transform.position;
		}
		catch {
			Debug.Log("Fail to find " + handPolarity.ToLower() + " fingertip position.");
			return null;
		}

		Vector3 fingerbasePos = new Vector3();
		try {
			fingerbasePos = GameObject.Find(handPolarity[0] + "_index_a").transform.position;
		}
		catch {
			Debug.Log("Fail to find " + handPolarity.ToLower() + " fingerbase position.");
			return null;
		}

		lineParams["position"] = fingerbasePos;
		lineParams["direction"] = (fingertipPos - fingerbasePos).normalized;

		return lineParams;
	}

	// Pass params of point (using pinch) to main Update()
	/// <summary>
    /// Returns "position"
    /// </summary>
	public Dictionary<string, Vector3> PointParams() {
		Dictionary<string, Vector3> pointParams = new Dictionary<string, Vector3>();

		Vector3 indexTipPos = new Vector3();
		try {
			indexTipPos = GameObject.Find(handPolarity[0] + "_index_end").transform.position;
		}
		catch {
			Debug.Log("Fail to find " + handPolarity.ToLower() + " index fingertip position.");
			return null;
		}

		Vector3 thumbTipPos = new Vector3();
		try {
			thumbTipPos = GameObject.Find(handPolarity[0] + "_thumb_end").transform.position;
		}
		catch {
			Debug.Log("Fail to find " + handPolarity.ToLower() + " thumb fingertip position.");
			return null;
		}

		pointParams["position"] = (indexTipPos + thumbTipPos) / 2;

		return pointParams;
	}

	// Edit tune discrete param => TODO: need improvement
	public Dictionary<string, float> TuneDiscrete() {
		Dictionary<string, float> tuneParams = new Dictionary<string, float>();
		
		// Palm rotation (in euler angle), angle to upwards in world coordinate
		Quaternion palmRot = new Quaternion(currHand.Rotation.x, currHand.Rotation.y, currHand.Rotation.z, currHand.Rotation.w);
		float angleUp = palmRot.eulerAngles.z;
		if (angleUp > 180f) {
			angleUp = 360f - angleUp;
        }
		//Debug.Log(angleUp);
		tuneParams["palmAngle"] = angleUp;

		// Thumb direction, angle to upwards in world coordinate
		try {
			Vector3 thumbDir = GameObject.Find(handPolarity[0] + "_thumb_end").transform.position - 
				GameObject.Find(handPolarity[0] + "_Palm").transform.position;

			tuneParams["thumbAngle"] = Vector3.Angle(Vector3.up, thumbDir);
		}
		catch {
			Debug.Log("Fail to find " + handPolarity.ToLower() + " thumb position");
        }

		return tuneParams;
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
