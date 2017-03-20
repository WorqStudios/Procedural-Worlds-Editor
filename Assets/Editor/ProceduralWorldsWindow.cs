﻿// #define		DEBUG_GRAPH

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using PW;
using UnityStandardAssets.ImageEffects;

public class ProceduralWorldsWindow : EditorWindow {

    private static Texture2D	backgroundTex;
	private static Texture2D	resizeHandleTex;
	private static Texture2D	selectorBackgroundTex;
	private static Texture2D	debugTexture1;
	private static Texture2D	selectorCaseBackgroundTex;
	private static Texture2D	selectorCaseTitleBackgroundTex;

	private static Texture2D	preset2DSideViewTexture;
	private static Texture2D	preset2DTopDownViewTexture;
	private static Texture2D	preset3DPlaneTexture;
	private static Texture2D	preset3DSphericalTexture;
	private static Texture2D	preset3DCubicTexture;
	private static Texture2D	preset1DDensityFieldTexture;
	private static Texture2D	preset2DDensityFieldTexture;
	private static Texture2D	preset3DDensityFieldTexture;
	private static Texture2D	presetMeshTetxure;
	
	static GUIStyle	whiteText;
	static GUIStyle	whiteBoldText;
	static GUIStyle	splittedPanel;
	static GUIStyle	nodeGraphWidowStyle;

	int					currentPickerWindow;
	int					mouseAboveNodeIndex;
	Vector2				lastMousePosition;

	Camera				previewCamera;
	GameObject			previewSceneRoot;
	GameObject			previewCameraObject;
	GameObject			previewTerrainObject;
	RenderTexture		previewCameraRenderTexture;

	[SerializeField]
	public PWNodeGraph	currentGraph;

	[System.SerializableAttribute]
	private class PWNodeStorage
	{
		public string		name;
		public System.Type	nodeType;
		
		public PWNodeStorage(string n, System.Type type)
		{
			name = n;
			nodeType = type;
		}
	}

	[System.NonSerializedAttribute]
	Dictionary< string, List< PWNodeStorage > > nodeSelectorList = new Dictionary< string, List< PWNodeStorage > >()
	{
		{"Simple values", new List< PWNodeStorage >()},
		{"Operations", new List< PWNodeStorage >()},
		{"Noises", new List< PWNodeStorage >()},
		{"Noise masks", new List< PWNodeStorage >()},
		{"Storage", new List< PWNodeStorage >()},
		{"Visual", new List< PWNodeStorage >()},
		{"Debug", new List< PWNodeStorage >()},
		{"Custom", new List< PWNodeStorage >()},
	};

	[System.NonSerializedAttribute]
	Dictionary< string, Dictionary< string, FieldInfo > > bakedNodeFields = new Dictionary< string, Dictionary< string, FieldInfo > >();

	[MenuItem("Window/Procedural Worlds")]
	static void Init()
	{
		ProceduralWorldsWindow window = (ProceduralWorldsWindow)EditorWindow.GetWindow (typeof (ProceduralWorldsWindow));

		window.Show();
	}
	
	void AddToSelector(string key, params object[] objs)
	{
		if (nodeSelectorList.ContainsKey(key))
		{
			for (int i = 0; i < objs.Length; i += 2)
			nodeSelectorList[key].Add(new PWNodeStorage((string)objs[i], (Type)objs[i + 1]));
		}
	}

	void InitializeNewGraph(PWNodeGraph graph)
	{
		//setup splitted panels:
		graph.h1 = new HorizontalSplitView(resizeHandleTex, position.width * 0.85f, position.width / 2, position.width - 4);
		graph.h2 = new HorizontalSplitView(resizeHandleTex, position.width * .25f, 0, position.width / 2);

		graph.graphDecalPosition = Vector2.zero;

		graph.presetChoosed = false;
		
		graph.localWindowIdCount = 0;
		
		graph.outputNode = ScriptableObject.CreateInstance< PWNodeGraphOutput >();
		graph.outputNode.SetWindowId(currentGraph.localWindowIdCount++);
		graph.outputNode.windowRect.position = new Vector2(position.width - 100, (int)(position.height / 2));

		graph.inputNode = ScriptableObject.CreateInstance< PWNodeGraphInput >();
		graph.inputNode.SetWindowId(currentGraph.localWindowIdCount++);
		graph.inputNode.windowRect.position = new Vector2(50, (int)(position.height / 2));

		graph.firstInitialization = "initialized";

		graph.saveName = null;
		graph.name = "New ProceduralWorld";
	}

