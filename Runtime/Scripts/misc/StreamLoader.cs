using System;
using System.IO;
using UnityEngine;

public static class StreamLoader
{
    public static Stream OpenRead(string path)
    {
        const string resourcesPrefix = "Assets/Resources/";
        if (path.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string resourcePath = path.Substring(resourcesPrefix.Length);
            resourcePath = Path.ChangeExtension(resourcePath, null);
            TextAsset ta = Resources.Load<TextAsset>(resourcePath);
            if (ta == null)
                throw new FileNotFoundException($"Resource not found: {resourcePath}");
            return new MemoryStream(ta.bytes, writable: false);
        }

        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
    }
}
