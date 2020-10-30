using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Windows;
using Leap;
using FARVR.Design;
using FARVR.MathUtils;
using FARVR.GeoParams;

namespace FARVR.GestureDefinition {
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
				if (mathUtils.CompareDict(gesture_param, gesture_param_list[i])) {
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
			}
			catch {
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
				}
				else {
					currButton.ChangeColor("normal");
				}
			}

			// Step 4. send button name to CallCompiler (in Update)
			return selectedButtonName;
		}

		// Pass params of grab to main Update()
		public struct GrabParams {
			public Vector3 handPosition;
			public Vector3 handRotation;
			public string colliderName;
			public Vector3 contactPosition;
			public bool isEmpty;
        }

		/// <summary>
		/// Returns "handPosition", "handRotation", "colliderName", "contactPosition"
		/// </summary>
		public GrabParams Grab() {
			GrabParams grabParams = new GrabParams();
			grabParams.isEmpty = true;

			try {
				grabParams.handPosition = GameObject.Find(handPolarity[0] + "_Palm").transform.position;
				grabParams.handRotation = GameObject.Find(handPolarity[0] + "_Palm").transform.eulerAngles;
				grabParams.isEmpty = false;
			}
			catch {
				Debug.Log("Fail to find " + handPolarity.ToLower() + " palm.");
			}
			try {
				// Only detect collision between palm and object for now (finger colliders exist, unused)
				GameObject palmCollider = GameObject.Find(handPolarity[0] + "_Palm/palm");
				string colliderName = palmCollider.GetComponent<handCollisionManagement>().ColliderName;
				grabParams.colliderName = colliderName;
				grabParams.contactPosition = palmCollider.GetComponent<handCollisionManagement>().ContactPosition;
				grabParams.isEmpty = false;
			}
			catch {
				Debug.Log("Fail to find " + handPolarity.ToLower() + " palm collider.");
			}

			return grabParams;
		}

		// Pass params of select to main Update()
		public struct SelectParams {
			public Vector3 fingertipPos;
			public Vector3 fingerbasePos;
			public bool isEmpty;
        }

		/// <summary>
		/// Returns "fingertipPos", "fingerbasePos"
		/// </summary>
		public SelectParams Select() {
			SelectParams selectParams = new SelectParams();
			selectParams.isEmpty = true;

			try {
				selectParams.fingertipPos = GameObject.Find(handPolarity[0] + "_index_end").transform.position;
				selectParams.isEmpty = false;
			}
			catch {
				Debug.Log("Fail to find " + handPolarity.ToLower() + " fingertip position.");
			}
			try {
				selectParams.fingerbasePos = GameObject.Find(handPolarity[0] + "_index_a").transform.position;
				selectParams.isEmpty = false;
			}
			catch {
				Debug.Log("Fail to find " + handPolarity.ToLower() + " fingerbase position.");
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

		/// <summary>
		/// Returns "position", "forwardDir", "normalDir"
		/// </summary>
		public Geometry.PlaneParams FindPlaneParams() {
			Geometry.PlaneParams planeParams = new Geometry.PlaneParams();
			planeParams.isEmpty = true;
			planeParams.name = handPolarity + " hand plane";

			try {
				planeParams.position = GameObject.Find(handPolarity[0] + "_Palm").transform.position;
				planeParams.forwardDir = new Vector3(currHand.Direction.x, currHand.Direction.y, currHand.Direction.z);
				planeParams.normalDir = new Vector3(currHand.PalmNormal.x, currHand.PalmNormal.y, currHand.PalmNormal.z);
				planeParams.isEmpty = false;
			}
			catch {
				Debug.Log("Fail to find " + handPolarity.ToLower() + " palm position.");
			}

			return planeParams;
		}

		/// <summary>
		/// Returns "position", "direction"
		/// </summary>
		public Geometry.LineParams FindLineParams() {
			Geometry.LineParams lineParams = new Geometry.LineParams();
			lineParams.isEmpty = true;
			lineParams.name = handPolarity + " hand line";

			Vector3 fingertipPos = new Vector3();
			try {
				fingertipPos = GameObject.Find(handPolarity[0] + "_index_end").transform.position;
			}
			catch {
				Debug.Log("Fail to find " + handPolarity.ToLower() + " fingertip position.");
				return lineParams;
			}

			Vector3 fingerbasePos = new Vector3();
			try {
				fingerbasePos = GameObject.Find(handPolarity[0] + "_index_a").transform.position;
			}
			catch {
				Debug.Log("Fail to find " + handPolarity.ToLower() + " fingerbase position.");
				return lineParams;
			}

			lineParams.position = fingerbasePos;
			lineParams.direction = (fingertipPos - fingerbasePos).normalized;
			lineParams.isEmpty = false;

			return lineParams;
		}

		/// <summary>
		/// Returns "position"
		/// </summary>
		public Geometry.PointParams FindPointParams() {
			Geometry.PointParams pointParams = new Geometry.PointParams();
			pointParams.isEmpty = true;
			pointParams.name = handPolarity + " hand point";

			Vector3 indexTipPos = new Vector3();
			try {
				indexTipPos = GameObject.Find(handPolarity[0] + "_index_end").transform.position;
			}
			catch {
				Debug.Log("Fail to find " + handPolarity.ToLower() + " index fingertip position.");
				return pointParams;
			}

			Vector3 thumbTipPos = new Vector3();
			try {
				thumbTipPos = GameObject.Find(handPolarity[0] + "_thumb_end").transform.position;
			}
			catch {
				Debug.Log("Fail to find " + handPolarity.ToLower() + " thumb fingertip position.");
				return pointParams;
			}

			pointParams.position = (indexTipPos + thumbTipPos) / 2;
			pointParams.isEmpty = false;

			return pointParams;
		}

		// Pass params of tune to main Update()
		public struct TuneParams {
			public float palmAngle;
			public float thumbAngle;
			public bool isEmpty;
        }

		// Edit tune discrete param => TODO: need improvement
		public TuneParams TuneDiscrete() {
			TuneParams tuneParams = new TuneParams();
			tuneParams.isEmpty = true;

			// Palm rotation (in euler angle), angle to upwards in world coordinate
			Quaternion palmRot = new Quaternion(currHand.Rotation.x, currHand.Rotation.y, currHand.Rotation.z, currHand.Rotation.w);
			float angleUp = palmRot.eulerAngles.z;
			if (angleUp > 180f) {
				angleUp = 360f - angleUp;
			}
			//Debug.Log(angleUp);
			tuneParams.palmAngle = angleUp;
			tuneParams.isEmpty = false;

			// Thumb direction, angle to upwards in world coordinate
			try {
				Vector3 thumbDir = GameObject.Find(handPolarity[0] + "_thumb_end").transform.position -
					GameObject.Find(handPolarity[0] + "_Palm").transform.position;

				tuneParams.thumbAngle = Vector3.Angle(Vector3.up, thumbDir);
			}
			catch {
				Debug.Log("Fail to find " + handPolarity.ToLower() + " thumb position");
			}

			return tuneParams;
		}

		#endregion
	}

}