	void BakeNode(Type t)
	{
		var dico = new Dictionary< string, FieldInfo >();
		bakedNodeFields[t.AssemblyQualifiedName] = dico;

		foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
			dico[field.Name] = field;
	}

	void OnEnable()
	{
		CreateBackgroundTexture();
		
		splittedPanel = new GUIStyle();
		splittedPanel.margin = new RectOffset(5, 0, 0, 0);

		nodeGraphWidowStyle = new GUIStyle();
		nodeGraphWidowStyle.normal.background = backgroundTex;

		//setup nodeList:
		foreach (var n in nodeSelectorList)
			n.Value.Clear();
		AddToSelector("Simple values", "Slider", typeof(PWNodeSlider));
		AddToSelector("Operations", "Add", typeof(PWNodeAdd));
		AddToSelector("Debug", "DebugLog", typeof(PWNodeDebugLog));
		AddToSelector("Noise masks", "Circle Noise Mask", typeof(PWNodeCircleNoiseMask));

		//bake the fieldInfo types:
		bakedNodeFields.Clear();
		foreach (var nodeCat in nodeSelectorList)
			foreach (var nodeClass in nodeCat.Value)
				BakeNode(nodeClass.nodeType);
		BakeNode(typeof(PWNodeGraphOutput));
		BakeNode(typeof(PWNodeGraphInput));
		
		if (currentGraph == null)
			currentGraph = ScriptableObject.CreateInstance< PWNodeGraph >();
			
		//clear the corrupted node:
		for (int i = 0; i < currentGraph.nodes.Count; i++)
			if (currentGraph.nodes[i] == null)
				DeleteNode(i--);
	}

    void OnGUI()
    {
		//initialize graph the first time he was created
		//function is in OnGUI cause in OnEnable, the position values are bad.
		if (currentGraph.firstInitialization == null)
			InitializeNewGraph(currentGraph);
			
		//text colors:
		whiteText = new GUIStyle();
		whiteText.normal.textColor = Color.white;
		whiteBoldText = new GUIStyle();
		whiteBoldText.fontStyle = FontStyle.Bold;
		whiteBoldText.normal.textColor = Color.white;
		
		if (currentGraph.firstInitialization != "initialized")
			return ;

		if (!currentGraph.presetChoosed)
		{
			DrawPresetPanel();
			return ;
		}

		EditorUtility.SetDirty(this);

		//esc key event:
		if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
		{
			if (currentGraph.draggingLink)
				currentGraph.draggingLink = false;
		}

        //background color:
		if (backgroundTex == null || currentGraph.h1 == null || resizeHandleTex == null)
			OnEnable();
		GUI.DrawTexture(new Rect(0, 0, maxSize.x, maxSize.y), backgroundTex, ScaleMode.StretchToFill);

		ProcessPreviewScene();

		DrawNodeGraphCore();

		currentGraph.h1.UpdateMinMax(position.width / 2, position.width - 4);
		currentGraph.h2.UpdateMinMax(0, position.width / 2);

		currentGraph.h1.Begin();
		Rect p1 = currentGraph.h2.Begin(backgroundTex);
		DrawLeftBar(p1);
		Rect g = currentGraph.h2.Split(resizeHandleTex);
		DrawNodeGraphHeader(g);
		currentGraph.h2.End();
		Rect p2 = currentGraph.h1.Split(resizeHandleTex);
		DrawSelector(p2);
		currentGraph.h1.End();

		DrawContextualMenu(g);

		//if event, repaint
		if (Event.current.type == EventType.mouseDown
			|| Event.current.type == EventType.mouseDrag
			|| Event.current.type == EventType.mouseUp
			|| Event.current.type == EventType.scrollWheel
			|| Event.current.type == EventType.KeyDown
			|| Event.current.type == EventType.Repaint
			|| Event.current.type == EventType.KeyUp)
			Repaint();
    }

