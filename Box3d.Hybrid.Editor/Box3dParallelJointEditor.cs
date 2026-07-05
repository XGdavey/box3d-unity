using UnityEditor;

namespace Box3d.Hybrid.Editor
{
    [CustomEditor(typeof(Box3dParallelJoint))]
    public class Box3dParallelJointEditor : Box3dJointEditor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); // no conditional fields; inherits the anchor handle
        }
    }
}
