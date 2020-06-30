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
			// Create button object
			GameObject button = new GameObject(name, typeof(Button), typeof(RectTransform), typeof(Image));
			button.transform.SetParent(this.transform);
			button.transform.localRotation = Quaternion.identity;
			button.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

			// Set button position
			RectTransform rectTransform = button.GetComponent<RectTransform>();
			rectTransform.anchorMin = new Vector2(0, 1);
			rectTransform.anchorMax = new Vector2(0, 1);
			rectTransform.pivot = new Vector2(0.5f, 0.5f);
			rectTransform.sizeDelta = new Vector2(1600, 250);
			rectTransform.anchoredPosition3D = new Vector3(0, -4*count, 0);
			count++;

			// Add text to button
			GameObject textObj = new GameObject("Text", typeof(Text));
			textObj.transform.SetParent(button.transform);
			textObj.transform.localRotation = Quaternion.identity;
			textObj.transform.localScale = new Vector3(1, 1, 1);

			// Set text position
			RectTransform textRectTransform = textObj.GetComponent<RectTransform>();
			textRectTransform.anchorMin = new Vector2(0, 0);
			textRectTransform.anchorMax = new Vector2(1, 1);
			textRectTransform.pivot = new Vector2(0.5f, 0.5f);
			textRectTransform.offsetMin = new Vector2(0, 0);
			textRectTransform.offsetMax = new Vector2(0, 0);
			textRectTransform.anchoredPosition3D = new Vector3(0, 0, 0);

			// Set text style
			Text text = textObj.GetComponent<Text>();
			text.text = name;
			text.font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
			text.fontSize = 200;
			text.color = new Color32(50, 50, 50, 255);
			text.alignment = TextAnchor.MiddleCenter;

		}
	}

}
