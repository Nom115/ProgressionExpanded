namespace ProgressionExpanded.Items.Modifiers
{
	/// <summary>
	/// A modifier as it actually sits on an item instance: which template, which tier, the concrete
	/// rolled value, and a self-contained copy of the stat key + keyword so the per-frame apply
	/// hot-path never has to resolve the JSON definition. Serialized (as a JSON list) into item save
	/// data by <c>ItemModifierSystem</c>.
	/// </summary>
	public class RolledModifier
	{
		/// <summary>Pool the modifier was rolled from (so we can resolve its DisplayName later).</summary>
		public string PoolId { get; set; }

		/// <summary>The <see cref="ItemModifierDefinition.ModId"/> this was rolled from.</summary>
		public string ModId { get; set; }

		/// <summary>Stat this affects (copied from the definition at roll time).</summary>
		public string StatKey { get; set; }

		/// <summary>"increased" / "more" / "flat" (copied from the definition at roll time).</summary>
		public string Keyword { get; set; }

		/// <summary>1-based tier number.</summary>
		public int Tier { get; set; }

		/// <summary>The concrete rolled magnitude (percent number for increased/more, raw for flat).</summary>
		public float Value { get; set; }

		public RolledModifier() { }

		public RolledModifier(string poolId, string modId, string statKey, string keyword, int tier, float value)
		{
			PoolId = poolId;
			ModId = modId;
			StatKey = statKey;
			Keyword = keyword;
			Tier = tier;
			Value = value;
		}

		/// <summary>Deep copy (used by GlobalItem.Clone so stacks/drops don't alias).</summary>
		public RolledModifier Copy() => new RolledModifier(PoolId, ModId, StatKey, Keyword, Tier, Value);
	}
}
