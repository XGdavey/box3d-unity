using UnityEditor;

namespace Box3d.Hybrid.Editor
{
    [CustomEditor(typeof(Box3dHingeJoint))]
    public class Box3dHingeJointEditor : Box3dJointEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawBase();
            Field("Axis");
            ConditionalField("UseLimits", "MinAngle", "MaxAngle");
            ConditionalField("UseMotor", "MotorSpeed", "MaxMotorTorque");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
