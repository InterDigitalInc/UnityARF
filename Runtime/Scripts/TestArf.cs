//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using UnityEngine;

namespace Interdigital.Arf {

public class TestArf : MonoBehaviour
{
    public string filePath;
    public ArfParserOptions options = new ArfParserOptions();

    void Start()
    {       
        Console.SetOut(new UnityTools.LogTextWriter());
        ArfParser.LoadAvatar(filePath, options); 
    }
}

public class ConsoleRedirect : MonoBehaviour
{
}


}
