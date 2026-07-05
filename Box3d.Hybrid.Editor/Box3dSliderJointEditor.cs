using UnityEditor;

namespace Box3d.Hybrid.Editor
{
    [CustomEditor(typeof(Box3dSliderJoint))]
    public class Box3dSliderJointEditor : Box3dJointEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawBase();
            Field("Axis");
            ConditionalField("UseLimits", "MinTranslation", "MaxTranslation");
            ConditionalField("UseMotor", "MotorSpeed", "MaxMotorForce");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
