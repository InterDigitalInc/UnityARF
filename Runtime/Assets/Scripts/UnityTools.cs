using UnityEngine;
using UnityEngine.Rendering;

class UnityTools
{
    public static string GetRendererType()
    {    
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null)
        {
            return "Built-in";
        }
        else if (pipeline.GetType().ToString().Contains("Universal"))
        {
            return "URP";
        }
        else if (pipeline.GetType().ToString().Contains("HDRender"))
        {
            return "HDRP";
        }
        else
        {
            return "Custom";
        }
    }

    public static Shader GetStandardShader()
    {
        var rendererType = GetRendererType();
        if (rendererType == "Built-in") {
            return Shader.Find("Standard");
        }
        else if (rendererType == "URP") {
            return Shader.Find("Universal Render Pipeline/Lit");
        }
        else if (rendererType == "HDRP") {
            return Shader.Find("HDRP/Lit");
        }
        else {
            return null;
        }
    }

}