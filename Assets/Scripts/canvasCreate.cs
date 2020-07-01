using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class canvasCreate : MonoBehaviour {

	public string url = "http://localhost:5001";
		
	List<string> nameList = new List<string>();


	// Use this for initialization
	void Start () {
		nameList = GetObjList(url);
		ShowList(nameList);
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	// Get the name list of all available design objects from the compiler
	List<string> GetObjList(string url) {
		List<string> nameList = new List<string>();

		string htmlText = "";
		using (UnityWebRequest www = UnityWebRequest.Get(url)) {
			www.SendWebRequest();
			while (!www.isDone) ;

			if (www.isNetworkError || www.isHttpError) {
				Debug.Log(www.error);
			}
			else {
				htmlText = www.downloadHandler.text;
			}
		}

		string pattern = @"(?is)<a[^>]*?href=(['""\s]?)(?<name>[^'""\s]*)\1[^>]*?>";
		Regex reg = new Regex(pattern);
		MatchCollection ms = reg.Matches(htmlText);
		foreach (Match m in ms) {
			nameList.Add(m.Groups["name"].Value);
		}

		return nameList;
	}

	// Create a list of buttons on canvas to show the available design objects
	void ShowList(List<string> nameList) {
		int count = 1;

		foreach (string name in nameList) {
			NameButton nameButton = new NameButton();
			nameButton.Initialize(name, count);
			count++;
			

			break;
		}
	}

}

public class NameButton {

	public GameObject button;
	public RectTransform rectTransform;

	private GameObject canvas = GameObject.Find("Canvas");

	public void Initialize(string name, int count) {
		button = new GameObject(name, typeof(Button), typeof(RectTransform), typeof(Image));
		Display(name, count);
	}

	// Display the button object
	private void Display(string name, int count) {
		// Create button object
		button.transform.SetParent(canvas.transform);
		button.transform.localRotation = Quaternion.identity;
		button.transform.localScale = Vector3.one * 0.01f;

		// Set button position
		rectTransform = button.GetComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0, 1);
		rectTransform.anchorMax = new Vector2(0, 1);
		rectTransform.pivot = new Vector2(0.5f, 0.5f);
		rectTransform.sizeDelta = new Vector2(1600, 250);
		rectTransform.anchoredPosition3D = new Vector3(0, -4 * count, 0);

		// Add text to button
		GameObject textObj = new GameObject("Text", typeof(Text));
		textObj.transform.SetParent(button.transform);
		textObj.transform.localRotation = Quaternion.identity;
		textObj.transform.localScale = Vector3.one;

		// Set text position
		RectTransform textRectTransform = textObj.GetComponent<RectTransform>();
		textRectTransform.anchorMin = new Vector2(0, 0);
		textRectTransform.anchorMax = new Vector2(1, 1);
		textRectTransform.pivot = new Vector2(0.5f, 0.5f);
		textRectTransform.offsetMin = Vector2.zero;
		textRectTransform.offsetMax = Vector2.zero;
		textRectTransform.anchoredPosition3D = Vector3.zero;

		// Set text style
		Text text = textObj.GetComponent<Text>();
		text.text = name;
		text.font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
		text.fontSize = 200;
		text.color = new Color32(50, 50, 50, 255);
		text.alignment = TextAnchor.MiddleCenter;
	}

	// Get button global position range
	// pos = "topLeft", "topright", "bottomLeft", "bottomRight", "center"
	public Vector3 GetPosition(string pos) {
		Vector3 centerPos = button.transform.position;
		Vector3 rightRange = Vector3.Scale(Vector3.Scale(
			button.transform.right * rectTransform.sizeDelta[0] / 2,
			button.transform.localScale), canvas.transform.localScale);
		Vector3 upRange = Vector3.Scale(Vector3.Scale(
			button.transform.up * rectTransform.sizeDelta[1] / 2,
			button.transform.localScale), canvas.transform.localScale);

		if (pos == "topLeft" || pos == "A") {
			return centerPos - rightRange + upRange;
        } else if (pos == "topRight" || pos == "B") {
			return centerPos + rightRange + upRange;
        } else if (pos == "bottomRight" || pos == "C") {
			return centerPos + rightRange - upRange;
        } else if (pos == "bottomLeft" || pos == "D") {
			return centerPos - rightRange - upRange;
        } else if (pos == "center") {
			return centerPos;
        } else {
			Debug.Log("GetPosition function fail to recognize request");
			return centerPos;
        }

	}

	// Calculate the vertical distance between a point and the button/canvas plane
	public float VerticalDis(Vector3 point) {
		return Mathf.Abs(Vector3.Dot(point - GetPosition("center"), button.transform.forward));
    }

	// Detect whether projection of a point on canvas is within the range of current button rectangle
	public bool WithinRange(Vector3 point) {
		Vector3 proj = Vector3.ProjectOnPlane(point - GetPosition("center"), button.transform.forward) + GetPosition("center");

		// (ABxAE)*(CDxCE)>=0 and (DAxDE)*(BCxBE)>=0
		float withinHeight = Vector3.Dot(Vector3.Cross(GetPosition("B") - GetPosition("A"), proj - GetPosition("A")),
			Vector3.Cross(GetPosition("D") - GetPosition("C"), proj - GetPosition("C")));
		float withinWidth = Vector3.Dot(Vector3.Cross(GetPosition("A") - GetPosition("D"), proj - GetPosition("D")),
			Vector3.Cross(GetPosition("C") - GetPosition("B"), proj - GetPosition("B")));

		if (withinHeight >= 0 && withinWidth >= 0) {
			return true;
        } else {
			return false;
        }
    }

}