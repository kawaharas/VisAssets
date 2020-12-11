using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
 
public class WebRequestExample : MonoBehaviour
{
	// Where to send our request
//	const string DEFAULT_URL = "https://jsonplaceholder.typicode.com/todos/1";
	const string DEFAULT_URL = "file://C:/Users/kawahara/Downloads/example/model.ctl";
	string targetUrl = DEFAULT_URL;

	// Keep track of what we got back
	string recentData = "";

	void Awake()
	{
//		this.StartCoroutine(this.RequestRoutine(this.targetUrl, this.ResponseCallback));
	}

	// Web requests are typially done asynchronously, so Unity's web request system
	// returns a yield instruction while it waits for the response.
	//
	private IEnumerator RequestRoutine(string url, Action<string> callback = null)
	{
		// Using the static constructor
		var request = UnityWebRequest.Get(url);

		// Wait for the response and then get our data
		yield return request.SendWebRequest();
		var data = request.downloadHandler.text;

		// This isn't required, but I prefer to pass in a callback so that I can
		// act on the response data outside of this function
		if (callback != null)
		{
			callback(data);
		}
	}

	// Callback to act on our response data
	private void ResponseCallback(string data)
	{
		Debug.Log(data);
		recentData = data;
	}

	// Old fashioned GUI system to show the example
	void OnGUI()
	{
		this.targetUrl = GUI.TextArea(new Rect(0, 0, 500, 100), this.targetUrl);
		GUI.TextArea(new Rect(0, 100, 500, 300), this.recentData);
		if (GUI.Button(new Rect(0, 400, 500, 100), "Resend Request"))
		{
			this.StartCoroutine(this.RequestRoutine(targetUrl, this.ResponseCallback));
		}
	}
}
