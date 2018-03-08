﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using PW;
using PW.Core;
using PW.Editor;
using PW.Biomator;

public class PWBiomeGraphEditor : PWGraphEditor
{

	List< PWBiomeGraph >	biomeGraphs = new List< PWBiomeGraph >();
	ReorderableList			biomeGraphList;

	[System.NonSerialized]
	PWBiomePresetScreen		presetScreen;

	[MenuItem("Window/Procedural Worlds/Biome Graph", priority = 2)]
	static void Init()
	{
		PWBiomeGraphEditor window = (PWBiomeGraphEditor)GetWindow(typeof(PWBiomeGraphEditor));
		window.Show();
	}

	public override void OnEnable()
	{
		base.OnEnable();
		
		OnGraphChanged += GraphLoadedCallback;

		biomeGraphList = new ReorderableList(biomeGraphs, typeof(PWBiomeGraph), false, true, false, false);

		biomeGraphList.drawElementCallback = (rect, index, active, focus) => {
			EditorGUI.LabelField(rect, biomeGraphs[index].name);
			rect.x += rect.width - 50;
			rect.width = 50;
			rect.height = EditorGUIUtility.singleLineHeight;
			if (GUI.Button(rect, "Open"))
				LoadGraph(biomeGraphs[index]);
		};
		biomeGraphList.drawHeaderCallback = (rect) => {
			EditorGUI.LabelField(rect, "Biome list");
		};
		
		layout = PWLayoutFactory.Create2ResizablePanelLayout(this);

		if (graph != null)
			LoadGUI();

		LoadGraphList();
	}

	void LoadGraphList()
	{
		biomeGraphs.Clear();
		var resGraphs = Resources.FindObjectsOfTypeAll< PWBiomeGraph >();

		foreach (var biomeGraph in resGraphs)
			biomeGraphs.Add(biomeGraph);
	}

	void LoadGUI()
	{
		var settingsPanel = layout.GetPanel< PWGraphSettingsPanel >();
		
		settingsPanel.onGUI = (rect) => {
			settingsPanel.DrawDefault(rect);
			DrawBiomeSettingsBar(rect);
		};
	}

	void GraphLoadedCallback(PWGraph graph)
	{
		if (graph == null)
			return ;
		
		LoadGUI();
	}

	void DrawBiomeSettingsBar(Rect rect)
	{
		GUI.SetNextControlName("PWName");
		graph.name = EditorGUILayout.TextField("Biome name: ", graph.name);

		EditorGUILayout.Space();

		using (DefaultGUISkin.Get())
			biomeGraphList.DoLayoutList();
		
		if (GUILayout.Button("Refresh"))
			LoadGraphList();

		EditorGUILayout.Space();

		biomeGraph.surfaceType = (BiomeSurfaceType)EditorGUILayout.EnumPopup("Biome surface type", biomeGraph.surfaceType);
	}

	public override void OnGUI()
	{
		//draw the node editor
		base.OnGUI();
		
		if (graph == null)
			return ;
		
		if (!biomeGraph.presetChoosed)
		{
			if (presetScreen == null)
				presetScreen = new PWBiomePresetScreen(biomeGraph);
			
			var newGraph = presetScreen.Draw(position, graph);
			
			//we initialize the layout once the user choosed the preset to generate
			if (biomeGraph.presetChoosed)
				ResetLayout();

			if (newGraph != graph)
				LoadGraph(newGraph);

			return ;
		}
		
		layout.DrawLayout();
	}

	public override void OnDisable()
	{
		base.OnDisable();
		
		OnGraphChanged -= GraphLoadedCallback;
	}

}
