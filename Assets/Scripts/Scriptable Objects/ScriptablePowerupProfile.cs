using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "Chance Profile", menuName = "Chance/Chance Profile", order = 1)]
public class ScriptablePowerupProfile : ScriptableObject
{
	[System.Serializable]
	public class Items
	{
		public float Probability;
		public GameManager.POWERUP Item;
	}

	[SerializeField, Range(0f, 1f)]
	public float PowerupChance = 0.025f;

	[Header("Type Chance"), SerializeField]
	List<Items> initial = new List<Items>();

	List<Items> converted;

	//[Header("Type Chance"), SerializeField]
	//public float LineChance;
	//[SerializeField]
	//public float CrossChance;
	//[SerializeField]
	//public float ColorChance;

	public void Init()
	{
		 converted = new List<Items>(initial.Count);
		float sum = 0.0f;
		foreach (var item in initial.Take(initial.Count - 1))
		{
			sum += item.Probability;
			converted.Add(new Items { Probability = sum, Item = item.Item });
		}
		converted.Add(new Items { Probability = 1.0f, Item = initial.Last().Item });
	}


	public GameManager.POWERUP GeneratePowerUp()
	{
		float value = Random.value;

		var selected = converted.SkipWhile(i => i.Probability < value).First();

		return selected.Item;

	}

}
