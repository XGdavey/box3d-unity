using UnityEditor;

namespace Box3d.Hybrid.Editor
{
    [CustomEditor(typeof(Box3dFixedJoint))]
    public class Box3dFixedJointEditor : Box3dJointEditor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); // no conditional fields; inherits the anchor handle
        }
    }
}
