using System.Collections;
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
			foreach (Hand hand in hands) {
				// Grab
				if (hand.GrabStrength == 1) {
					Debug.Log("Gesture grab detected");
                }
				// Palm
				if (hand.GrabStrength == 0) {
					Debug.Log("Gesture palm detected");
				}
				// Gun (IsExtend = T, T, F, F, F)
				if (hand.Fingers.Count == 5) {
					List<Finger> fingers = hand.Fingers;
					bool gesture_gun_flag = true;
					foreach (Finger finger in fingers) {
						if (finger.Type == Finger.FingerType.TYPE_THUMB) {
							if (finger.IsExtended != true) { gesture_gun_flag = false; }}
						if (finger.Type == Finger.FingerType.TYPE_INDEX) {
							if (finger.IsExtended != true) { gesture_gun_flag = false; }}
						if (finger.Type == Finger.FingerType.TYPE_MIDDLE) {
							if (finger.IsExtended != false) { gesture_gun_flag = false; }}
						if (finger.Type == Finger.FingerType.TYPE_RING) {
							if (finger.IsExtended != false) { gesture_gun_flag = false; }}
						if (finger.Type == Finger.FingerType.TYPE_PINKY) {
							if (finger.IsExtended != false) { gesture_gun_flag = false; }}
					}
					if (gesture_gun_flag == true) {
						Debug.Log("Gesture gun detected.");
                    }
                }
				// OK (PinchStrength = 1, IssExtend = _, _, T, T, T)
				if (hand.Fingers.Count == 5) {
					List<Finger> fingers = hand.Fingers;
					bool gesture_ok_flag = true;
					if (hand.PinchStrength != 1) { gesture_ok_flag = false; }
					foreach (Finger finger in fingers) {
						if (finger.Type == Finger.FingerType.TYPE_MIDDLE) {
							if (finger.IsExtended != true) { gesture_ok_flag = false; }}
						if (finger.Type == Finger.FingerType.TYPE_RING) {
							if (finger.IsExtended != true) { gesture_ok_flag = false; }}
						if (finger.Type == Finger.FingerType.TYPE_PINKY) {
							if (finger.IsExtended != true) { gesture_ok_flag = false; }}
                    }
					if (gesture_ok_flag == true) {
						Debug.Log("Gesture OK detected.");
                    }
				}
				// point (IsExtend = F, T, F, F, F)
				if (hand.Fingers.Count == 5)
				{
					List<Finger> fingers = hand.Fingers;
					bool gesture_point_flag = true;
					foreach (Finger finger in fingers)
					{
						if (finger.Type == Finger.FingerType.TYPE_THUMB) {
							if (finger.IsExtended != false) { gesture_point_flag = false; }}
						if (finger.Type == Finger.FingerType.TYPE_INDEX) {
							if (finger.IsExtended != true) { gesture_point_flag = false; }}
						if (finger.Type == Finger.FingerType.TYPE_MIDDLE) {
							if (finger.IsExtended != false) { gesture_point_flag = false; }}
						if (finger.Type == Finger.FingerType.TYPE_RING) {
							if (finger.IsExtended != false) { gesture_point_flag = false; }}
						if (finger.Type == Finger.FingerType.TYPE_PINKY) {
							if (finger.IsExtended != false) { gesture_point_flag = false; }}
					}
					if (gesture_point_flag == true)
					{
						Debug.Log("Gesture point detected.");
					}
				}
			}
        }


    }
}

public class Gesture {
	enum GestureType {
		Gesture_Grab,
		Gesture_Palm,
		Gesture_Gun, 
		Gesture_OK,
		Gesture_Point
    }
}
