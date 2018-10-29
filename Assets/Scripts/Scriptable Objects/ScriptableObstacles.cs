using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Chance Profile", menuName = "Obstacles", order = 1)]
public class ScriptableObstacles : ScriptableObject
{
	public List<Vector2> coordinates
	{
		get { return obstacleCoordinates; }
	}

	public Sprite ObstacleSprite
	{
		get
		{
			return obstacleSprite;
		}
	}

	[SerializeField]
	private List<Vector2> obstacleCoordinates;

	[SerializeField]
	private Sprite obstacleSprite;

}
