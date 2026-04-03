//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using System.Collections.Generic;

[Serializable]
public class Blendshapes
{
    public List<float> weights;
}


[Serializable]
public class Face
{
    public List<List<float>> landmarks;
    public Blendshapes blendshapes;
}


[Serializable]
public class Faces
{
    public List<Face> faces;
    public List<String> blendshapesName;
}

[Serializable]
public class FacesResult
{
    public Faces content;
    public string error;
}