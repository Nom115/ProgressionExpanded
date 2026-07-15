using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ProgressionExpanded.Src.Levels.PlayerSystems.Talents;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Attributes
{
	public enum PlayerAttribute
	{
		Strength,
		Dexterity,
		Intellect
	}

	/// <summary>
	/// The three levelling attributes. Each point costs one passive point from the shared pool that
	/// minor talents also draw on, so investing here is always a talent rank given up.
	/// </summary>
	public class AttributeManager : ModPlayer
	{
		public const int HealthPerStrength = 2;
		public const int ManaPerIntellect = 2;
		public const float SpeedPerDexterity = 0.01f; // 1% per point

		private const string StrengthKey = "Strength";
		private const string DexterityKey = "Dexterity";
		private const string IntellectKey = "Intellect";

		private int strength;
		private int dexterity;
		private int intellect;

		// Attributes live in this ModPlayer's own TagCompound rather than PlayerDataManager, so
		// LoadData populates the fields straight from the save. PlayerDataManager.Initialize()
		// clears its dictionaries and runs BEFORE LoadData, which is the entire reason the tree
		// needs a deferred re-read; keeping our state out of it means that hazard cannot occur
		// here at all. Same reasoning as PassivePointManager — see its note above SaveData.
		public override void Initialize()
		{
			strength = 0;
			dexterity = 0;
			intellect = 0;
		}

		public override void SaveData(TagCompound tag)
		{
			tag[StrengthKey] = strength;
			tag[DexterityKey] = dexterity;
			tag[IntellectKey] = intellect;
		}

		public override void LoadData(TagCompound tag)
		{
			strength = tag.GetInt(StrengthKey);
			dexterity = tag.GetInt(DexterityKey);
			intellect = tag.GetInt(IntellectKey);
		}

		#region Queries

		public int Get(PlayerAttribute attribute)
		{
			switch (attribute)
			{
				case PlayerAttribute.Strength: return strength;
				case PlayerAttribute.Dexterity: return dexterity;
				case PlayerAttribute.Intellect: return intellect;
				default: return 0;
			}
		}

		/// <summary>
		/// Total passive points sunk into attributes. PassivePointManager sums this with the tree's
		/// spend to reconcile the economy on load — keep the cost curve behind this method rather
		/// than inlining str+dex+int at call sites, or a future non-linear cost would silently
		/// disagree with what SpendPoints actually charged.
		/// </summary>
		public int GetTotalSpentOnAttributes()
		{
			return strength + dexterity + intellect;
		}

		#endregion

		#region Mutation

		/// <summary>
		/// Spend one passive point on an attribute. Returns false if the player can't afford it.
		/// </summary>
		public bool TryInvest(PlayerAttribute attribute, int amount = 1)
		{
			if (amount <= 0)
				return false;

			if (!Player.GetModPlayer<PassivePointManager>().SpendPoints(amount))
				return false;

			Add(attribute, amount);
			return true;
		}

		/// <summary>
		/// Take a point back out of an attribute and return it to the pool.
		/// </summary>
		public bool TryRefund(PlayerAttribute attribute, int amount = 1)
		{
			if (amount <= 0 || Get(attribute) < amount)
				return false;

			Add(attribute, -amount);
			Player.GetModPlayer<PassivePointManager>().RefundPoints(amount);
			return true;
		}

		/// <summary>
		/// Refund every invested attribute point back to the pool.
		/// </summary>
		public void RefundAll()
		{
			int total = GetTotalSpentOnAttributes();
			if (total <= 0)
				return;

			WipeAll();
			Player.GetModPlayer<PassivePointManager>().RefundPoints(total);
		}

		/// <summary>
		/// Zero the attributes without touching the point economy. Only for the schema migration,
		/// which reconstructs the economy from scratch afterwards — anything else wants RefundAll.
		/// </summary>
		public void WipeAll()
		{
			strength = 0;
			dexterity = 0;
			intellect = 0;
		}

		private void Add(PlayerAttribute attribute, int amount)
		{
			switch (attribute)
			{
				case PlayerAttribute.Strength: strength += amount; break;
				case PlayerAttribute.Dexterity: dexterity += amount; break;
				case PlayerAttribute.Intellect: intellect += amount; break;
			}
		}

		#endregion

		#region Application

		public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
		{
			health = StatModifier.Default;
			mana = StatModifier.Default;

			// Base, not Flat. StatModifier resolves as (statMax + Base) * Additive + Flat, so Base
			// sits inside the multipliers and Flat sits outside them. Strength must be inside: the
			// point of Fortitude / Royal Jelly granting % maximum life is that they scale the life
			// Strength bought. Using Flat here would quietly exclude every Strength point from every
			// % life bonus in the game.
			health.Base += HealthPerStrength * strength;

			// Juggernaut states that Intellect grants no mana. Asked here rather than cancelled
			// afterwards: suppression is derived from the player's picks, so the answer is stable at
			// any point in the frame and does not depend on which ModPlayer happens to run first.
			if (!TalentPlayer.Get(Player).Suppresses(TalentSuppression.ManaFromIntellect))
				mana.Base += ManaPerIntellect * intellect;
		}

		public override void PostUpdateMiscEffects()
		{
			ApplyDexterity();
		}

		/// <summary>
		/// Dexterity's attack-speed half. Ranged is handled in ModifyShootStats instead — vanilla has
		/// no player-wide projectile-velocity stat.
		/// </summary>
		public void ApplyDexterity()
		{
			if (dexterity <= 0)
				return;

			// Juggernaut states that attack speed cannot be increased, and it means it: Dexterity is
			// a dead stat for that build, which is exactly the cost that makes the pinnacle a real
			// decision rather than a free +100% life.
			if (TalentPlayer.Get(Player).Suppresses(TalentSuppression.AttackSpeedIncreases))
				return;

			float bonus = SpeedPerDexterity * dexterity;
			Player.GetAttackSpeed(DamageClass.Melee) += bonus;
			Player.GetAttackSpeed(DamageClass.Magic) += bonus;
		}

		public override void ModifyShootStats(Item item, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
		{
			if (dexterity <= 0 || item == null)
				return;

			if (!item.DamageType.CountsAsClass(DamageClass.Ranged))
				return;

			velocity *= 1f + SpeedPerDexterity * dexterity;
		}

		#endregion

		public static AttributeManager Get(Player player)
		{
			return player.GetModPlayer<AttributeManager>();
		}
	}
}
