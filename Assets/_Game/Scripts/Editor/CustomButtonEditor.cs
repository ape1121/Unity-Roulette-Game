using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(UIButton))]
[CanEditMultipleObjects]
public class CustomButtonEditor : ButtonEditor
{
	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		SerializedProperty prop = serializedObject.GetIterator();
		bool enterChildren = true;
		while (prop.NextVisible(enterChildren))
		{
			using (new EditorGUI.DisabledScope(prop.name == "m_Script"))
			{
				EditorGUILayout.PropertyField(prop, true);
			}
			enterChildren = false;
		}

		serializedObject.ApplyModifiedProperties();
	}
}