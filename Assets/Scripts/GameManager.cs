using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
	private enum DIRECTION : int
	{
		UP = 0,
		DOWN,
		LEFT,
		RIGHT
	}

	public enum POWERUP : int
	{
		NONE = 0,
		LINE,
		CROSS,
		COLOR
	}

	//////////////////////////////////////////////////////////////////////////
	//////////////////STATIC PROPERTIES///////////////////////////////////////
	//////////////////////////////////////////////////////////////////////////
	public static readonly int MATCH = 3;
	//public static readonly float POWERUP_CHANCE = 0.025f;

	//////////////////////////////////////////////////////////////////////////
	//////////////////PUBLIC PROPERTIES///////////////////////////////////////
	//////////////////////////////////////////////////////////////////////////

	//Input properties
	public KeyCode mouseButton = KeyCode.Mouse0;

	[Header("Seed"), SerializeField]
	private bool useSeed;
	[SerializeField]
	private int seed;

	//Board Properties
	[Header("Board"), SerializeField]
	private int xTiles;
	[SerializeField]
	private int yTiles;

	[SerializeField]
	private SpriteRenderer backgroundSprite;
	[SerializeField]
	private Vector2 startLocation = Vector2.zero;

	//Tile Properties
	[Header("Tiles"), SerializeField]
	private GameObject tilePrefab;
	[SerializeField]
	private float tileSize;
	[SerializeField]
	private Vector2 tileSpacing;

	[Header("Tiles"), SerializeField]
	ScriptableObstacles obstacles;

	[Header("Speeds"), SerializeField]
	private float swapSpeed = 5f;
	[SerializeField]
	private AnimationCurve swapCurve = new AnimationCurve();
	[SerializeField]
	private float fallSpeed = 10f;
	[SerializeField]
	private AnimationCurve fallCurve = new AnimationCurve();

	[Header("Power Ups"), SerializeField]
	private ScriptablePowerupProfile powerupProfile;

	//Color Properties
	[Header("Colors"), SerializeField]
	//public Color[] colors;
	private ScriptableColorPallet colorProfile;

	//////////////////////////////////////////////////////////////////////////
	////////////////////PRIVATE PROPERTIES////////////////////////////////////
	//////////////////////////////////////////////////////////////////////////
	private bool coroutineActive = false;

	protected List<int> obstacleIndexes;

	//Tiles Properties
	protected Tile[] tiles;
	protected TileLocation[] tileLocations;
	private int selectedTile;

	//Mouse Properties
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
		if(!useSeed)
			seed = Random.Range(int.MinValue, int.MaxValue);

		Debug.LogError("Using Random Seed: " + seed);

		Random.InitState(seed);
		powerupProfile.Init();

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
	}

	#endregion //Unity Functions

	private void GenerateTiles()
	{
		GetObstacles();

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

				//Determine if the tile must be an Obstacle or a moveable Tile
				if(obstacleIndexes.Contains(index))
					tiles[index] = new Tile(index, tempTransform, obstacles.ObstacleSprite);
				else
					tiles[index] = new Tile(UnityEngine.Random.Range(0, colorProfile.Length), index, tempTransform);


				tileLocations[index] = new TileLocation(index, tempTransform.position);
			}
		}
	}

	private void GetObstacles()
	{
		obstacleIndexes = new List<int>();

		if (obstacles == null)
			return;

		for(int i = 0; i < obstacles.coordinates.Count; i++)
		{
			obstacleIndexes.Add(CoordinateToIndex(obstacles.coordinates[i]));
		}

	}

	#region Mouse Functions

	private void MouseDown()
	{
		if (coroutineActive)
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

		if (tiles[target].isObstacle)
			return false;

		swapTile = tiles[target];
		return true;
	}

	#endregion //Mouse Functions

	#region Check for Matches

	private List<int> CheckForMatches(int checkIndex)
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

			//Ensure that we don't use multiple of the same element
			_out = _out.Distinct().ToList();

			//List of indexes affected by Powerups
			List<int> extraOut = new List<int>();

			for (int i = 0; i < _out.Count; i++)
			{
				switch (tiles[_out[i]].powerUp)
				{
					case POWERUP.NONE:
						continue;

					case POWERUP.LINE:
						extraOut.AddRange(GetWholeRow(_out[i]));
						break;

					case POWERUP.CROSS:
						extraOut.AddRange(GetWholeRow(_out[i]));
						extraOut.AddRange(GetWholeColumn(_out[i]));
						break;

					case POWERUP.COLOR:
						extraOut.AddRange(GetAllSimilarColor(_out[i]));
						break;

					default:
						throw new System.NotImplementedException();
				}
			}

			_out.AddRange(extraOut);

			return _out;
		}

		return null;

	}

	/// <summary>
	/// Algorithm to search in direction for number of matches
	/// </summary>
	/// <param name="compareTo"></param>
	/// <param name="tileIndex"></param>
	/// <param name="direction"></param>
	/// <param name="indexes"></param>
	/// <returns></returns>
	private bool CheckMatch(Tile compareTo, int tileIndex, DIRECTION direction, ref List<int> indexes)
	{
		if (tiles[tileIndex].isObstacle)
			return false;

		if(tiles[tileIndex] == compareTo)
		{
			indexes.Add(tileIndex);
			if (CheckLegalDirection(direction, tileIndex))
				return CheckMatch(tiles[tileIndex], tileIndex + DirectionToInt(direction), direction, ref indexes);
		}

		return false;
	}

	
	/// <summary>
	/// Checks all tiles which have cascaded to search for new matches
	/// </summary>
	/// <param name="tiles"></param>
	private void CheckFallingMatch(List<MoveRequest> tiles)
	{
		List<int> indexes = new List<int>();

		for (int i = 0; i < tiles.Count; i++)
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

	#endregion //Check for Matches

	#region Coordinate Functions

	/// <summary>
	/// Updates the index of specified tile
	/// </summary>
	/// <param name="tile"></param>
	/// <param name="newIndex"></param>
	private void SetNewIndex(Tile tile, int newIndex)
	{
		tiles[newIndex] = tile;
		tile.SetIndex(newIndex);
		//TODO I should also set the gameObject name
		var coordinate = IndexToCoordinate(newIndex);
		tile.name = string.Format("Tile [{0}, {1}]", coordinate.x, coordinate.y);
	}

	/// <summary>
	/// Returns the top index of column currentIndex is located
	/// </summary>
	/// <param name="currentIndex"></param>
	/// <returns></returns>
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

	/// <summary>
	/// Attemps to see if the move in specified direction is possible
	/// </summary>
	/// <param name="direction"></param>
	/// <param name="currentIndex"></param>
	/// <returns></returns>
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

	private int CoordinateToIndex(Vector2 coordinate)
	{
		return CoordinateToIndex((int)coordinate.x, (int)coordinate.y);
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

	/// <summary>
	/// Returns the column number based on index
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	private int IndexToColumn(int index)
	{
		return (int)IndexToCoordinate(index).x;
	}

	/// <summary>
	/// Returns the row number based on index
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	private int IndexToRow(int index)
	{
		return (int)IndexToCoordinate(index).y;
	}

	private List<int> GetWholeRow(int currentIndex)
	{
		List<int> _out = new List<int>();
		int rowStartIndex = IndexToRow(currentIndex) * xTiles;

		for(int i = 0; i < xTiles; i++)
		{
			if (isObstacle(rowStartIndex + i))
				continue;

			_out.Add(rowStartIndex + i);
		}

		return _out;
	}

	private List<int> GetWholeColumn(int currentIndex)
	{
		List<int> _out = new List<int>();
		int rowStartIndex = IndexToColumn(currentIndex);

		for (int i = 0; i < yTiles; i++)
		{
			//if (isObstacle(rowStartIndex + (i * xTiles)))
			//	continue;

			_out.Add(rowStartIndex + (i * xTiles));
		}

		return _out;
	}

	private List<int> GetAllSimilarColor(int currentIndex)
	{
		List<int> _out = new List<int>();
		Tile tile = tiles[currentIndex];

		for (int i = 0; i < tiles.Length; i++)
		{
			if (tiles[i].index == currentIndex)
				continue;

			if (tile == tiles[i])
				_out.Add(i);
		}

		return _out;
	}

	/// <summary>
	/// Returns list of tiles above specific index
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	private List<int> GetTileIndicesAbove(int index)
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

	private List<int> GetTileIndicesAbove(int index, List<int> excluding)
	{
		List<int> tilesAbove = new List<int>();
		while (true)
		{
			index += DirectionToInt(DIRECTION.UP);

			if (index >= tiles.Length)
				return tilesAbove;

			if(!excluding.Contains(index))
				tilesAbove.Add(index);
		}
	}

	/// <summary>
	/// Offsets a location based on the column. Multiplies offset by global spacing values Up
	/// </summary>
	/// <param name="column"></param>
	/// <param name="offset"></param>
	/// <returns></returns>
	private Vector2 OffsetAboveColumn(int column, int offset)
	{
		//Debug.LogFormat("Index[{0}], Column: {1}, Max Tiles: {2}, Offset: {3}",(column + (xTiles * (yTiles - 1))), column, tiles.Length, (xTiles * (yTiles - 1)));
		Vector2 topCoordinate = tileLocations[column + (xTiles * (yTiles - 1))].location;

		return topCoordinate + new Vector2(0, tileSpacing.y * offset);
	}

	/// <summary>
	/// Converts a direction to an index offset
	/// </summary>
	/// <param name="direction"></param>
	/// <returns></returns>
	private int DirectionToInt(DIRECTION direction)
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

	#endregion //Coordinate Functions

	#region Coroutines

	/// <summary>
	/// Swaps the tile positions, and will then check to see if the two have matches next to eachother
	/// </summary>
	/// <param name="tile1"></param>
	/// <param name="tile2"></param>
	/// <returns></returns>
	private IEnumerator SwapTilePositionsCoroutine(Tile tile1, Tile tile2)
	{
		coroutineActive = true;

		int tempIndex1 = tile1.index;
		int tempIndex2 = tile2.index;

		Vector2 tile1Pos = tileLocations[tile1.index].location;
		Vector2 tile2Pos = tileLocations[tile2.index].location;

		float _t = 0;

		while(_t < 1f)
		{
			tile1.transform.position = Vector2.Lerp(tile1Pos, tile2Pos, swapCurve.Evaluate(_t));
			tile2.transform.position = Vector2.Lerp(tile2Pos, tile1Pos, swapCurve.Evaluate(_t));

			_t += Time.deltaTime * swapSpeed;

			yield return null;
		}

		SetNewIndex(tile1, newIndex: tempIndex2);
		SetNewIndex(tile2, newIndex: tempIndex1);

		var check1 = CheckForMatches(tempIndex1);
		var check2 = CheckForMatches(tempIndex2);

		//We dont want to check for matches if we have an override.
		if (check1 == null && check2 == null)
		{
			StartCoroutine(ForceSwapTilePositionsCoroutine(tiles[tempIndex2], tiles[tempIndex1]));
			yield break;
		}

		List<int> indexes = new List<int>();
		if (check1 != null) indexes.AddRange(check1);
		if (check2 != null) indexes.AddRange(check2);

		StartCoroutine(FallCoroutine(indexes.Distinct().ToList()));

	}

	/// <summary>
	/// Used for swapping tiles back to original positions, used in the event of no matches
	/// </summary>
	/// <param name="tile1"></param>
	/// <param name="tile2"></param>
	/// <returns></returns>
	private IEnumerator ForceSwapTilePositionsCoroutine(Tile tile1, Tile tile2)
	{
		coroutineActive = true;

		int tempIndex1 = tile1.index;
		int tempIndex2 = tile2.index;

		Vector2 tile1Pos = tileLocations[tile1.index].location;
		Vector2 tile2Pos = tileLocations[tile2.index].location;

		float _t = 0;

		while (_t < 1f)
		{
			tile1.transform.position = Vector2.Lerp(tile1Pos, tile2Pos, swapCurve.Evaluate(_t));
			tile2.transform.position = Vector2.Lerp(tile2Pos, tile1Pos, swapCurve.Evaluate(_t));

			_t += Time.deltaTime * swapSpeed;

			yield return null;
		}

		SetNewIndex(tile1, newIndex: tempIndex2);
		SetNewIndex(tile2, newIndex: tempIndex1);

		coroutineActive = false;

	}


	/// <summary>
	/// Cascades all of the tiles that have been queued to fall
	/// </summary>
	/// <param name="matchedIndexes">List of all matched Tiles(Displaced Tiles)</param>
	/// <returns></returns>
	private IEnumerator FallCoroutine(List<int> matchedIndexes)
	{
		coroutineActive = true;
		List<int>[] columns = new List<int>[xTiles];
		List<MoveRequest> requests = new List<MoveRequest>();

		//////////////////////////////////////////////////////////////////////
		//////////////////////////////////////////////////////////////////////

		//Find the amount and which tiles of each column need to be moved
		for (int i = 0; i < matchedIndexes.Count; i++)
		{
			int column = IndexToColumn(matchedIndexes[i]);

			if (columns[column] == null)
				columns[column] = new List<int>();

			columns[column].Add(matchedIndexes[i]);
		}

		//for (int i = 0; i < columns.Length; i++)
		//	columns[i].OrderByDescending(x => x);


		//////////////////////////////////////////////////////////////////////
		//////////////////////////////////////////////////////////////////////\

		//UnityEditor.EditorApplication.isPaused = true;
		//yield return null;

		for (int i = 0; i < columns.Length; i++)
		{
			if (columns[i] == null)
				continue;

			//Find column move min & Max
			int minLimboIndex = columns[i].Min();
			//int maxLimboIndex = columns[i].Max();

			//Find number of tiles above it after max of tiles
			List<int> tilesAboveIndex = GetTileIndicesAbove(minLimboIndex, columns[i]);

			List<int> occupied = new List<int>();

			//int obstacleCountAboveIndex = ;
			//bool obstacleOffset = RequiresObstacleOffset(minLimboIndex, out obstacles);

			
			//Request move tiles to new index location (offset of columns[column].Count * xTiles)
			for(int j = 0; j < tilesAboveIndex.Count; j++)
			{
				if (isObstacle(tilesAboveIndex[j]))
					continue;
				
				int aboveIndex = tiles[tilesAboveIndex[j]].index;
				int obstacleCountAboveIndex = GetObstacleCountAbove(minLimboIndex, aboveIndex);
				int totalBelow = TotalBelowInColumn(aboveIndex, columns[i]) + obstacleCountAboveIndex;
				int targetIndex = aboveIndex - (totalBelow * xTiles);

				while (isObstacle(targetIndex))
					targetIndex += xTiles;

				while (ContainsTarget(requests, targetIndex))
					targetIndex += xTiles;

				while (isObstacle(targetIndex))
					targetIndex += xTiles;

				//Debug.LogFormat("Tile Above Index [{0}], total below {1}, target index [{2}], obstacle COunt: {3}", aboveIndex, totalBelow, targetIndex, obstacleCountAboveIndex);


				requests.Add(new MoveRequest()
				{
					//FIXME The amount of tiles moving needs to be the amount below, not just count
					tile = tiles[tilesAboveIndex[j]],
					//targetIndex = tiles[above[j]].index - (columns[i].Count * xTiles)
					targetIndex = targetIndex
				});
			}

			occupied = new List<int>();

			//Request move limbo tiles to now vacant positions
			for (int j = 0; j < columns[i].Count; j++)
			{
				if (isObstacle(columns[i][j]))
					continue;

				tiles[columns[i][j]].transform.position = OffsetAboveColumn(column: i, offset: j + 1);
				tiles[columns[i][j]].SetColor(Random.Range(0, colorProfile.Length));

				int columnTopIndex = (i + (xTiles * (yTiles - 1)));
				int verticalOffset = ((columns[i].Count) * xTiles);
				verticalOffset -= xTiles * (j + 1);

				int targetIndex = columnTopIndex - verticalOffset;

				while (isObstacle(targetIndex))
					targetIndex += xTiles;

				requests.Add(new MoveRequest()
				{
					tile = tiles[columns[i][j]],
					targetIndex = targetIndex
				});
			}
		}

		float moveT = 0f;
		List<Vector2> startPositions = new List<Vector2>();

		//Get all of the start positions for smooth lerping
		for (int i = 0; i < requests.Count; i++)
		{
			startPositions.Add(requests[i].tile.transform.position);
		}

		//Move all tiles that have been requested to be moved
		while (moveT < 1f)
		{
			for(int i = 0; i < requests.Count; i++)
			{
				requests[i].tile.transform.position = Vector2.Lerp(
					startPositions[i],
					tileLocations[requests[i].targetIndex].location, fallCurve.Evaluate(moveT));
			}

			//TODO Need to add a move multiplier here, Maybe also relative to fall distance
			moveT += Time.deltaTime * fallSpeed;

			yield return null;
		}

		//Set new indexes for moved tiles
		for (int i = 0; i < requests.Count; i++)
		{
			requests[i].tile.transform.position = tileLocations[requests[i].targetIndex].location;
			SetNewIndex(requests[i].tile, newIndex: requests[i].targetIndex);
		}

		coroutineActive = false;

		//yield return null;

		CheckFallingMatch(requests);
	}

	#endregion //Coroutines

	#region Obstacles


	bool isObstacle(int index)
	{
		try
		{
			return tiles[index].isObstacle;
		}
		catch(System.Exception e)
		{
			Debug.LogError("Error at Index: " + index);
			throw e;
		}
	}

	/// <summary>
	/// Finds total amount in list that is less than value
	/// </summary>
	/// <param name="value"></param>
	/// <param name="values"></param>
	/// <returns></returns>
	private int ObstaclesInColumn(List<int> values)
	{
		int count = 0;
		for (int i = 0; i < values.Count; i++)
		{
			if (tiles[values[i]].isObstacle)
				count++;
		}

		return count;
	}

	/// <summary>
	/// Returns count of obstacles above index
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	private int GetObstacleCountAbove(int minIndex, int maxIndex)
	{
		int count = 0;
		int index = minIndex;
		//List<int> tilesAbove = new List<int>();
		while (true)
		{
			index += DirectionToInt(DIRECTION.UP);

			if (index >= maxIndex || index >= tiles.Length)
				return count;

			if(isObstacle(index))
				count++;
		}
	}

	///// <summary>
	///// Determines if the column the current index is in will require using Obstacle offsets to account of spaces
	///// that the obstcales occupy.
	///// </summary>
	///// <param name="currentIndex"></param>
	///// <param name="count"></param>
	///// <returns></returns>
	//private bool RequiresObstacleOffset(int currentIndex, out int count)
	//{
	//	var column = GetWholeColumn(currentIndex);
	//	count = ObstaclesInColumn(column);
	//
	//	if (count == 0)
	//		return false;
	//
	//	Debug.LogFormat("Current Index[{0}] Smallest Obstacle[{1}]", currentIndex, SmallestObstacleIndex(column));
	//
	//	return currentIndex < SmallestObstacleIndex(column);
	//
	//}
	//
	///// <summary>
	///// Returns the min index of the obstacle in the column
	///// </summary>
	///// <param name="column"></param>
	///// <returns></returns>
	//private int SmallestObstacleIndex(List<int> column)
	//{
	//	int smallest = column.Max();
	//	for (int i = 0; i < column.Count; i++)
	//	{
	//		if (isObstacle(column[i]) && column[i] < smallest)
	//			smallest = column[i];
	//	}
	//
	//	return smallest;
	//}


	#endregion //Obstacles

	#region Static Functions

	/// <summary>
	/// Finds the closest tile based on MousePosition. Searches through all tiles to find match if any. Returns bool to indicate
	/// if there's a match, and if so, will out the index of matched tile
	/// </summary>
	/// <param name="Tiles"></param>
	/// <param name="thresholdRadius"></param>
	/// <param name="mousePosition"></param>
	/// <param name="index"></param>
	/// <returns></returns>
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

	/// <summary>
	/// Converts a mouse drag to a direction.
	/// </summary>
	/// <param name="dragDirection"></param>
	/// <returns></returns>
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

	/// <summary>
	/// Used to calculate the direction of the mouse drag
	/// </summary>
	/// <param name="dragDirection"></param>
	/// <returns></returns>
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

	/// <summary>
	/// Finds total amount in list that is less than value
	/// </summary>
	/// <param name="value"></param>
	/// <param name="values"></param>
	/// <returns></returns>
	private static int TotalBelowInColumn(int value, List<int> values)
	{
		int count = 0;
		for (int i = 0; i < values.Count; i++)
		{
			if (values[i] < value)
				count++;
		}

		return count;
	}

	private static bool ContainsTarget(List<MoveRequest> requests, int targetIndex)
	{
		for(int i = 0; i < requests.Count; i++)
		{
			if (requests[i].targetIndex == targetIndex)
				return true;
		}

		return false;
	}

	



	#endregion //Static Functions

	#region Extra Classes

	private class MoveRequest
	{
		public Tile tile;
		public int targetIndex;
	}

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

		public bool isObstacle { get; private set; }

		public POWERUP powerUp { get; private set; }
		public int color { get; private set; }
		public int index { get; private set; }
		public Transform transform { get; private set; }

		private SpriteRenderer mRenderer;
		private TextMeshPro textMesh;

		public Tile(int Color, int Index, Transform Transform)
		{
			transform = Transform;
			mRenderer = transform.GetComponent<SpriteRenderer>();
			textMesh = transform.GetComponentInChildren<TextMeshPro>();

			textMesh.text = string.Empty;

			SetIndex(Index);
			SetColor(Color);

		}

		public Tile(int index,Transform Transform, Sprite sprite)
		{
			isObstacle = true;
			transform = Transform;
			mRenderer = transform.GetComponent<SpriteRenderer>();
			textMesh = transform.GetComponentInChildren<TextMeshPro>();

			mRenderer.sprite = sprite;
			mRenderer.color = Color.gray;
			textMesh.text = string.Empty;

		}

		public void SetColor(int _color)
		{
			color = _color;
			mRenderer.color = GameManager.Instance.colorProfile.Colors[color];

			//TODO Need to calculate the chance being a power-up
			if (Random.value <= GameManager.Instance.powerupProfile.PowerupChance)
				powerUp = GameManager.Instance.powerupProfile.GeneratePowerUp();
			else
				powerUp = POWERUP.NONE;

			textMesh.text = PowerUpToString(powerUp);
		}

		public void SetIndex(int index)
		{
			this.index = index;
		}

		private static string PowerUpToString(POWERUP power)
		{
			switch (power)
			{
				case POWERUP.NONE:
					return string.Empty;
				case POWERUP.LINE:
					return "-";
				case POWERUP.CROSS:
					return "+";
				case POWERUP.COLOR:
					return "#";
				default:
					throw new System.NotImplementedException(power + " not yet added");
			}
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
