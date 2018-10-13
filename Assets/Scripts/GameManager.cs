using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
	public enum DIRECTION : int
	{
		UP = 0,
		DOWN,
		LEFT,
		RIGHT
	}

	public KeyCode mouseButton = KeyCode.Mouse0;

	//Public variables
	[Header("Board"), SerializeField]
	int xTiles;
	[SerializeField]
	int yTiles;

	[SerializeField]
	SpriteRenderer backgroundSprite;
	[SerializeField]
	Vector2 startLocation = Vector2.zero;

	[Header("Tiles"), SerializeField]
	GameObject tilePrefab;
	[SerializeField]
	float tileSize;
	[SerializeField]
	Vector2 tileSpacing;


	[Header("Colors")]
	public Color[] colors;

	//Private variables
	protected Tile[] tiles;
	protected TileLocation[] tileLocations;

	int selectedTile;

	bool mouseDown;
	Vector2 mouseDownPostion;
	Vector2 mouseUpPosition;

	private new Transform transform;

	#region Instance

	public static GameManager Instance
	{
		get
		{
			return mInstance;
		}
		set { mInstance = value; }
	}
	private static GameManager mInstance;

	void Awake()
	{
		if (Instance != null)
		{
			this.enabled = false;
			throw new System.Exception("Trying to create multiple of " + GetType().ToString());
		}

		Instance = this;
	}

	#endregion

	#region Unity Functions

	// Use this for initialization
	private void Start()
	{
		transform = gameObject.transform;

		GenerateTiles();
	}

	private void Update()
	{
		if (Input.GetKeyUp(mouseButton))
		{
			MouseUp();
		}
		else if (Input.GetKeyDown(mouseButton))
		{
			MouseDown();
		}

		if(Input.GetKeyDown(KeyCode.Mouse1))
		{
			int temp;
			if (TryFindClosest(tiles, tileSize / 2f, Input.mousePosition, out temp))
			{
				//Destroy(tiles[temp].transform.gameObject);
				//tiles[temp] = null;
				StartCoroutine(CollapseColumnCoroutine(temp));


				Tile moving = tiles[temp];
				int newIndex = GetTopofColumnIndex(temp);
				SetNewIndex(moving, newIndex);
				moving.transform.position = tileLocations[newIndex].location;
				moving.SetColor(UnityEngine.Random.Range(0, colors.Length));

				//TODO Need to Move tile to the top (New Color, New Index)

			}
		}
	}

	#endregion //Unity Functions

	private void GenerateTiles()
	{
		tiles = new Tile[xTiles * yTiles];
		tileLocations = new TileLocation[xTiles * yTiles];

		backgroundSprite.size = new Vector2(xTiles * tileSpacing.x, yTiles * tileSpacing.y) + (tileSpacing / 2f);

		Vector2 tileStart = startLocation - (new Vector2(xTiles * tileSpacing.x, yTiles * tileSpacing.y) / 2f) + (tileSpacing / 2f);
		for (int i = 0; i < yTiles; i++)
		{
			for (int j = 0; j < xTiles; j++)
			{
				Transform tempTransform = Instantiate(tilePrefab).transform;

				tempTransform.parent = this.transform;
				tempTransform.gameObject.name = string.Format("Tile [{0}, {1}]", j, i);

				tempTransform.position = tileStart + new Vector2(tileSpacing.x * j, tileSpacing.y * i);

				int index = CoordinateToIndex(j, i);
				tiles[index] = new Tile(UnityEngine.Random.Range(0, colors.Length), index, tempTransform);
				tileLocations[index] = new TileLocation(index, tempTransform.position);
			}
		}
	}

	#region Mouse Functions

	private void MouseDown()
	{
		mouseDown = true;
		mouseDownPostion = Input.mousePosition;

		//Divide by 2 to reduce confusion on inspector, as i need the radius, not tile diameter
		if (TryFindClosest(tiles, tileSize / 2f, mouseDownPostion, out selectedTile))
		{
			//tiles[selectedTile].SetColor(Color.red);
		}
	}

	private void MouseUp()
	{
		mouseDown = false;
		mouseUpPosition = Input.mousePosition;

		//Check to ensure we've even selected a tile
		if (selectedTile >= 0)
		{
			//FIXME I think im having issues occaisionally with the direction, Might actually have to do the Trig Function here
			DIRECTION direction = GetMouseDragDirection((mouseUpPosition - mouseDownPostion).normalized);

			Tile foundTile;
			if (TrySwapSelectedPosition(tiles[selectedTile], direction, out foundTile))
			{
				//foundTile.SetColor(Color.blue);

				StartCoroutine(SwapTilePositionsCoroutine(tiles[selectedTile], foundTile));
			}

			//TODO Here is where my coroutine for moving the tiles would go
		}

		//Reset mouse values
		mouseDownPostion = mouseUpPosition = Vector2.zero;
		selectedTile = -1;
	}



	private bool TrySwapSelectedPosition(Tile tile, DIRECTION direction, out Tile swapTile)
	{
		int index = tile.index;
		int target = -1;
		swapTile = null;

		switch (direction)
		{
			case DIRECTION.UP:
				if (index + xTiles >= tiles.Length)
				{
					return false;
				}
				target = index + xTiles;
				break;

			case DIRECTION.DOWN:
				if (index - xTiles < 0)
				{
					return false;
				}
				target = index - xTiles;
				break;

			case DIRECTION.LEFT:
				if (index == 0 || index % xTiles == 0)
				{
					return false;
				}
				target = index - 1;
				break;

			case DIRECTION.RIGHT:
				if (index == (xTiles - 1) || (index - (xTiles - 1)) % xTiles  == 0)
				{
					return false;
				}

				target = index + 1;
				break;
		}


		swapTile = tiles[target];
		return true;
	}

	#endregion //Mouse Functions

	private void SetNewIndex(Tile tile, int newIndex)
	{
		tiles[newIndex] = tile;
		tile.SetIndex(newIndex);
		//TODO I should also set the gameObject name
		var coordinate = IndexToCoordinate(newIndex);
		tile.name = string.Format("Tile [{0}, {1}]", coordinate.x, coordinate.y);
	}

	private int GetTopofColumnIndex(int currentIndex)
	{
		int temp = currentIndex;

		while (true)
		{
			if (temp + xTiles >= tiles.Length)
				return temp;

			temp += xTiles;
		}
	}

	#region Coordinate Converters

	private int CoordinateToIndex(int x, int y)
	{
		return x + (y * xTiles);
	}

	private Vector2 IndexToCoordinate(int index)
	{
		int y = Mathf.FloorToInt(index / xTiles);
		int x = index - (y * xTiles);

		return new Vector2(x, y);
	}

	#endregion //Coordinate Converters

	#region Coroutines

	private IEnumerator SwapTilePositionsCoroutine(Tile tile1, Tile tile2)
	{
		int tempTile1 = tile1.index;
		int tempTile2 = tile2.index;

		Vector2 tile1Pos = tileLocations[tile1.index].location;
		Vector2 tile2Pos = tileLocations[tile2.index].location;

		float _t = 0;

		while(_t < 1f)
		{
			tile1.transform.position = Vector2.Lerp(tile1Pos, tile2Pos, _t);
			tile2.transform.position = Vector2.Lerp(tile2Pos, tile1Pos, _t);

			_t += Time.deltaTime * 2f;

			yield return null;
		}

		//FIXME This needs to be set in the Array too, not only the object
		//tile1.SetIndex(tempTile2.index);
		//tile2.SetIndex(tempTile1.index);

		SetNewIndex(tile1, tempTile2);
		SetNewIndex(tile2, tempTile1);
	}

	//FIXME Need a check for NullReferences on fall origin
	private IEnumerator CollapseColumnCoroutine(int index)
	{
		List<Tile> fallingTiles = null;
		List<Vector2> fallLocations = null;
		List<Vector2> currentLocations = null;
		int temp = index;
		temp += xTiles;

		while (temp < tiles.Length)
		{
			if (fallingTiles == null)
			{
				fallingTiles = new List<Tile>();
				fallLocations = new List<Vector2>();
				currentLocations = new List<Vector2>();
			}

			currentLocations.Add(tileLocations[temp].location);
			fallLocations.Add(tileLocations[temp - xTiles].location);
			fallingTiles.Add(tiles[temp]);

			temp += xTiles;
		}

		if (fallingTiles == null || fallingTiles.Count <= 0)
			yield break;

		float _t = 0f;
		while(_t < 1f)
		{
			for(int i = 0; i < fallingTiles.Count; i++)
			{
				fallingTiles[i].transform.position = Vector2.Lerp(currentLocations[i], fallLocations[i], _t);

			}

			_t += Time.deltaTime * 2f;


			yield return null;
		}

		for (int i = 0; i < fallingTiles.Count; i++)
		{
			SetNewIndex(fallingTiles[i], fallingTiles[i].index - xTiles);
		}
	}

	IEnumerator MovePositionCoroutine(Tile tile, Vector2 startPosition, Vector2 endPosition, Action onFinishedCallback)
	{
		float _t = 0;

		while (_t < 1f)
		{
			tile.transform.position = Vector2.Lerp(startPosition, endPosition, _t);

			_t += Time.deltaTime * 2f;

			yield return null;
		}

		if (onFinishedCallback != null)
			onFinishedCallback();
	}

	#endregion //Coroutines

	#region Static Functions

	private static bool TryFindClosest(Tile[] Tiles, float thresholdRadius, Vector2 mousePosition, out int index)
	{
		Vector2 position = Camera.main.ScreenToWorldPoint(mousePosition);
		

		float tempDistance = 0f;
		float _distance = float.MaxValue;
		index = -1;

		for(int i = 0; i < Tiles.Length; i++)
		{
			tempDistance = Vector2.Distance(position, Tiles[i].transform.position);

			if (tempDistance > thresholdRadius)
				continue;

			if(tempDistance < _distance)
			{
				_distance = tempDistance;
				index = i;
			}
		}

		return index >= 0;


	}

	private static DIRECTION GetMouseDragDirection(Vector2 dragDirection)
	{
		//Debug.Log("Drag Direction: " + dragDirection);
		//Vector2 dragDirection = mouseUpPosition - mouseDownPostion;
		DIRECTION outDirection;

		if (Mathf.Abs(dragDirection.x) > Mathf.Abs(dragDirection.y))
		{
			//TODO Move left or Right
			outDirection = dragDirection.x < 0f ? DIRECTION.LEFT : DIRECTION.RIGHT;
		}
		else
		{
			//TODO Move Up or Down
			outDirection = dragDirection.y < 0f ? DIRECTION.DOWN : DIRECTION.UP;
		}

		return outDirection;
	}

	private static Vector2 CalculateMouseDragDirection(Vector2 dragDirection)
	{
		//Vector2 dragDirection = mouseUpPosition - mouseDownPostion;
		Vector2 outDirection = Vector2.zero;

		if (Mathf.Abs(dragDirection.x) > Mathf.Abs(dragDirection.y))
		{
			//TODO Move left or Right
			outDirection = dragDirection.x < 0f ? Vector2.left : Vector2.right;
		}
		else
		{
			//TODO Move Up or Down
			outDirection = dragDirection.x < 0f ? Vector2.down : Vector2.up;
		}

		return outDirection;
	}

	#endregion //Static Functions

	#region Extra Classes

	public struct TileLocation
	{
		public int index;
		public Vector2 location;

		public TileLocation(int Index, Vector2 Location)
		{
			index = Index;
			location = Location;
		}
	}

	public class Tile
	{
		public string name
		{
			get { return transform.gameObject.name; }
			set { transform.gameObject.name = value; }
		}
		public int color { get; private set; }
		public int index { get; private set; }
		public Transform transform { get; private set; }

		private SpriteRenderer mRenderer;

		public Tile(int Color, int Index, Transform Transform)
		{
			transform = Transform;
			mRenderer = transform.GetComponent<SpriteRenderer>();

			index = Index;
			color = Color;
			//TODO Need to get Color
			mRenderer.color = GameManager.Instance.colors[color];

		}

		public void SetColor(Color _color)
		{
			mRenderer.color = _color;
		}
		public void SetColor(int _color)
		{
			color = _color;
			mRenderer.color = GameManager.Instance.colors[color];
		}

		public void SetIndex(int index)
		{
			this.index = index;
		}


		public static bool operator ==(Tile lhs, Tile rhs)
		{
			return lhs.color == rhs.color;
		}
		public static bool operator !=(Tile lhs, Tile rhs)
		{
			return !(lhs.color == rhs.color);
		}
	}

	#endregion //Extra Classes

}
