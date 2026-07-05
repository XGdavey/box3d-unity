using UnityEditor;

namespace Box3d.Hybrid.Editor
{
    [CustomEditor(typeof(Box3dDistanceJoint))]
    public class Box3dDistanceJointEditor : Box3dJointEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawBase();
            Field("ConnectedAnchor");
            Field("Length");
            ConditionalField("UseSpring", "Hertz", "DampingRatio");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
