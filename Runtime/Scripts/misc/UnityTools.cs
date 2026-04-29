//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

public class UnityTools
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

    public static string HashBytes(byte[] data)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data);

        // Convert to hex string
        var sb = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash)
            sb.AppendFormat("{0:x2}", b);

        return sb.ToString();
    }

    public class LogTextWriter : TextWriter
    {
        public override void WriteLine(string value)
        {
            Debug.Log(value);
        }

        public override Encoding Encoding => Encoding.UTF8;
    }

}