//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using UnityEngine;

public class IPTextBox : MonoBehaviour
{
    private string ip = "127.0.0.1:6001";
    private RestClient restClient;
    
    void Awake()
    {
        restClient = GetComponent<RestClient>();
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20), "IP:");
        ip = GUI.TextField(new Rect(10, 30, 200, 25), ip, 50);

        if (GUI.Button(new Rect(10, 60, 100, 25), "OK"))
        {
            restClient.uri = $"http://{ip}/faces";
            Debug.Log(restClient.uri);
            restClient.enabled = false;
            restClient.enabled = true;
        }
    }
}
