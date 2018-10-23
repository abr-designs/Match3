using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Color Profile", menuName = "Colors/Color Profile", order = 1)]
public class ScriptableColorPallet : ScriptableObject
{
	public Color[] Colors
	{
		get
		{
			return colors;
		}
	}

	public int Length
	{
		get
		{
			return colors.Length;
		}
	}

	[SerializeField]
	protected Color[] colors;

}
