using Interdigital.Arf;
using UnityEditor;

[CustomEditor(typeof(GaussianModel))]
public class GaussianModelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var model = (GaussianModel)target;

        if (model.shs == null)
        {
            EditorGUILayout.HelpBox("No Gaussian data loaded.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Gaussians", model.count.ToString());
        EditorGUILayout.LabelField("SH degree", (model.shs.Length - 1).ToString());
    }
}