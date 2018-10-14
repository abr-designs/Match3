using System.Linq;
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

	public readonly int MATCH = 3;

	public KeyCode mouseButton = KeyCode.Mouse0;

	//////////////////////////////////////////////////////////////////////////
	//Public variables
	//////////////////////////////////////////////////////////////////////////
	[Header("Board"), SerializeField]
	int xTiles;
	[SerializeField]
	int yTiles;

	[SerializeField]
	SpriteRenderer backgroundSprite;
	[SerializeField]
	Vector2 startLocation = Vector2.zero;

	//////////////////////////////////////////////////////////////////////////
	//Tile Properties
	//////////////////////////////////////////////////////////////////////////
	[Header("Tiles"), SerializeField]
	GameObject tilePrefab;
	[SerializeField]
	float tileSize;
	[SerializeField]
	Vector2 tileSpacing;

	//////////////////////////////////////////////////////////////////////////
	//Color Properties
	//////////////////////////////////////////////////////////////////////////
	[Header("Colors")]
	public Color[] colors;

	//////////////////////////////////////////////////////////////////////////
	//Private variables
	//////////////////////////////////////////////////////////////////////////
	protected Tile[] tiles;
	protected TileLocation[] tileLocations;

	private int selectedTile;

	private bool mouseDown;
	private Vector2 mouseDownPostion;
	private Vector2 mouseUpPosition;

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
				CheckForMatches(temp);
				////Destroy(tiles[temp].transform.gameObject);
				////tiles[temp] = null;
				//StartCoroutine(CollapseColumnCoroutine(temp));
				//
				//
				//Tile moving = tiles[temp];
				//int newIndex = GetTopofColumnIndex(temp);
				//SetNewIndex(moving, newIndex);
				//moving.transform.position = tileLocations[newIndex].location;
				//moving.SetColor(UnityEngine.Random.Range(0, colors.Length));
				//
				////TODO Need to Move tile to the top (New Color, New Index)

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
		if (isMoving)
			return;

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

		if (CheckLegalDirection(direction, index))
		{
			target = index + DirectionToInt(direction);
		}
		else
			return false;

		swapTile = tiles[target];
		return true;
	}

	#endregion //Mouse Functions

	#region Check for Matches

	List<int> CheckForMatches(int checkIndex)
	{
		List<int> verticalIndexes = new List<int>() { checkIndex };
		List<int> horizontalIndexes = new List<int>() { checkIndex };

		DIRECTION dir;
		for (int i = 0; i < 4; i++)
		{
			dir = (DIRECTION)i;
			if (CheckLegalDirection(dir, checkIndex))
			{
				if(i <= 1)
					CheckMatch(tiles[checkIndex], checkIndex + DirectionToInt(dir), dir, ref verticalIndexes);
				else
					CheckMatch(tiles[checkIndex], checkIndex + DirectionToInt(dir), dir, ref horizontalIndexes);
			}
		}

		

		if ((verticalIndexes.Count >= MATCH || horizontalIndexes.Count >= MATCH))
		{
			Debug.LogFormat("<color=green><b>At [{0}] HAS MATCH</b>; Vertical Matches: {2}; Horizontal Matches: {3}</color>",
				  checkIndex, null, verticalIndexes.Count, horizontalIndexes.Count);

			List<int> _out = new List<int>();
			if (verticalIndexes.Count >= MATCH)
				_out.AddRange(verticalIndexes);

			if (horizontalIndexes.Count >= MATCH)
				_out.AddRange(horizontalIndexes);

			return _out;
		}
		else
		{
			Debug.LogFormat("<color=red>At [{0}] DOESNT HAVE MATCH; Vertical Matches: {2}; Horizontal Matches: {3}</color>",
				checkIndex, null, verticalIndexes.Count, horizontalIndexes.Count);
		}

		return null;

	}

	private bool CheckMatch(Tile compareTo, int tileIndex, DIRECTION direction, ref List<int> indexes)
	{
		if(tiles[tileIndex] == compareTo)
		{
			indexes.Add(tileIndex);
			if (CheckLegalDirection(direction, tileIndex))
				return CheckMatch(tiles[tileIndex], tileIndex + DirectionToInt(direction), direction, ref indexes);
		}

		return false;
	}

	int DirectionToInt(DIRECTION direction)
	{
		switch (direction)
		{
			case DIRECTION.UP:
				return xTiles;
			case DIRECTION.DOWN:
				return -xTiles;
			case DIRECTION.LEFT:
				return -1;
			case DIRECTION.RIGHT:
				return 1;
		}

		return 0;
	}

	#endregion //Check for Matches

	#region Coordinate Functions

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

	bool CheckLegalDirection(DIRECTION direction, int currentIndex)
	{
		switch (direction)
		{
			case DIRECTION.UP:
				return !(currentIndex + xTiles >= tiles.Length);

			case DIRECTION.DOWN:
				return !(currentIndex - xTiles < 0);

			case DIRECTION.LEFT:
				return !(currentIndex == 0 || currentIndex % xTiles == 0);

			case DIRECTION.RIGHT:
				return !(currentIndex == (xTiles - 1) || (currentIndex - (xTiles - 1)) % xTiles == 0);
		}

		return false;
	}

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

	private int IndexToColumn(int index)
	{
		return (int)IndexToCoordinate(index).x;
	}

	#endregion //Coordinate Functions

	#region Coroutines

	private IEnumerator SwapTilePositionsCoroutine(Tile tile1, Tile tile2)
	{
		int tempIndex1 = tile1.index;
		int tempIndex2 = tile2.index;

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

		SetNewIndex(tile1, newIndex: tempIndex2);
		SetNewIndex(tile2, newIndex: tempIndex1);

		var check1 = CheckForMatches(tempIndex1);
		var check2 = CheckForMatches(tempIndex2);

		if (check1 == null && check2 == null)
			yield break;

		List<int> indexes = new List<int>();
		if (check1 != null) indexes.AddRange(check1);
		if (check2 != null) indexes.AddRange(check2);

		StartCoroutine(FallCoroutine(indexes.Distinct().ToList()));
	}

	bool isMoving = false;
	private IEnumerator FallCoroutine(List<int> targetIndexes)
	{
		isMoving = true;
		List<int>[] columns = new List<int>[xTiles];
		List<MoveRequest> requests = new List<MoveRequest>();

		//////////////////////////////////////////////////////////////////////
		//////////////////////////////////////////////////////////////////////

		//Find the amount and which tiles of each column need to be moved
		for (int i = 0; i < targetIndexes.Count; i++)
		{
			int column = IndexToColumn(targetIndexes[i]);

			if (columns[column] == null)
				columns[column] = new List<int>();

			columns[column].Add(targetIndexes[i]);
		}

		//////////////////////////////////////////////////////////////////////
		//////////////////////////////////////////////////////////////////////\

		for (int i = 0; i < columns.Length; i++)
		{
			if (columns[i] == null)
				continue;

			//Find column move min & Max
			int minLimboIndex = columns[i].Min();
			//int maxLimboIndex = columns[i].Max();

			//Find number of tiles above it after max of tiles
			List<int> above = TilesAboveIndex(minLimboIndex);

			
			//Request move tiles to new index location (offset of columns[column].Count * xTiles)
			for(int j = 0; j < above.Count; j++)
			{
				int aboveIndex = tiles[above[j]].index;
				requests.Add(new MoveRequest()
				{
					//FIXME The amount of tiles moving needs to be the amount below, not just count
					tile = tiles[above[j]],
					//targetIndex = tiles[above[j]].index - (columns[i].Count * xTiles)
					targetIndex = aboveIndex - (LessThanCount(aboveIndex, columns[i]) * xTiles)
				});
			}

			//Request move limbo tiles to now vacant positions
			for (int j = 0; j < columns[i].Count; j++)
			{
				tiles[columns[i][j]].transform.position = OffsetAboveColumn(column: i, offset: j + 1);
				tiles[columns[i][j]].SetColor(Random.Range(0, colors.Length));

				int columnTopIndex = (i + (xTiles * (yTiles - 1)));
				int verticalOffset = ((columns[i].Count) * xTiles);
				verticalOffset -= xTiles * (j + 1);

				requests.Add(new MoveRequest()
				{
					tile = tiles[columns[i][j]],
					targetIndex = columnTopIndex - verticalOffset
				});
			}
		}

		//UnityEditor.EditorApplication.isPaused = true;
		//yield return null;

		float _t = 0;
		Debug.Log("Moving");
		List<Vector2> startPositions = new List<Vector2>();
		for (int i = 0; i < requests.Count; i++)
		{
			startPositions.Add(requests[i].tile.transform.position);
		}

		while (_t < 1f)
		{
			for(int i = 0; i < requests.Count; i++)
			{
				requests[i].tile.transform.position = Vector2.Lerp(
					startPositions[i],
					tileLocations[requests[i].targetIndex].location, _t);
			}

			_t += Time.deltaTime;

			yield return null;
		}

		Debug.Log("Setting");

		//Swap tiles
		for (int i = 0; i < requests.Count; i++)
		{
			requests[i].tile.transform.position = tileLocations[requests[i].targetIndex].location;
			SetNewIndex(requests[i].tile, newIndex: requests[i].targetIndex);
		}

		Debug.Log("Done");
		isMoving = false;

		yield return null;

		CheckFallingMatch(requests);
	}

	void CheckFallingMatch(List<MoveRequest> tiles)
	{
		List<int> indexes = new List<int>();

		for(int i = 0; i < tiles.Count; i++)
		{
			var temp = CheckForMatches(tiles[i].targetIndex);

			if (temp != null)
				indexes.AddRange(temp);
		}

		if (indexes.Count > 0)
		{
			//UnityEditor.EditorApplication.isPaused = true;
			StartCoroutine(FallCoroutine(indexes.Distinct().ToList()));
		}
	}

	private static int LessThanCount(int value, List<int> values)
	{
		int count = 0;
		for(int i = 0; i < values.Count; i++)
		{
			if (values[i] < value)
				count++;
		}

		return count;
	}

	class MoveRequest
	{
		public Tile tile;
		public int targetIndex;

	}

	private List<int> TilesAboveIndex(int index)
	{
		List<int> tilesAbove = new List<int>();
		while (true)
		{
			index += DirectionToInt(DIRECTION.UP);

			if (index >= tiles.Length)
				return tilesAbove;

			tilesAbove.Add(index);
		}
	}

	private Vector2 OffsetAboveColumn(int column, int offset)
	{
		//Debug.LogFormat("Index[{0}], Column: {1}, Max Tiles: {2}, Offset: {3}",(column + (xTiles * (yTiles - 1))), column, tiles.Length, (xTiles * (yTiles - 1)));
		Vector2 topCoordinate = tileLocations[column + (xTiles * (yTiles - 1))].location;

		return topCoordinate + new Vector2(0, tileSpacing.y * offset);
	}

	//FIXME Need a check for NullReferences on fall origin
	//private IEnumerator CollapseColumnCoroutine(int index)
	//{
	//	List<Tile> fallingTiles = null;
	//	List<Vector2> fallLocations = null;
	//	List<Vector2> currentLocations = null;
	//	int temp = index;
	//	temp += xTiles;
	//
	//	while (temp < tiles.Length)
	//	{
	//		if (fallingTiles == null)
	//		{
	//			fallingTiles = new List<Tile>();
	//			fallLocations = new List<Vector2>();
	//			currentLocations = new List<Vector2>();
	//		}
	//
	//		currentLocations.Add(tileLocations[temp].location);
	//		fallLocations.Add(tileLocations[temp - xTiles].location);
	//		fallingTiles.Add(tiles[temp]);
	//
	//		temp += xTiles;
	//	}
	//
	//	if (fallingTiles == null || fallingTiles.Count <= 0)
	//		yield break;
	//
	//	float _t = 0f;
	//	while(_t < 1f)
	//	{
	//		for(int i = 0; i < fallingTiles.Count; i++)
	//		{
	//			fallingTiles[i].transform.position = Vector2.Lerp(currentLocations[i], fallLocations[i], _t);
	//
	//		}
	//
	//		_t += Time.deltaTime * 2f;
	//
	//
	//		yield return null;
	//	}
	//
	//	for (int i = 0; i < fallingTiles.Count; i++)
	//	{
	//		SetNewIndex(fallingTiles[i], fallingTiles[i].index - xTiles);
	//	}
	//}
	//
	//IEnumerator MovePositionCoroutine(Tile tile, Vector2 startPosition, Vector2 endPosition, Action onFinishedCallback)
	//{
	//	float _t = 0;
	//
	//	while (_t < 1f)
	//	{
	//		tile.transform.position = Vector2.Lerp(startPosition, endPosition, _t);
	//
	//		_t += Time.deltaTime * 2f;
	//
	//		yield return null;
	//	}
	//
	//	if (onFinishedCallback != null)
	//		onFinishedCallback();
	//}

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
