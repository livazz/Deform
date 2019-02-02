﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using Deform;

namespace DeformEditor
{
	public class DeformWindow : EditorWindow
	{
		private static List<DeformerAttribute> deformerAttributes;

		private static class Styles
		{
			public const int MAX_LIST_BUTTON_WIDTH = 150;
			public const int PAD_X = 5;
			public const int PAD_Y = 2;

			public static GUIStyle ListButton;

			static Styles ()
			{
				ListButton = new GUIStyle (EditorStyles.toolbarButton);
				ListButton.margin = new RectOffset (PAD_X, PAD_X, PAD_Y, PAD_Y);
			}
		}

		private static class Content
		{
			public static GUIContent CreateDeformable;
			public static GUIContent[] FilterToolbar;

			static Content ()
			{
				CreateDeformable = new GUIContent
				(
					text: "Create Deformable",
					tooltip: "Create a deformable"
				);
				FilterToolbar = new GUIContent[]
				{
					new GUIContent ("A", "All"), new GUIContent ("N", "Normal"), new GUIContent ("M", "Mask"), new GUIContent ("U", "Utility")
				};
			}
		}

		private enum FilterCategory { All, Normal, Mask, Utility }

		[SerializeField]
		private FilterCategory filter;
		[SerializeField]
		private Vector2 scrollPosition;
		[SerializeField]
		private string searchQuery;
		[SerializeField]
		private SearchField searchField;

		[MenuItem ("Window/Deform/Creator", priority = 0)]
		[MenuItem ("Tools/Deform/Creator", priority = 0)]
		public static void ShowWindow ()
		{
			GetWindow<DeformWindow> ("Deform", true);
		}

		[UnityEditor.Callbacks.DidReloadScripts]
		private static void UpdateDeformerAttributes ()
		{
			deformerAttributes = GetAllDeformerAttributes ().OrderBy (x => (int)x.Category).ToList ();
		}

		private void OnEnable ()
		{
			UpdateDeformerAttributes ();

			Undo.undoRedoPerformed += Repaint;
		}

		private void OnDisable ()
		{
			Undo.undoRedoPerformed -= Repaint;
		}

		private void OnGUI ()
		{
			if (searchField == null)
				searchField = new SearchField ();

			EditorGUILayout.Space ();

			if (GUILayout.Button (Content.CreateDeformable, Styles.ListButton))
				AddOrCreateDeformable ();

			EditorGUILayout.Space ();

			using (new EditorGUILayout.HorizontalScope ())
			{
				var categoryCount = Content.FilterToolbar.Length;
				for (int i = 0; i < categoryCount; i++)
				{
					if (GUILayout.Toggle ((int)filter == i, Content.FilterToolbar[i], (i == 0) ? EditorStyles.miniButtonLeft : (i == categoryCount - 1) ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid, GUILayout.MinWidth (0)))
					{
						Undo.RecordObject (this, "Changed Category Filter");
						filter = (FilterCategory)i;
					}
				}
			}

			using (new EditorGUILayout.HorizontalScope ())
			{
				using (var check = new EditorGUI.ChangeCheckScope ())
				{
					var rect = GUILayoutUtility.GetRect (1, 1, 18, 18, GUILayout.ExpandWidth (true));
					rect.width -= Styles.PAD_X * 2;
					rect.x += Styles.PAD_X;
					rect.y += Styles.PAD_Y * 2;

					var newSearchQuery = searchField.OnToolbarGUI (rect, searchQuery);
					if (check.changed)
					{
						Undo.RecordObject (this, "Changed Search Query");
						searchQuery = newSearchQuery;
					}
				}
			}

			using (var scroll = new EditorGUILayout.ScrollViewScope (scrollPosition))
			{
				if (deformerAttributes == null || deformerAttributes.Count == 0)
					EditorGUILayout.LabelField ("No deformers found.", GUILayout.MinWidth (0));
				else
				{
					var filteredDeformerAttributes =
						deformerAttributes.Where (d => AttributeIncludedInFilter (d, filter)).Where (d => string.IsNullOrEmpty (searchQuery) || d.Name.ToLower ().Contains (searchQuery.ToLower ())).ToList ();
					var drawnCount = 0;
					for (int i = 0; i < filteredDeformerAttributes.Count; i++)
					{
						var current = filteredDeformerAttributes[i];

						if (AttributeIncludedInFilter (current, filter))
						{
							if (drawnCount == 0)
								EditorGUILayout.LabelField (current.Category.ToString (), EditorStyles.centeredGreyMiniLabel, GUILayout.MinWidth (0));

							if (GUILayout.Button (new GUIContent (current.Name, current.Description), Styles.ListButton))
								CreateDeformerFromAttribute (current, true);
							drawnCount++;
						}

						if (filter == FilterCategory.All)
						{
							if (i + 1 < filteredDeformerAttributes.Count)
							{
								var next = filteredDeformerAttributes[i + 1];
								if (next.Category != current.Category)
									EditorGUILayout.LabelField (next.Category.ToString (), EditorStyles.centeredGreyMiniLabel, GUILayout.MinWidth (0));
							}
						}
					}

					EditorGUILayout.Space ();
				}
				scrollPosition = scroll.scrollPosition;
			}
		}

