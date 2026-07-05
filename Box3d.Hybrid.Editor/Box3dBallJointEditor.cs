using UnityEditor;

namespace Box3d.Hybrid.Editor
{
    [CustomEditor(typeof(Box3dBallJoint))]
    public class Box3dBallJointEditor : Box3dJointEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawBase();
            Field("Axis");
            ConditionalField("UseConeLimit", "ConeAngle");
            ConditionalField("UseTwistLimit", "MinTwist", "MaxTwist");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
