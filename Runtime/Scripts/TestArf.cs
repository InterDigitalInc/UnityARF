//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Interdigital.Arf {

public class TestArf : MonoBehaviour
{
    public string filePath;
    public string lodName = "high_quality";
    public bool loadBlendshapes = true;
    public bool loadSkeletons = true;
    public bool loadSkins = true;

    void Start()
    {       
        Console.SetOut(new UnityTools.LogTextWriter());
        ArfParser arf = ArfParser.Load(filePath); 
        
        GameObject avatar = arf.createAvatar(
            transform, lodName,
            loadBlendshapes: loadBlendshapes,
            loadSkeletons: loadSkeletons,
            loadSkins: loadSkins
        );

        arf.Close();
    }
}

public class ConsoleRedirect : MonoBehaviour
{
}


}
