// This script is attached to Leap Rig
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Windows;
using Leap;
using FARVR.Design;
using SimpleJSON;
using GestureDefinition;

public class gestureTest : MonoBehaviour {

	// For test
	GameObject testObject;

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
	Gesture.GrabParams grabParams = new Gesture.GrabParams();
	Gesture.GrabParams grabParamsPrev = new Gesture.GrabParams();
	GameObject grabObj = null;
	GameObject contactPoint = null;

	// For SelectObj
	Gesture.SelectParams selectParams = new Gesture.SelectParams();
	Gesture.SelectParams selectParamsPrev = new Gesture.SelectParams();
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
	Dictionary<string, dynamic> currParams = null;

	// For storing gesture params on plane/line/point
	Gesture.PlaneParams leftPlane = new Gesture.PlaneParams();
	Gesture.PlaneParams rightPlane = new Gesture.PlaneParams();
	Gesture.LineParams leftLine = new Gesture.LineParams();
	Gesture.LineParams rightLine = new Gesture.LineParams();
	Gesture.PointParams leftPoint = new Gesture.PointParams();
	Gesture.PointParams rightPoint = new Gesture.PointParams();

	// For storing gesture geos together
	struct GestureGeo {
		public bool planeDetected;
		public Gesture.PlaneParams leftPlane;
		public Gesture.PlaneParams rightPlane;
		public bool lineDetected;
		public Gesture.LineParams leftLine;
		public Gesture.LineParams rightLine;
		public bool pointDetected;
		public Gesture.PointParams leftPoint;
		public Gesture.PointParams rightPoint;
	}
	GestureGeo gestureGeo = new GestureGeo();
	GestureGeo gestureGeoPrev = new GestureGeo();
	GestureGeo gestureGeoInit = new GestureGeo();

	// For searching targeted object plane
	static float planeDirPosRatio = 0.8f;

	// For changing discrete params (leg num, boat n, etc) => TODO: need improvement
	Gesture.TuneParams tuneParams = new Gesture.TuneParams();
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

		testObject = GameObject.Find("Cube");// For test
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

