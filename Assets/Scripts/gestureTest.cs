// This script is attached to Leap Rig
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Windows;
using Leap;
using FARVR.Design;
using FARVR.GestureDefinition;
using FARVR.MathUtils;
using SimpleJSON;
using FARVR.GeoParams;

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
	Geometry.PlaneParams leftPlane = new Geometry.PlaneParams();
	Geometry.PlaneParams rightPlane = new Geometry.PlaneParams();
	Geometry.LineParams leftLine = new Geometry.LineParams();
	Geometry.LineParams rightLine = new Geometry.LineParams();
	Geometry.PointParams leftPoint = new Geometry.PointParams();
	Geometry.PointParams rightPoint = new Geometry.PointParams();

	// For storing gesture geos together
	class GestureGeo {
		public Geometry.PlaneParams leftPlane, rightPlane;
		public Geometry.LineParams leftLine, rightLine;
		public Geometry.PointParams leftPoint, rightPoint;

		private int count;
		public int Count {
			get {
				count = 0;
				if (leftPlane.isEmpty != true) count += 1;
				if (rightPlane.isEmpty != true) count += 1;
				if (leftLine.isEmpty != true) count += 1;
				if (rightLine.isEmpty != true) count += 1;
				if (leftPoint.isEmpty != true) count += 1;
				if (rightPoint.isEmpty != true) count += 1;

				return count;
            }
		}
	
		public void Reset() {
			leftPlane.isEmpty = true;
			rightPlane.isEmpty = true;
			leftLine.isEmpty = true;
			rightLine.isEmpty = true;
			leftPoint.isEmpty = true;
			rightPoint.isEmpty = true;
		}

		public void Copy(GestureGeo gestureGeo) {
			leftPlane = gestureGeo.leftPlane;
			rightPlane = gestureGeo.rightPlane;
			leftLine = gestureGeo.leftLine;
			rightLine = gestureGeo.rightLine;
			leftPoint = gestureGeo.leftPoint;
			rightPoint = gestureGeo.rightPoint;
        }

		public override string ToString() {
			List<string> nonEmptyNames = new List<string>();
			if (leftPlane.isEmpty != true) {
				if (leftPlane.name != "") {
					nonEmptyNames.Add(leftPlane.name);
                }
				else {
					nonEmptyNames.Add("leftPlane");
                }
            }
			if (rightPlane.isEmpty != true) {
				if (rightPlane.name != "") {
					nonEmptyNames.Add(rightPlane.name);
				}
				else {
					nonEmptyNames.Add("rightPlane");
				}
			}
			if (leftLine.isEmpty != true) {
				if (leftLine.name != "") {
					nonEmptyNames.Add(leftLine.name);
				}
				else {
					nonEmptyNames.Add("leftLine");
				}
			}
			if (rightLine.isEmpty != true) {
				if (rightLine.name != "") {
					nonEmptyNames.Add(rightLine.name);
				}
				else {
					nonEmptyNames.Add("rightLine");
				}
			}
			if (leftPoint.isEmpty != true) {
				if (leftPoint.name != "") {
					nonEmptyNames.Add(leftPoint.name);
				}
				else {
					nonEmptyNames.Add("leftPoint");
				}
			}
			if (rightPoint.isEmpty != true) {
				if (rightPoint.name != "") {
					nonEmptyNames.Add(rightPoint.name);
				}
				else {
					nonEmptyNames.Add("rightPoint");
				}
			}

			return string.Join(", ", nonEmptyNames.ToArray<string>());
        }
	}

	GestureGeo gestureGeo = new GestureGeo();
	GestureGeo gestureGeoPrev = new GestureGeo();
	GestureGeo gestureGeoInit = new GestureGeo();
	GestureGeo gestureGeoSelect = new GestureGeo();

	// For searching targeted object plane
	const float planeDirPosRatio = 0.8f;
	const float lineDirPosRatio = 0.8f;
	const float singlePairRatio = 0.8f;
	const float planePairDirPosRatio = 0.5f;
	const int geoSearchPatchSize = 10;

	// For changing discrete params (leg num, boat n, etc) => TODO: need improvement
	Gesture.TuneParams tuneParams = new Gesture.TuneParams();
	bool isTuned = false;
	const float palmAngleTHLD = 5f; // Degree
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
					CalcGestureGeo(selectObj);
									
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
		gestureGeo.Reset();
		gestureGeoPrev.Reset();
		gestureGeoInit.Reset();
		gestureGeoSelect.Reset();
	}

	void CalcGestureGeo(GameObject gameObject) {
		GesturePlaneUpdate();
		GestureLineUpdate();
		GesturePointUpdate();
		
		SelectObjGeo(gameObject);

		// For debug
		if (gestureGeoSelect.Count > 0) {
			Debug.Log(gestureGeoSelect.ToString());
        }

		gestureGeoPrev.Copy(gestureGeo);
    }

	void SelectObjGeo(GameObject gameObject) {
		GestureGeo searchGeoResult = new GestureGeo();
		searchGeoResult.Reset();

		List<Geometry.PlaneParams> planeList = GetPlaneList(gameObject);
		List<Geometry.LineParams> lineList = GetLineList(gameObject);
		List<Geometry.PointParams> pointList = GetPointList(gameObject);

		// Select plane pair
		if (gestureGeoInit.leftPlane.isEmpty != true && gestureGeoInit.rightPlane.isEmpty != true && 
			(gestureGeoSelect.leftPlane.isEmpty == true || gestureGeoSelect.rightPlane.isEmpty == true)) {
			searchGeoResult = SearchPlanePair(gameObject, planeList, gestureGeoInit);
			gestureGeoSelect.leftPlane = searchGeoResult.leftPlane;
			gestureGeoSelect.rightPlane = searchGeoResult.rightPlane;
        }

		// Select line pair

		// Select point pair

		// Select plane-line pair

		// Select plane-point pair

		// Select line-point pair

		// Reset selecting
		if (gestureGeoInit.Count == 0) {
			gestureGeoSelect.Reset();
		}
	}

	#endregion

	#region Search object plane geometry with gesture plane geometry
	void GesturePlaneUpdate() {
		gestureGeo.leftPlane = leftPlane;
		if (gestureGeo.leftPlane.isEmpty != true && gestureGeoPrev.leftPlane.isEmpty == true) {
			gestureGeoInit.leftPlane = leftPlane;
			Debug.Log("Left plane is initialized.");
        }
		if (gestureGeo.leftPlane.isEmpty == true && gestureGeoPrev.leftPlane.isEmpty != true) {
			gestureGeoInit.leftPlane.isEmpty = true;
        }

		gestureGeo.rightPlane = rightPlane;
		if (gestureGeo.rightPlane.isEmpty != true && gestureGeoPrev.rightPlane.isEmpty == true) {
			gestureGeoInit.rightPlane = rightPlane;
			Debug.Log("Right plane is initialized.");
        }
		if (gestureGeo.rightPlane.isEmpty == true && gestureGeoPrev.rightPlane.isEmpty != true) {
			gestureGeoInit.rightPlane.isEmpty = true;
        }
    }

	List<Geometry.PlaneParams> GetPlaneList(GameObject gameobject) {
		List<Geometry.PlaneParams> planeList = new List<Geometry.PlaneParams>();
		try {
			planeList = gameobject.GetComponent<DesignObj>().GetPlaneInfo(url);
		}
		catch {
			Debug.Log("Fail to get plane list from compiler.");
        }
		return planeList;
	}

	static float PlaneSimilarity(Geometry.PlaneParams planeEva, Geometry.PlaneParams planeTar, float refDistance, float planeDirPosRatio = planeDirPosRatio) {
		return mathUtils.DirectionSimilarity(planeEva.normalDir, planeTar.normalDir) * planeDirPosRatio
			+ Vector3.Distance(planeEva.position, planeTar.position) / refDistance * (1 - planeDirPosRatio);
    }

	List<Geometry.PlaneParams> SortPlanes(GameObject gameObject, List<Geometry.PlaneParams> planeList, Geometry.PlaneParams targetPlane) {
		List<Geometry.PlaneParams> planeListNew = new List<Geometry.PlaneParams>();
		List<float> scores = new List<float>();

		float centerDis = Vector3.Distance(targetPlane.position, gameObject.transform.position); // For pseudo- normalization
		foreach (Geometry.PlaneParams currPlane in planeList) {
			scores.Add(PlaneSimilarity(currPlane, targetPlane, centerDis));
        }

		// Add planes from planeList to planeListNew in the order of score value from small to large
		int[] scoreIdx = Enumerable.Range(0, scores.Count).ToArray<int>();
		Array.Sort<int>(scoreIdx, (i, j) => scores[i].CompareTo(scores[j]));

		Geometry.PlaneParams tmpPlane;
		for (int i = 0; i < scores.Count; i++) {
			tmpPlane = planeList[scoreIdx[i]];
			tmpPlane.confidence = scores[scoreIdx[i]];
			planeListNew.Add(tmpPlane);
		}
		
		return planeListNew;
	}

	#endregion

    #region Search object line geometry with gesture line geometry
	void GestureLineUpdate() {
		gestureGeo.leftLine = leftLine;
		if (gestureGeo.leftLine.isEmpty != true && gestureGeoPrev.leftLine.isEmpty == true) {
			gestureGeoInit.leftLine = leftLine;
			Debug.Log("Left line is initialized.");
		}
		if (gestureGeo.leftLine.isEmpty == true && gestureGeoPrev.leftLine.isEmpty != true) {
			gestureGeoInit.leftLine.isEmpty = true;
		}

		gestureGeo.rightLine = rightLine;
		if (gestureGeo.rightLine.isEmpty != true && gestureGeoPrev.rightLine.isEmpty == true) {
			gestureGeoInit.rightLine = rightLine;
			Debug.Log("Right line is initialized.");
		}
		if (gestureGeo.rightLine.isEmpty == true && gestureGeoPrev.rightLine.isEmpty != true) {
			gestureGeoInit.rightLine.isEmpty = true;
		}
	}

	List<Geometry.LineParams> GetLineList(GameObject gameobject) {
		List<Geometry.LineParams> lineList = new List<Geometry.LineParams>();
		try {
			lineList = gameobject.GetComponent<DesignObj>().GetLineInfo(url);
		}
		catch {
			Debug.Log("Fail to get line list from compiler.");
		}
		return lineList;
	}

	static float LineSimilarity(Geometry.LineParams lineEva, Geometry.LineParams lineTar, float refDistance, float lineDirPosRatio = lineDirPosRatio) {
		if (Mathf.Abs(Vector3.Angle(lineEva.direction, lineTar.direction)) > 90f) {
			lineEva.direction = Vector3.zero - lineEva.direction;
		}
		return mathUtils.DirectionSimilarity(lineEva.direction, lineTar.direction) * lineDirPosRatio
			+ Vector3.Distance(lineEva.position, lineTar.position) / refDistance * (1 - lineDirPosRatio);
	}

	List<Geometry.LineParams> SortLines(GameObject gameObject, List<Geometry.LineParams> lineList, Geometry.LineParams targetLine) {
		List<Geometry.LineParams> lineListNew = new List<Geometry.LineParams>();
		List<float> scores = new List<float>();

		float centerDis = Vector3.Distance(targetLine.position, gameObject.transform.position); // For pseudo- normalization
		foreach (Geometry.LineParams currLine in lineList) {
			scores.Add(LineSimilarity(currLine, targetLine, centerDis));
		}

		// Add planes from planeList to planeListNew in the order of score value from small to large
		int[] scoreIdx = Enumerable.Range(0, scores.Count).ToArray<int>();
		Array.Sort<int>(scoreIdx, (i, j) => scores[i].CompareTo(scores[j]));

		Geometry.LineParams tmpLine;
		for (int i = 0; i < scores.Count; i++) {
			tmpLine = lineList[scoreIdx[i]];
			tmpLine.confidence = scores[scoreIdx[i]];
			lineListNew.Add(tmpLine);
		}

		return lineListNew;
	}

	#endregion

	#region Search object point geometry with gesture point geometry
	void GesturePointUpdate() {
		gestureGeo.leftPoint = leftPoint;
		if (gestureGeo.leftPoint.isEmpty != true && gestureGeoPrev.leftPoint.isEmpty == true) {
			gestureGeoInit.leftPoint = leftPoint;
			Debug.Log("Left point is initialized.");
		}
		if (gestureGeo.leftPoint.isEmpty == true && gestureGeoPrev.leftPoint.isEmpty != true) {
			gestureGeoInit.leftPoint.isEmpty = true;
		}

		gestureGeo.rightPoint = rightPoint;
		if (gestureGeo.rightPoint.isEmpty != true && gestureGeoPrev.rightPoint.isEmpty == true) {
			gestureGeoInit.rightPoint = rightPoint;
			Debug.Log("Right point is initialized.");
		}
		if (gestureGeo.rightPoint.isEmpty == true && gestureGeoPrev.rightPoint.isEmpty != true) {
			gestureGeoInit.rightPoint.isEmpty = true;
		}
	}

	List<Geometry.PointParams> GetPointList(GameObject gameobject) {
		List<Geometry.PointParams> pointList = new List<Geometry.PointParams>();
		try {
			pointList = gameobject.GetComponent<DesignObj>().GetPointInfo(url);
		}
		catch {
			Debug.Log("Fail to get point list from compiler.");
		}
		return pointList;
	}

	static float PointSimilarity(Geometry.PointParams pointEva, Geometry.PointParams pointTar, float refDistance) {
		return Vector3.Distance(pointEva.position, pointTar.position) / refDistance;
    }

	List<Geometry.PointParams> SortPoints(GameObject gameObject, List<Geometry.PointParams> pointList, Geometry.PointParams targetPoint) {
		List<Geometry.PointParams> pointListNew = new List<Geometry.PointParams>();
		List<float> scores = new List<float>();

		float centerDis = Vector3.Distance(targetPoint.position, gameObject.transform.position); // For pseudo- normalization
		foreach (Geometry.PointParams currPoint in pointList) {
			scores.Add(PointSimilarity(currPoint, targetPoint, centerDis));
		}

		// Add planes from planeList to planeListNew in the order of score value from small to large
		int[] scoreIdx = Enumerable.Range(0, scores.Count).ToArray<int>();
		Array.Sort<int>(scoreIdx, (i, j) => scores[i].CompareTo(scores[j]));

		Geometry.PointParams tmpPoint;
		for (int i = 0; i < scores.Count; i++) {
			tmpPoint = pointList[scoreIdx[i]];
			tmpPoint.confidence = scores[scoreIdx[i]];
			pointListNew.Add(tmpPoint);
		}

		return pointListNew;
	}

	#endregion

	#region Search object geometry in pairs
	// The smaller the score is, the more similar the two plane pairs are to the target plane pair
	static float PlanePairSimilarity(List<Geometry.PlaneParams> planePairEva, List<Geometry.PlaneParams> planePairTarget, float singlePairRatio = singlePairRatio, float planePairDirPosRatio = planePairDirPosRatio) {
		float score = 0f;

		if (planePairEva[0].position == planePairEva[1].position &&
			planePairEva[0].normalDir == planePairEva[1].normalDir) {
			score = Mathf.Infinity;
			return score;
        }

		score += (1 - planePairDirPosRatio) * mathUtils.VectorSimilarity(planePairEva[0].position - planePairEva[1].position, planePairTarget[0].position - planePairTarget[1].position); // Similarity of two relative vectors between two plane pairs
		score += planePairDirPosRatio * mathUtils.DirectionSimilarity(Quaternion.FromToRotation(planePairEva[0].normalDir, planePairTarget[0].normalDir) * planePairEva[1].normalDir, planePairTarget[1].normalDir); // Rotate eva pair so eva[0] align with tar[0], compare rotated eva[1] with tar[1]
		score = score * (1 - singlePairRatio) + (planePairEva[0].confidence + planePairEva[1].confidence) / 2 * singlePairRatio;

		return score;
	}

	GestureGeo SearchPlanePair(GameObject gameObject, List<Geometry.PlaneParams> planeList, GestureGeo gestureGeo) {
		GestureGeo selectedPlanePair = new GestureGeo();

		List<Geometry.PlaneParams> sortedPlaneListLeft = new List<Geometry.PlaneParams>();
		List<Geometry.PlaneParams> sortedPlaneListRight = new List<Geometry.PlaneParams>();

		if (gestureGeo.leftPlane.isEmpty != true) {
			sortedPlaneListLeft = SortPlanes(gameObject, planeList, gestureGeo.leftPlane);
		}
		else {
			Debug.Log("Fail to access left gesture plane for searching obj plane.");
			return selectedPlanePair;
		}
		if (gestureGeo.rightPlane.isEmpty != true) {
			sortedPlaneListRight = SortPlanes(gameObject, planeList, gestureGeo.rightPlane);
		}
		else {
			Debug.Log("Fail to access right gesture plane for seaching obj plane.");
			return selectedPlanePair;
		}

		// Begin searching and selecting
		List<Geometry.PlaneParams> planePairTarget = new List<Geometry.PlaneParams>(2);
		planePairTarget[0] = gestureGeo.leftPlane;
		planePairTarget[1] = gestureGeo.rightPlane;

		List<float> scores = new List<float>();
		float score;
		List<Geometry.PlaneParams> planePairEva;
		foreach (Vector2Int idx in mathUtils.Permutation(Mathf.Min(planeList.Count, geoSearchPatchSize))) {
			planePairEva = new List<Geometry.PlaneParams>(2);
			planePairEva[0] = sortedPlaneListLeft[idx[0]];
			planePairEva[1] = sortedPlaneListRight[idx[1]];
			score = PlanePairSimilarity(planePairEva, planePairTarget);
			scores.Add(score);
		}

		// Sort plane pair in the order of score value from small to large
		int[] scoreIdx = Enumerable.Range(0, scores.Count).ToArray<int>();
		Array.Sort<int>(scoreIdx, (i, j) => scores[i].CompareTo(scores[j]));

		Vector2Int pairIdx = mathUtils.Permutation(Mathf.Min(planeList.Count, geoSearchPatchSize))[scoreIdx[0]];
		selectedPlanePair.leftPlane = sortedPlaneListLeft[pairIdx[0]];
		selectedPlanePair.rightPlane = sortedPlaneListRight[pairIdx[1]];

		return selectedPlanePair;
	}

	GestureGeo SearchLinePair() {
		GestureGeo selectedLinePair = new GestureGeo();
		//Debug.Log("line-line gesture is detected.");
		//Debug.Log("line distance: " + Vector3.Distance(leftLine.position, rightLine.position).ToString());
		//Debug.Log("line direction diff: " + Vector3.Angle(leftLine.direction, rightLine.direction).ToString());

		return selectedLinePair;
	}

	GestureGeo SearchPointPair() {
		GestureGeo selectedPointPair = new GestureGeo();
		//Debug.Log("point-point gesture is detected.");
		//Debug.Log("point distance: " + Vector3.Distance(leftPoint.position, rightPoint.position).ToString());

		return selectedPointPair;
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