		private bool AttributeIncludedInFilter (DeformerAttribute attribute, FilterCategory filter)
		{
			if (filter == FilterCategory.All)
				return true;
			else if (filter == FilterCategory.Normal && attribute.Category == Category.Normal)
				return true;
			else if (filter == FilterCategory.Mask && attribute.Category == Category.Mask)
				return true;
			else if (filter == FilterCategory.Utility && attribute.Category == Category.Utility)
				return true;
			return false;
		}

		public void AddOrCreateDeformable ()
		{
			var targets = Selection.gameObjects;

			// If we don't have any objects selected, create a new Deformable.
			if (targets == null || targets.Length == 0)
				CreateDeformable ();
			else
			{
				// Keep track of whether or not we've actually been able to add a Deformable component.
				var addedComponent = false;
				foreach (var target in Selection.gameObjects)
				{
					// Check if there's already a Deformable/
					var deformable = target.GetComponent<Deformable> ();
					// If there isn't, we can add one
					if (deformable == null)
					{
						Undo.AddComponent<Deformable> (target);
						addedComponent = true;
					}
				}

				// If we never ended up adding a Deformable component, we should create new one.
				if (!addedComponent)
					CreateDeformable ();
			}
		}

		private Deformable CreateDeformable ()
		{
			var newObject = GameObject.CreatePrimitive (PrimitiveType.Sphere);
			newObject.name = "Deformable Object";

			var deformable = newObject.AddComponent<Deformable> ();
			deformable.ChangeMesh (DeformEditorResources.LoadDefaultMesh ());

			DestroyImmediate (newObject.GetComponent<Collider> ());

			Selection.activeGameObject = newObject;

			Undo.RegisterCreatedObjectUndo
			(
				newObject,
				"Created Deformable GameObject"
			);

			return deformable;
		}

