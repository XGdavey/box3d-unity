using UnityEditor;
using UnityEngine;

namespace Box3D.Hybrid.Editor
{
    /// <summary>Shared drawing helpers for joint inspectors: the common base fields, a conditional
    /// group whose sub-fields appear only when a toggle is on, and a draggable Scene-view handle for
    /// the anchor.</summary>
    public abstract class Box3DJointEditor : UnityEditor.Editor
    {
        /// <summary>Draws a draggable handle for a local-anchor property on a body's Transform.</summary>
        protected void DrawAnchorHandle(string anchorProperty, Transform body, Color color)
        {
            SerializedProperty anchor = serializedObject.FindProperty(anchorProperty);
            Vector3 world = body.TransformPoint(anchor.vector3Value);

            Handles.color = color;
            float size = HandleUtility.GetHandleSize(world) * 0.12f;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Move Joint Anchor");
                anchor.vector3Value = body.InverseTransformPoint(moved);
                serializedObject.ApplyModifiedProperties();
            }
        }

        protected virtual void OnSceneGUI()
        {
            DrawAnchorHandle("Anchor", ((Box3DJoint)target).transform, Color.yellow);
        }

        /// <summary>Draws the connected body, anchor, and collide-connected fields.</summary>
        protected void DrawBase()
        {
            Field("ConnectedBody");
            Field("Anchor");
            Field("CollideConnected");
        }

        protected void Field(string propertyName)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName));
        }

        /// <summary>Draws a bool toggle and, only when it is on, its indented sub-fields.</summary>
        protected void ConditionalField(string toggle, params string[] subFields)
        {
            SerializedProperty toggleProperty = serializedObject.FindProperty(toggle);
            EditorGUILayout.PropertyField(toggleProperty);
            if (!toggleProperty.boolValue) return;

            EditorGUI.indentLevel++;
            foreach (string subField in subFields) Field(subField);
            EditorGUI.indentLevel--;
        }
    }
}
