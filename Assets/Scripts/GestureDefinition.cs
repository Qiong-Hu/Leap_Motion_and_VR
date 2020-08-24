using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Windows;
using Leap;
using FARVR.Design;

namespace GestureDefinition {
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

		/// <summary>
		/// Returns "handPosition", "handRotation", "colliderName", "contactPosition"
		/// </summary>
		public Dictionary<string, dynamic> Grab() {
			Dictionary<string, dynamic> grabParams = new Dictionary<string, dynamic>();

			try {
				grabParams["handPosition"] = GameObject.Find(handPolarity[0] + "_Palm").transform.position;
				grabParams["handRotation"] = GameObject.Find(handPolarity[0] + "_Palm").transform.eulerAngles;
			}
			catch {
				Debug.Log("Fail to find " + handPolarity.ToLower() + " palm.");
				return null;
			}
			try {
				// Only detect collision between palm and object for now (finger colliders exist, unused)
				GameObject palmCollider = GameObject.Find(handPolarity[0] + "_Palm/palm");
				string colliderName = palmCollider.GetComponent<handCollisionManagement>().ColliderName;
				grabParams["colliderName"] = colliderName;
				grabParams["contactPosition"] = palmCollider.GetComponent<handCollisionManagement>().ContactPosition;
			}
			catch {
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
			planeParams["normalDir"] = new Vector3(currHand.PalmNormal.x, currHand.PalmNormal.y, currHand.PalmNormal.z);

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
			foreach (string key in dict2.Keys) {
				if (!CompareArrayList(dict1[key], dict2[key])) {
					flag = false;
					break;
				}
			}
			return flag;
		}

	}
}