		public void CreateDeformerFromAttribute (DeformerAttribute attribute, bool autoAdd)
		{
			var selectedGameObjects = Selection.gameObjects;
			if (selectedGameObjects == null || selectedGameObjects.Length == 0)
			{
				var newGameObject = new GameObject (attribute.Name);
				Undo.RegisterCreatedObjectUndo (newGameObject, "Created Deformer");

				newGameObject.AddComponent (attribute.Type);

				newGameObject.transform.localRotation = Quaternion.Euler (attribute.XRotation, attribute.YRotation, attribute.ZRotation);

				Selection.activeGameObject = newGameObject;
			}
			else
			{
				if (autoAdd)
					Undo.SetCurrentGroupName ("Created and Added Deformer");
				else
					Undo.SetCurrentGroupName ("Created Deformer");

				var newGameObject = new GameObject (attribute.Name);
				Undo.RegisterCreatedObjectUndo (newGameObject, "Created Deformer");

				var newDeformer = newGameObject.AddComponent (attribute.Type) as Deformer;

				if (selectedGameObjects.Length == 1)
				{
					var parent = selectedGameObjects[0].transform;
					newGameObject.transform.SetParent (parent, true);
					newGameObject.transform.position = parent.position;
					newGameObject.transform.rotation = parent.rotation * Quaternion.Euler (attribute.XRotation, attribute.YRotation, attribute.ZRotation);
				}
				else
				{
					var center = GetAverageGameObjectPosition (selectedGameObjects);
					var rotation = Quaternion.Euler (attribute.XRotation, attribute.YRotation, attribute.ZRotation);
					newGameObject.transform.SetPositionAndRotation (center, rotation);
				}

				if (autoAdd)
				{
					var groups = new GroupDeformer[selectedGameObjects.Length];
					var repeaters = new RepeaterDeformer[selectedGameObjects.Length];
					var deformables = new Deformable[selectedGameObjects.Length];

					var groupIndex = 0;
					var repeaterIndex = 0;
					var deformableIndex = 0;

					foreach (var gameObject in selectedGameObjects)
					{
						// if the object we're creating a deformer under has an auto group, there's no need to add the new deformer to anything
						// because the auto group will automatically find it.
						if (gameObject.GetComponent<AutoGroupDeformer> () != null)
							continue;
						// check for repeater deformer
						var repeater = gameObject.GetComponent<RepeaterDeformer> ();
						if (repeater != null)
						{
							repeaters[repeaterIndex] = repeater;
							repeaterIndex++;
						}
							
						// check for group deformer
						var group = gameObject.GetComponent<GroupDeformer> ();
						if (group != null)
						{
							// add group to array of groups if it exists
							groups[groupIndex] = group;
							groupIndex++;
						}
						// if there isn't a group or repeater deformer, check for a deformable
						if (repeater == null || group == null)
						{
							var deformable = gameObject.GetComponent<Deformable> ();
							if (deformable != null)
							{
								// add deformable to array of deformables if it exists
								deformables[deformableIndex] = deformable;
								deformableIndex++;
							}
						}
					}

					if (repeaters!= null && repeaters.Length != 0)
					{
						Array.Resize (ref repeaters, repeaterIndex);
						Undo.RecordObjects (repeaters, "Set Deformer");
						foreach (var repeater in repeaters)
							if (repeater != null)
								repeater.Deformer = newDeformer;
					}

					if (groups != null && groups.Length != 0)
					{
						Array.Resize (ref groups, groupIndex);
						Undo.RecordObjects (groups, "Added Deformer");
						foreach (var group in groups)
							if (group != null)
								group.DeformerElements.Add (new DeformerElement (newDeformer));
					}

					if (deformables != null && deformables.Length != 0)
					{
						Array.Resize (ref deformables, deformableIndex);
						Undo.RecordObjects (deformables, "Added Deformer");
						foreach (var deformable in deformables)
							if (deformable != null)
								deformable.DeformerElements.Add (new DeformerElement (newDeformer));
					}
				}

				Undo.CollapseUndoOperations (Undo.GetCurrentGroup ());
			}
		}

		private Vector3 GetAverageGameObjectPosition (GameObject[] gameObjects)
		{
			if (gameObjects == null || gameObjects.Length == 0)
				return Vector3.zero;

			var sum = Vector3.zero;
			foreach (var gameObject in gameObjects)
				sum += gameObject.transform.position;

			return sum / gameObjects.Length;
		}

		public static IEnumerable<DeformerAttribute> GetAllDeformerAttributes ()
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies ())
			{
				foreach (var type in assembly.GetTypes ())
				{
					if (type.IsSubclassOf (typeof (Deformer)))
					{
						var attribute = type.GetCustomAttribute<DeformerAttribute> (false);
						if (attribute != null)
							yield return attribute;
					}
				}
			}
		}
	}
}