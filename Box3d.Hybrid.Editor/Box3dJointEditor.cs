using UnityEditor;

namespace Box3d.Hybrid.Editor
{
    /// <summary>Shared drawing helpers for joint inspectors: the common base fields and a
    /// conditional group whose sub-fields appear only when a toggle is on.</summary>
    public abstract class Box3dJointEditor : UnityEditor.Editor
    {
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
