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
    public List<String> blendshapeNames;
}

[Serializable]
public class FacesResult
{
    public Faces content;
    public string error;
}