	void DrawPresetLine(Texture2D tex, string description, Action callback)
	{
		EditorGUILayout.BeginHorizontal();
		{
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(tex, GUILayout.Width(100), GUILayout.Height(100)))
				callback();
			EditorGUILayout.LabelField(description, whiteText);
			GUILayout.FlexibleSpace();
		}
		EditorGUILayout.EndHorizontal();

	}

	void DrawPresetPanel()
	{
		GUI.DrawTexture(new Rect(0, 0, position.width, position.height), backgroundTex);
		EditorGUILayout.BeginHorizontal();
		{
			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginVertical();
			EditorGUILayout.EndVertical();
			EditorGUILayout.BeginVertical();
			{
				GUILayout.FlexibleSpace();
				//TODO: lines
				DrawPresetLine(preset2DSideViewTexture, "2D sideview procedural terrain", () => {});
				DrawPresetLine(preset2DTopDownViewTexture, "2D top down procedural terrain", () => {});
				DrawPresetLine(preset3DPlaneTexture, "3D plane procedural terrain", () => {});
				DrawPresetLine(preset3DSphericalTexture, "3D spherical procedural terrain", () => {});
				DrawPresetLine(preset3DCubicTexture, "3D cubic procedural terrain", () => {});
				DrawPresetLine(preset1DDensityFieldTexture, "1D float density field", () => {});
				DrawPresetLine(preset2DDensityFieldTexture, "2D float density field", () => {});
				DrawPresetLine(preset3DDensityFieldTexture, "3D float density field", () => {});
				DrawPresetLine(presetMeshTetxure, "mesh", () => {});
				GUILayout.FlexibleSpace();
			}
			EditorGUILayout.EndVertical();
			EditorGUILayout.BeginVertical();
			EditorGUILayout.EndVertical();
			GUILayout.FlexibleSpace();
		}
		EditorGUILayout.EndHorizontal();
	}

	void ProcessPreviewScene()
	{
		const string		previewSceneRootName = "PWpreviewSceneRoot";
		const string		previewCameraObjectName = "PWPreviewCamera";
		const string		previewTerrainObjectName = "PWPreviewTerrain";
		const string		previewLayerName = "PWPreviewLayer";

		//initialize preview scene:
		if (previewCamera == null)
		{
			previewCameraObject = GameObject.Find(previewCameraObjectName);
			if (previewCameraObject)
				previewCamera = previewCameraObject.GetComponent< Camera >();
			if (previewCameraObject == null || previewCamera == null || previewCameraRenderTexture == null)
			{
				previewCameraRenderTexture = new RenderTexture(800, 800, 10000, RenderTextureFormat.ARGB32);
				previewSceneRoot = GameObject.Find(previewSceneRootName);
				if (previewSceneRoot == null)
				{
					previewSceneRoot = new GameObject(previewSceneRootName);
					// previewSceneRoot.hideFlags = HideFlags.HideAndDontSave;
				}
				previewCameraObject = GameObject.Find(previewCameraObjectName);
				if (previewCameraObject == null)
				{
					previewCameraObject = new GameObject(previewCameraObjectName);
					previewCameraObject.transform.parent = previewSceneRoot.transform;
					previewCameraObject.transform.position = new Vector3(-15, 21, -15);
					previewCameraObject.transform.rotation = Quaternion.Euler(45, 45, 0);
				}
				//camera settings:
				if (previewCamera == null)
					previewCamera = previewCameraObject.AddComponent< Camera >();
				previewCamera.backgroundColor = Color.white;
				previewCamera.clearFlags = CameraClearFlags.Color;
				previewCamera.targetTexture = previewCameraRenderTexture;

				//camera post processing:
				if (previewCameraObject.GetComponent< Bloom >() == null)
					previewCameraObject.AddComponent< Bloom >().bloomIntensity = .4f;
				if (previewCameraObject.GetComponent< Antialiasing >() == null)
					previewCameraObject.AddComponent< Antialiasing >();
				// previewCameraObject.AddComponent< DepthOfField >();

				if (LayerMask.NameToLayer(previewLayerName) != -1)
					previewCamera.cullingMask = LayerMask.NameToLayer(previewLayerName);
				previewTerrainObject = GameObject.Find(previewTerrainObjectName);
				if (previewTerrainObject == null)
				{
					previewTerrainObject = new GameObject(previewTerrainObjectName);
					previewTerrainObject.transform.parent = previewSceneRoot.transform;
					previewTerrainObject.transform.position = Vector3.zero;
				}
			}
		}
	}

	void DrawLeftBar(Rect currentRect)
	{
		GUI.DrawTexture(currentRect, backgroundTex);

		//add the texturepreviewRect size:
		Rect previewRect = new Rect(0, 0, currentRect.width, currentRect.width);
		currentGraph.leftBarScrollPosition = EditorGUILayout.BeginScrollView(currentGraph.leftBarScrollPosition, GUILayout.ExpandWidth(true));
		{
			EditorGUILayout.BeginHorizontal(GUILayout.Height(currentRect.width), GUILayout.ExpandHeight(true));
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginVertical(GUILayout.Height(currentRect.height - currentRect.width - 4), GUILayout.ExpandWidth(true));
			{
				EditorGUILayout.LabelField("Procedural Worlds Editor !", whiteText);

				if (currentGraph == null)
					OnEnable();
				GUI.SetNextControlName("PWName");
				currentGraph.name = EditorGUILayout.TextField("ProceduralWorld name: ", currentGraph.name);

				if ((Event.current.type == EventType.MouseDown || Event.current.type == EventType.Ignore)
					&& !GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)
					&& GUI.GetNameOfFocusedControl() == "PWName")
					GUI.FocusControl(null);
		
				if (currentGraph.parent == null)
				{
					EditorGUILayout.BeginHorizontal();
					if (GUILayout.Button("Load graph"))
					{
						currentPickerWindow = EditorGUIUtility.GetControlID(FocusType.Passive) + 100;
						EditorGUIUtility.ShowObjectPicker< PWNodeGraph >(null, false, "", currentPickerWindow);
					}
					else if (GUILayout.Button("Save this graph"))
					{
						if (currentGraph.saveName != null)
							return ;
	
						string path = AssetDatabase.GetAssetPath(Selection.activeObject);
						if (path == "")
							path = "Assets";
						else if (Path.GetExtension(path) != "")
							path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
	
						currentGraph.saveName = currentGraph.name;
						string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/" + currentGraph.saveName + ".asset");
	
						AssetDatabase.CreateAsset(currentGraph, assetPathAndName);
	
						AssetDatabase.SaveAssets();
						AssetDatabase.Refresh();
					}
					
					if (Event.current.commandName == "ObjectSelectorUpdated" && EditorGUIUtility.GetObjectPickerControlID() == currentPickerWindow)
					{
						UnityEngine.Object selected = null;
						selected = EditorGUIUtility.GetObjectPickerObject();
						if (selected != null)
						{
							Debug.Log("graph " + selected.name + " loaded");
							currentGraph = (PWNodeGraph)selected;
						}
					}
					EditorGUILayout.EndHorizontal();
				}

				GUI.DrawTexture(previewRect, previewCameraRenderTexture);
		
				//TODO: draw infos / debug / global settings view
			}
			EditorGUILayout.EndVertical();
		}
		EditorGUILayout.EndScrollView();
	}

	Rect DrawSelectorCase(ref Rect r, string name, bool title = false)
	{
		//text box
		Rect boxRect = new Rect(r);
		boxRect.y += 2;
		boxRect.height += 10;

		if (title)
			GUI.DrawTexture(boxRect, selectorCaseTitleBackgroundTex);
		else
			GUI.DrawTexture(boxRect, selectorCaseBackgroundTex);

		boxRect.y += 6;
		boxRect.x += 10;

		EditorGUI.LabelField(boxRect, name, (title) ? whiteBoldText : whiteText);

		r.y += 30;

		return boxRect;
	}

	void DrawSelector(Rect currentRect)
	{
		GUI.DrawTexture(currentRect, selectorBackgroundTex);
		currentGraph.selectorScrollPosition = EditorGUILayout.BeginScrollView(currentGraph.selectorScrollPosition, GUILayout.ExpandWidth(true));
		{
			EditorGUILayout.BeginVertical(splittedPanel);
			{
				EditorGUIUtility.labelWidth = 0;
				EditorGUIUtility.fieldWidth = 0;
				GUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
				{
					currentGraph.searchString = GUILayout.TextField(currentGraph.searchString, GUI.skin.FindStyle("ToolbarSeachTextField"));
					if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSeachCancelButton")))
					{
						// Remove focus if cleared
						currentGraph.searchString = "";
						GUI.FocusControl(null);
					}
				}
				GUILayout.EndHorizontal();
				
				Rect r = EditorGUILayout.GetControlRect();
				foreach (var nodeCategory in nodeSelectorList)
				{
					DrawSelectorCase(ref r, nodeCategory.Key, true);
					foreach (var nodeCase in nodeCategory.Value.Where(n => n.name.IndexOf(currentGraph.searchString, System.StringComparison.OrdinalIgnoreCase) >= 0))
					{
						Rect clickableRect = DrawSelectorCase(ref r, nodeCase.name);
	
						if (Event.current.type == EventType.MouseDown && clickableRect.Contains(Event.current.mousePosition))
							CreateNewNode(nodeCase.nodeType);
					}
				}
			}
			EditorGUILayout.EndVertical();
		}
		EditorGUILayout.EndScrollView();
	}
	
	void DrawNodeGraphHeader(Rect graphRect)
	{
		EditorGUILayout.BeginVertical(splittedPanel);
		{
			//TODO: render the breadcrumbs bar
	
			//remove 4 pixels for the separation bar
			graphRect.size -= Vector2.right * 4;
	
			#if (DEBUG_GRAPH)
			foreach (var node in nodes)
				GUI.DrawTexture(PWUtils.DecalRect(node.rect, graphDecalPosition), debugTexture1);
			#endif
	
			if (Event.current.type == EventType.MouseDown //if event is mouse down
				&& Event.current.button == 0
				&& !currentGraph.mouseAboveNodeAnchor //if mouse is not above a node anchor
				&& graphRect.Contains(Event.current.mousePosition) //and mouse position is in graph
				&& !currentGraph.nodes.Any(n => PWUtils.DecalRect(n.windowRect,currentGraph. graphDecalPosition, true).Contains(Event.current.mousePosition))) //and mouse is not above a window
				currentGraph.dragginGraph = true;
			if (Event.current.type == EventType.MouseUp)
				currentGraph.dragginGraph = false;
			if (Event.current.type == EventType.Layout)
			{
				if (currentGraph.dragginGraph)
					currentGraph.graphDecalPosition += Event.current.mousePosition - lastMousePosition;
				lastMousePosition = Event.current.mousePosition;
			}
		}
		EditorGUILayout.EndVertical();
	}

	string GetUniqueName(string name)
	{
		while (true)
		{
			if (!currentGraph.nodes.Any(p => p.name == name))
				return name;
			name += "*";
		}
	}

	void DisplayDecaledNode(int id, PWNode node, string name)
	{
		node.UpdateGraphDecal(currentGraph.graphDecalPosition);
		node.windowRect = PWUtils.DecalRect(node.windowRect, currentGraph.graphDecalPosition);
		Rect decaledRect = GUILayout.Window(id, node.windowRect, node.OnWindowGUI, name, GUILayout.Height(node.viewHeight));
		node.windowRect = PWUtils.DecalRect(decaledRect, -currentGraph.graphDecalPosition);
	}

	PWNode FindNodeByWindowId(int id)
	{
		var ret = currentGraph.nodes.FirstOrDefault(n => n.windowId == id);

		if (ret != null)
			return ret;
		var gInput = currentGraph.subGraphs.FirstOrDefault(g => g.inputNode.windowId == id);
		if (gInput != null && gInput.inputNode != null)
			return gInput.inputNode;
		var gOutput = currentGraph.subGraphs.FirstOrDefault(g => g.outputNode.windowId == id);
		if (gOutput != null && gOutput.outputNode != null)
			return gOutput.outputNode;

		if (currentGraph.inputNode.windowId == id)
			return currentGraph.inputNode;
		if (currentGraph.outputNode.windowId == id)
			return currentGraph.outputNode;

		return null;
	}

	void RenderNode(int id, PWNode node, string name, int index, ref bool mouseAboveAnchorLocal)
	{
		Event	e = Event.current;

		GUI.depth = node.computeOrder;
		DisplayDecaledNode(id, node, name);

		if (node.windowRect.Contains(e.mousePosition - currentGraph.graphDecalPosition))
			mouseAboveNodeIndex = index;

		//highlight, hide, add all linkable anchors:
		if (currentGraph.draggingLink)
			node.HighlightLinkableAnchorsTo(currentGraph.startDragAnchor);
		node.DisplayHiddenMultipleAnchors(currentGraph.draggingLink);

		//process envent, state and position for node anchors:
		var mouseAboveAnchor = node.ProcessAnchors();
		if (mouseAboveAnchor.mouseAbove)
			mouseAboveAnchorLocal = true;

		//if you press the mouse above an anchor, start the link drag
		if (mouseAboveAnchor.mouseAbove && e.type == EventType.MouseDown)
		{
			currentGraph.startDragAnchor = mouseAboveAnchor;
			currentGraph.draggingLink = true;
		}

		//render node anchors:
		node.RenderAnchors();

		//end dragging:
		if (e.type == EventType.mouseUp && currentGraph.draggingLink == true)
			if (mouseAboveAnchor.mouseAbove)
			{
				//attach link to the node:
				node.AttachLink(mouseAboveAnchor, currentGraph.startDragAnchor);
				var win = currentGraph.nodes.FirstOrDefault(n => n.windowId == currentGraph.startDragAnchor.windowId);
				if (win != null)
					win.AttachLink(currentGraph.startDragAnchor, mouseAboveAnchor);
				else
					Debug.LogWarning("window id not found: " + currentGraph.startDragAnchor.windowId);
				
				//Recalcul the compute order:
				EvaluateComputeOrder();

				currentGraph.draggingLink = false;
			}

		//draw links:
		var links = node.GetLinks();
		foreach (var link in links)
		{
			// Debug.Log("link: " + link.localWindowId + ":" + link.localAnchorId + " to " + link.distantWindowId + ":" + link.distantAnchorId);
			var fromWindow = FindNodeByWindowId(link.localWindowId);
			var toWindow = FindNodeByWindowId(link.distantWindowId);

			if (fromWindow == null || toWindow == null) //invalid window ids
			{
				Debug.LogWarning("window not found: " + link.localWindowId + ", " + link.distantWindowId);
				continue ;
			}

			Rect? fromAnchor = fromWindow.GetAnchorRect(link.localAnchorId);
			Rect? toAnchor = toWindow.GetAnchorRect(link.distantAnchorId);
			if (fromAnchor != null && toAnchor != null)
				DrawNodeCurve(fromAnchor.Value, toAnchor.Value, Color.black);
		}

		//check if user have pressed the close button of this window:
		if (node.WindowShouldClose())
			DeleteNode(index);
	}

	void ProcessNodeAndLinks(PWNode node)
	{
		node.Process();

		var links = node.GetLinks();

		foreach (var link in links)
		{
			var target = FindNodeByWindowId(link.distantWindowId);

			if (target == null)
				continue ;

			var val = bakedNodeFields[link.localClassAQName][link.localName].GetValue(node);
			var prop = bakedNodeFields[link.distantClassAQName][link.distantName];
			if (link.distantIndex == -1)
				prop.SetValue(target, val);
			else //multiple object data:
			{
				PWValues values = (PWValues)prop.GetValue(target);

				if (values != null)
				{
					if (!values.AssignAt(link.distantIndex, val, link.localName))
						Debug.Log("failed to set distant indexed field value: " + link.distantName);
				}
			}
		}
	}

	void DrawNodeGraphCore()
	{
		Event e = Event.current;

		Rect graphRect = EditorGUILayout.BeginHorizontal();
		{
			bool	mouseAboveAnchorLocal = false;
			mouseAboveNodeIndex = -1;
			PWNode.windowRenderOrder = 0;
			int		windowId = 0;
			BeginWindows();
			for (int i = 0; i < currentGraph.nodes.Count; i++)
			{
				var node = currentGraph.nodes[i];
				string nodeName = (string.IsNullOrEmpty(node.name)) ? node.nodeTypeName : node.name;
				RenderNode(windowId++, node, nodeName, i, ref mouseAboveAnchorLocal);
			}

			//display graph sub-PWGraphs
			foreach (var graph in currentGraph.subGraphs)
			{
				graph.outputNode.useExternalWinowRect = true;
				RenderNode(windowId++, graph.outputNode, graph.name, -1, ref mouseAboveAnchorLocal);
			}

			//display the upper graph reference:
			if (currentGraph.parent != null)
				RenderNode(windowId++, currentGraph.inputNode, "upper graph", -1, ref mouseAboveAnchorLocal);
			RenderNode(windowId++, currentGraph.outputNode, "output", -1, ref mouseAboveAnchorLocal);

			EndWindows();
			
			if (e.type == EventType.Repaint)
			{
				if (currentGraph.parent != null)
					ProcessNodeAndLinks(currentGraph.inputNode);
				foreach (var node in currentGraph.nodes)
					ProcessNodeAndLinks(node);
				foreach (var graph in currentGraph.subGraphs)
					ProcessNodeAndLinks(graph.outputNode);
				ProcessNodeAndLinks(currentGraph.outputNode);
			}

			//submachine enter button click management:
			foreach (var graph in currentGraph.subGraphs)
			{
				if (graph.outputNode.specialButtonClick)
				{
					//enter to subgraph:
					currentGraph.draggingLink = false;
					graph.outputNode.useExternalWinowRect = false;
					currentGraph = graph;
				}
			}

			//click up outside of an anchor, stop dragging
			if (e.type == EventType.mouseUp && currentGraph.draggingLink == true)
				currentGraph.draggingLink = false;

			if (currentGraph.draggingLink)
				DrawNodeCurve(
					new Rect((int)currentGraph.startDragAnchor.anchorRect.center.x, (int)currentGraph.startDragAnchor.anchorRect.center.y, 0, 0),
					new Rect((int)e.mousePosition.x, (int)e.mousePosition.y, 0, 0),
					currentGraph.startDragAnchor.anchorColor
				);
			currentGraph.mouseAboveNodeAnchor = mouseAboveAnchorLocal;

		}
		EditorGUILayout.EndHorizontal();
	}

	void DeleteNode(object oid)
	{
		int	id = (int)oid;

		//remove all input links for each node links:
		foreach (var link in currentGraph.nodes[id].GetLinks())
		{
			var node = FindNodeByWindowId(link.distantWindowId);
			if (node != null)
				node.RemoveDependency(link.localWindowId);
		}
		//remove all links for node dependencies
		foreach (var deps in currentGraph.nodes[id].GetDependencies())
		{
			var node = FindNodeByWindowId(deps);
			if (node != null)
				node.RemoveLinkByWindowTarget(deps);
		}

		//remove the node
		currentGraph.nodes.RemoveAt(id);
	}

	void CreateNewNode(object type)
	{
		//TODO: if mouse is in the node graph, add the new node at the mouse position instead of the center of the window
		Type t = (Type)type;
		PWNode newNode = ScriptableObject.CreateInstance(t) as PWNode;
		//center to the middle of the screen:
		newNode.windowRect.position = -currentGraph.graphDecalPosition + new Vector2((int)(position.width / 2), (int)(position.height / 2));
		newNode.SetWindowId(currentGraph.localWindowIdCount++);
		newNode.nodeTypeName = t.ToString();
		currentGraph.nodes.Add(newNode);
	}

	void CreatePWMachine()
	{
		Vector2 pos = -currentGraph.graphDecalPosition + new Vector2((int)(position.width / 2), (int)(position.height / 2));
		PWNodeGraph subgraph = ScriptableObject.CreateInstance< PWNodeGraph >();
		subgraph.externalGraphPosition = pos;
		InitializeNewGraph(subgraph);
		subgraph.parent = currentGraph;
		subgraph.name = "PW sub-machine";
		currentGraph.subGraphs.Add(subgraph);
	}

	void DrawContextualMenu(Rect graphNodeRect)
	{
		Event	e = Event.current;
        if (e.type == EventType.ContextClick)
        {
            Vector2 mousePos = e.mousePosition;
            EditorGUI.DrawRect(graphNodeRect, Color.green);

            if (graphNodeRect.Contains(mousePos))
            {
                // Now create the menu, add items and show it
                GenericMenu menu = new GenericMenu();
				if (mouseAboveNodeIndex != -1)
					menu.AddItem(new GUIContent("Delete node"), false, DeleteNode, mouseAboveNodeIndex);
				else
					menu.AddDisabledItem(new GUIContent("Delete node"));
                menu.AddSeparator("");
				menu.AddItem(new GUIContent("New PWMachine"), false, CreatePWMachine);
				foreach (var nodeCat in nodeSelectorList)
				{
					string menuString = "Create new/" + nodeCat.Key + "/";
					foreach (var nodeClass in nodeCat.Value)
						menu.AddItem(new GUIContent(menuString + nodeClass.name), false, CreateNewNode, nodeClass.nodeType);
				}
                menu.ShowAsContext();
                e.Use();
            }
        }
	}

	//Dictionary< windowId, dependencyWeight >
	Dictionary< int, int > nodeComputeOrderCount = new Dictionary< int, int >();
	int EvaluateComputeOrder(bool first = true, int depth = 0, int windowId = -1)
	{
		//Recursively evaluate compute order for each nodes:
		if (first)
		{
			nodeComputeOrderCount.Clear();
			for (int i = 0; i < currentGraph.nodes.Count; i++)
			{
				currentGraph.nodes[i].computeOrder = EvaluateComputeOrder(false, 1, currentGraph.nodes[i].windowId);
				// Debug.Log("computed order for node " + nodes[i].windowId + ": " + nodes[i].computeOrder);
			}
			//sort nodes for compute order:
			currentGraph.nodes.Sort((n1, n2) => { return n1.computeOrder.CompareTo(n2.computeOrder); });
		}

		//check if we the node have already been computed:
		if (nodeComputeOrderCount.ContainsKey(windowId))
			return nodeComputeOrderCount[windowId];

		var node = FindNodeByWindowId(windowId);
		if (node == null)
			return 0;

		//compute dependency weight:
		int	ret = 1;
		foreach (var dep in node.GetDependencies())
			ret += EvaluateComputeOrder(false, depth + 1, dep);

		nodeComputeOrderCount[windowId] = ret;
		return ret;
	}

	static void CreateBackgroundTexture()
	{
		Func< Color, Texture2D > CreateTexture2DColor = (Color c) => {
			Texture2D	ret;
			ret = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			ret.SetPixel(0, 0, c);
			ret.Apply();
			return ret;
		};

		Func< string, Texture2D > CreateTexture2DFromFile = (string ressourcePath) => {
			return AssetDatabase.LoadAssetAtPath< Texture2D >(ressourcePath);
        };

        Color backgroundColor = new Color32(56, 56, 56, 255);
		Color resizeHandleColor = EditorGUIUtility.isProSkin
			? new Color32(56, 56, 56, 255)
            : new Color32(130, 130, 130, 255);
		Color selectorBackgroundColor = new Color32(80, 80, 80, 255);
		Color selectorCaseBackgroundColor = new Color32(110, 110, 110, 255);
		Color selectorCaseTitleBackgroundColor = new Color32(50, 50, 50, 255);
		
		backgroundTex = CreateTexture2DColor(backgroundColor);
		resizeHandleTex = CreateTexture2DColor(resizeHandleColor);
		selectorBackgroundTex = CreateTexture2DColor(selectorBackgroundColor);
		debugTexture1 = CreateTexture2DColor(new Color(1f, 0f, 0f, .3f));
		selectorCaseBackgroundTex = CreateTexture2DColor(selectorCaseBackgroundColor);
		selectorCaseTitleBackgroundTex = CreateTexture2DColor(selectorCaseTitleBackgroundColor);

		string dir = "Assets/Editor/Ressources/";
		preset2DSideViewTexture = CreateTexture2DFromFile(dir + "preview2DSideView.png");
		preset2DTopDownViewTexture = CreateTexture2DFromFile(dir + "preview2DTopDownView.png");
		preset3DPlaneTexture = CreateTexture2DFromFile(dir + "preview3DPlane.png");
		preset3DSphericalTexture = CreateTexture2DFromFile(dir + "preview3DSpherical.png");
		preset3DCubicTexture = CreateTexture2DFromFile(dir + "preview3DCubic.png");
		presetMeshTetxure = CreateTexture2DFromFile(dir + "previewMesh.png");
		preset1DDensityFieldTexture= CreateTexture2DFromFile(dir + "preview1DDensityField.png");
		preset2DDensityFieldTexture = CreateTexture2DFromFile(dir + "preview2DDensityField.png");
		preset3DDensityFieldTexture = CreateTexture2DFromFile(dir + "preview3DDensityField.png");
	}

    void DrawNodeCurve(Rect start, Rect end, Color c)
    {
		//swap start and end if they are inverted
		if (start.xMax > end.xMax)
			PWUtils.Swap< Rect >(ref start, ref end);

        Vector3 startPos = new Vector3(start.x + start.width, start.y + start.height / 2, 0);
        Vector3 endPos = new Vector3(end.x, end.y + end.height / 2, 0);
        Vector3 startTan = startPos + Vector3.right * 100;
        Vector3 endTan = endPos + Vector3.left * 100;
        Color shadowCol = c;
		shadowCol.a = 0.04f;

        for (int i = 0; i < 3; i++)
            Handles.DrawBezier(startPos, endPos, startTan, endTan, shadowCol, null, (i + 1) * 5);

        Handles.DrawBezier(startPos, endPos, startTan, endTan, Color.black, null, 1);
    }
}