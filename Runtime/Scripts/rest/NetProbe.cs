//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class NetProbe : MonoBehaviour
{
    public string url = "http://localhost:6001/faces";

    IEnumerator Start()
    {
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        Debug.Log($"Requested: {url}");
        Debug.Log($"Final URL: {req.url}");
        Debug.Log($"Result: {req.result}");
        Debug.Log($"HTTP {req.responseCode}");
        Debug.Log($"Error: {req.error}");
    }

}