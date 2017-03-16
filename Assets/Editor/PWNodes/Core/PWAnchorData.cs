﻿using System.Collections.Generic;
using UnityEngine;
using System;

namespace PW
{
	[System.SerializableAttribute]
	public enum PWAnchorType
	{
		Input,
		Output,
		None,
	}

	[System.SerializableAttribute]
	public enum PWVisibility
	{
		Visible,
		Invisible,
		InvisibleWhenLinking,	//anchor is invisible while user is linking two anchors:
		Gone,					//anchor is invisible and his size is ignored
	}

	[System.SerializableAttribute]
	public enum PWAnchorHighlight
	{
		None,
		AttachNew,				//link will be attached to unlinked anchor
		AttachReplace,			//link will replace actual anchor link
		AttachAdd,				//link will be added to anchor links
	}

	[System.SerializableAttribute]
	public class PWAnchorData
	{
		//name of the attached propery / name specified in PW I/O.
		public string				fieldName;
		//window id of the anchor
		public int					windowId;
		//anchor type (input / output)
		public PWAnchorType			anchorType;
		//anchor field type
		public SerializableType		type;
		//if the anchor is rendered as multiple
		public bool					multiple;
		//if the type is generic of defined;
		public bool					generic;
		//the full name of the node class:
		public string				classAQName;
		//list of rendered anchors:
		[SerializeField]
		public List< PWAnchorMultiData >	multi;
		//accessor for the first anchor data:
		public PWAnchorMultiData	first { get{ return multi[0]; } set{ multi[0] = value; } }

		//properties for multiple anchors:
		[SerializeField]
		public SerializableType[]	allowedTypes;
		public int					minMultipleValues;
		public int					maxMultipleValues;
		public int					multipleValueCount;
		//instance of the field
		public object				anchorInstance;
		//field to copy values and anchor.
		public string				mirroredField;
		//current number of rendered anchors:
		public bool					displayHiddenMultipleAnchors;

		[System.SerializableAttribute]
		public class PWAnchorMultiData
		{
			public PWAnchorHighlight	highlighMode;
			public Rect					anchorRect;
			public bool					enabled;
			public SerializableColor	color;
			public string				name;
			public PWVisibility			visibility;
			//the visual offset of the anchor
			public Vector2				offset;
			//the id of the actual anchor
			public int					id;
			//external link connected to this anchor
			public int					linkCount;
			//if anchor is an additional hidden anchor (only visible when creating a new link)
			public bool					additional;

			public PWAnchorMultiData(Color color)
			{
				//dont touych to id, it is generated by the window
				additional = false;
				enabled = true;
				linkCount = 0;
				highlighMode = PWAnchorHighlight.None;
				visibility = PWVisibility.Visible;
				this.color = (SerializableColor)color;
			}
		}

		public PWAnchorData(string name, int id)
		{
			multiple = false;
			generic = false;
			displayHiddenMultipleAnchors = false;
			mirroredField = null;

			multi = new List< PWAnchorMultiData >(){new PWAnchorMultiData(Color.white)};
			multi[0].id = id;
			multi[0].name = name;

			// Debug.Log("created anchor with id: " + multi[0].id);
		}

		public void AddNewAnchor(int id)
		{
			AddNewAnchor(first.color, id);
		}

		public void AddNewAnchor(Color c, int id)
		{
			PWAnchorMultiData	tmp = new PWAnchorMultiData(c);
			PWValues			anchorValues = anchorInstance as PWValues;

			tmp.name = first.name;
			tmp.additional = true;
			tmp.id = id;
			if (anchorValues.Count == multipleValueCount)
				multipleValueCount++;
			// Debug.Log("new anchor with id: " + id);
			//add an object to the PWValues list:
			anchorValues.Add(null);

			multi.Add(tmp);
		}
	}
}