		// Init gesture geo param
		GestureGeoInit();
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
					//CalcGestureGeo();
				}

				// For debug
				CalcGestureGeo();

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
			leftPlane = leftGesture.FindPlaneParams();
		}
		else {
			leftPlane.isEmpty = true;
        }
		if (rightGesture.Type == planeGesture) {
			rightPlane = rightGesture.FindPlaneParams();
        }
		else {
			rightPlane.isEmpty = true;
        }

		// Modify line
		if (leftGesture.Type == lineGesture) {
			leftLine = leftGesture.FindLineParams();
        }
		else {
			leftLine.isEmpty = true;
        }
		if (rightGesture.Type == lineGesture) {
			rightLine = rightGesture.FindLineParams();
        }
		else {
			rightLine.isEmpty = true;
        }

		// Modify point
		if (leftGesture.Type == pointGesture) {
			leftPoint = leftGesture.FindPointParams();
        }
		else {
			leftPoint.isEmpty = true;
        }
		if (rightGesture.Type == pointGesture) {
			rightPoint = rightGesture.FindPointParams();
        }
		else {
			rightPoint.isEmpty = true;
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

						selectParams.isEmpty = true;
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
		if (grabParams.isEmpty != true && grabParamsPrev.isEmpty == true && grabObj == null && grabParams.colliderName != "") {
			GrabInit();
		}
		// Update grabbing
		else if (grabParams.isEmpty != true && grabParamsPrev.isEmpty != true && grabObj != null){
			GrabUpdate();
        }
		// End grabbing
		else if (grabParams.isEmpty == true && grabParamsPrev.isEmpty != true && grabObj != null) {
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
		grabObj = GameObject.Find(grabParams.colliderName);

		contactPoint = new GameObject("Contact Point");
		contactPoint.transform.position = grabParams.handPosition;
		contactPoint.transform.eulerAngles = grabParams.handRotation;

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
		contactPoint.transform.position = grabParams.handPosition;
		contactPoint.transform.eulerAngles = grabParams.handRotation;
		
		grabObj.transform.position = contactPoint.transform.GetChild(0).position;
		grabObj.transform.eulerAngles = contactPoint.transform.GetChild(0).eulerAngles;
	}

	void GrabEnd() {
		contactPoint.transform.position = grabParamsPrev.handPosition;
		contactPoint.transform.eulerAngles = grabParamsPrev.handRotation;

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
		grabParams.isEmpty = true;

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
		if (selectParams.isEmpty != true && selectParamsPrev.isEmpty == true) {
			DrawRayInit();
			currObj = DrawRayUpdate(selectParams.fingerbasePos, selectParams.fingertipPos);
		} 
		else if (selectParams.isEmpty != true && selectParamsPrev.isEmpty != true) {
			currObj = DrawRayUpdate(selectParams.fingerbasePos, selectParams.fingertipPos);
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

		selectParams.isEmpty = true;
	}

    #endregion

    #region [Obsolete] Stretch whole obj
    // Limitation info: limited range of scaling, because hands can't separate too distant before Leapmotion loses tracks
    //void StretchWholeObj(GameObject gameObject) {
    //	if (palmDis != 0) {
    //		gameObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
    //		gameObject.transform.localScale = originalScale * palmDis / palmDisRef;
    //		gameObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;
    //	}
    //}
    #endregion

    #region Calculate gesture geometry
    void GestureGeoInit() {
		gestureGeo.planeDetected = false;
		gestureGeo.lineDetected = false;
		gestureGeo.pointDetected = false;
		
		gestureGeoPrev = gestureGeo;
		gestureGeoInit = gestureGeo;
	}

	void CalcGestureGeo() {
		GesturePlane();
		GestureLine();
		GesturePoint();
    }
    #endregion

    #region Manipulate object plane geometry with gesture plane
    void GesturePlane() {
		// if leftPlane, rightPlane exist, planeGeo exist
		// if prev planeGeo not exist, curr planeGeo exist, call planeInit, begin to search for closest plane geo in obj
		// if prev planeFro exist, curr planeGeo exist, call planeUpdate; if closest obj planeGeo exist, update targeted obj planeGeo, pass to obj

		if (leftPlane.isEmpty != true && rightPlane.isEmpty != true) {
			gestureGeo.planeDetected = true;
        } else {
			gestureGeo.planeDetected = false;
        }

		if (gestureGeo.planeDetected == true && gestureGeoPrev.planeDetected == false) {
			GesturePlaneInit();
			SearchPlanePair(testObject, gestureGeoInit);// For test
        }
		else if (gestureGeo.planeDetected == true && gestureGeoPrev.planeDetected == true) {
			GesturePlaneUpdate();
        }

		gestureGeoPrev.planeDetected = gestureGeo.planeDetected;
		gestureGeoPrev.leftPlane = gestureGeo.leftPlane;
		gestureGeoPrev.rightPlane = gestureGeo.rightPlane;

		// For debug
		if (rightPlane.isEmpty != true) {
			List<Gesture.PlaneParams> planeList = GetCubePlanes(testObject);
			SortPlanes(testObject, planeList, rightPlane);
		}
	}

	void GesturePlaneInit() {
		Debug.Log("plane-plane gesture is detected.");

		gestureGeoInit.leftPlane = leftPlane;
		gestureGeoInit.rightPlane = rightPlane;
		gestureGeoInit.planeDetected = true;

		gestureGeo.leftPlane = leftPlane;
		gestureGeo.rightPlane = rightPlane;

		//gestureGeo["planePosInit"] = leftPlane.position - rightPlane.position;
		//gestureGeo["planeForwardDirInit"] = Vector3.Angle(leftPlane.forwardDir, rightPlane.forwardDir);
		//gestureGeo["planeNormalDirInit"] = Vector3.Angle(leftPlane.normalDir, rightPlane.normalDir);
	}

	void GesturePlaneUpdate() {
		gestureGeo.leftPlane = leftPlane;
		gestureGeo.rightPlane = rightPlane;

		//gestureGeo["planePos"] = leftPlane.position - rightPlane.position;
		//gestureGeo["planeForwardDir"] = Vector3.Angle(leftPlane.forwardDir, rightPlane.forwardDir);
		//gestureGeo["planeNormalDir"] = Vector3.Angle(leftPlane.normalDir, rightPlane.normalDir);
    }

	// For testing object Cube
	List<Gesture.PlaneParams> GetCubePlanes(GameObject gameObject) {
		Vector3 center = gameObject.transform.position;
		List<Gesture.PlaneParams> planes = new List<Gesture.PlaneParams>();

		Gesture.PlaneParams plane = new Gesture.PlaneParams();
		plane.name = "x+";
		plane.index = 1;
		plane.position = center + gameObject.transform.right * gameObject.transform.localScale.x;
		plane.normalDir = gameObject.transform.right;
		planes.Add(plane);

		plane.name = "x-";
		plane.index = 2;
		plane.position = center - gameObject.transform.right * gameObject.transform.localScale.x;
		plane.normalDir = -gameObject.transform.right;
		planes.Add(plane);

		plane.name = "y+";
		plane.index = 3;
		plane.position = center + gameObject.transform.up * gameObject.transform.localScale.y;
		plane.normalDir = gameObject.transform.up;
		planes.Add(plane);

		plane.name = "y-";
		plane.index = 4;
		plane.position = center - gameObject.transform.up * gameObject.transform.localScale.y;
		plane.normalDir = -gameObject.transform.up;
		planes.Add(plane);

		plane.name = "z+";
		plane.index = 5;
		plane.position = center + gameObject.transform.forward * gameObject.transform.localScale.z;
		plane.normalDir = gameObject.transform.forward;
		planes.Add(plane);

		plane.name = "z-";
		plane.index = 6;
		plane.position = center - gameObject.transform.forward * gameObject.transform.localScale.z;
		plane.normalDir = -gameObject.transform.forward;
		planes.Add(plane);

		return planes;
	}

	List<Gesture.PlaneParams> SortPlanes(GameObject gameObject, List<Gesture.PlaneParams> planeList, Gesture.PlaneParams targetPlane) {
		List<Gesture.PlaneParams> planeListNew = new List<Gesture.PlaneParams>();
		List<float> scores = new List<float>();

		float score;
		float centerDis = Vector3.Distance(targetPlane.position, gameObject.transform.position); // For normalization
		foreach (Gesture.PlaneParams currPlane in planeList) {
			score = Vector3.Angle(currPlane.normalDir, targetPlane.normalDir) / 180f * planeDirPosRatio +
				Vector3.Distance(currPlane.position, targetPlane.position) / centerDis * (1 - planeDirPosRatio);
			scores.Add(score);
        }

		// Add planes from planeList to planeListNew in the order of score value from small to large
		int[] scoreIdx = Enumerable.Range(0, scores.Count).ToArray<int>();
		System.Array.Sort<int>(scoreIdx, (i, j) => scores[i].CompareTo(scores[j]));
		Debug.Log("Most likely: " + planeList[scoreIdx[0]].name + ", " + planeList[scoreIdx[1]].name);// For debug

		
		return planeListNew;
	}

	List<Gesture.PlaneParams> SearchPlanePair(GameObject gameObject, GestureGeo gestureGeo) {
		List<Gesture.PlaneParams> selectedPlanePair = new List<Gesture.PlaneParams>();

		List<Gesture.PlaneParams> sortedPlaneListLeft = new List<Gesture.PlaneParams>();
		List<Gesture.PlaneParams> sortedPlaneListRight = new List<Gesture.PlaneParams>();

		List<Gesture.PlaneParams> planeList = GetCubePlanes(testObject);
		if (gestureGeo.planeDetected == true) {
			if (gestureGeo.leftPlane.isEmpty != true) {
				sortedPlaneListLeft = SortPlanes(gameObject, planeList, gestureGeo.leftPlane);
			}
			else {
				Debug.Log("Fail to access left gesture plane for searching obj plane.");
            }
			if (gestureGeo.rightPlane.isEmpty != true) {
				sortedPlaneListRight = SortPlanes(gameObject, planeList, gestureGeo.rightPlane);
			}
			else {
				Debug.Log("Fail to access right gesture plane for seaching obj plane.");
            }
		}
		else {
			Debug.Log("Fail to access gesture plane geo for searching obj planes.");
        }
		
		// TODO: Based on sortedPlaneListLeft and sortedPlaneListRight to find the best-fit plane pair, add to selectedPlanePair

		return selectedPlanePair;
	}

	#endregion

    #region Calculate gesture line geometry
	void GestureLine() {
		if (leftLine.isEmpty != true && rightLine.isEmpty != true) {
			Debug.Log("line-line gesture is detected.");
			Debug.Log("line distance: " + Vector3.Distance(leftLine.position, rightLine.position).ToString());
			Debug.Log("line direction diff: " + Vector3.Angle(leftLine.direction, rightLine.direction).ToString());
		}
	}

	void GestureLineInit() {
		
	}

	void GestureLineUpdate() {

    }

    #endregion

    #region Calculate gesture point geometry
	void GesturePoint() {
		if (leftPoint.isEmpty != true && rightPoint.isEmpty != true) {
			Debug.Log("point-point gesture is detected.");
			Debug.Log("point distance: " + Vector3.Distance(leftPoint.position, rightPoint.position).ToString());
		}
	}

	void GesturePointInit() {
		
	}

	void GesturePointUpdate() {

    }

    #endregion

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
		if (tuneParams.isEmpty != true && discreteParam != "") {
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
		if (isTuned == false && Mathf.Abs(tuneParams.palmAngle - 90f) <= palmAngleTHLD) {
			isTuned = true;
        }
    }

	int TuneUpdate() {
		// If point upwards, return +1; if point downwards, return -1; else, return 0
		int action = 0;

		if (isTuned == true && tuneParams.palmAngle <= palmAngleTHLD) {
			action = 1;
        }
		else if (isTuned == true && tuneParams.palmAngle >= 180f - palmAngleTHLD) {
			action = -1;
        }
		
		return action;
    }

	void TuneReset() {
		tuneParams.isEmpty = true;
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
