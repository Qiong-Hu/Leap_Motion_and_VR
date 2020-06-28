using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public class canvasCreate : MonoBehaviour {



	// Use this for initialization
	void Start () {
		// For debug
		string url = "http://localhost:5001";
		List<string> nameList = new List<string>();

		nameList = GetObjList(url);

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

}
