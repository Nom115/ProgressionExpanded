using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerSystems.Talents;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Stats
{
	/// <summary>
	/// The one place a stat-name string turns into an effect on the player.
	///
	/// Shared by the mastery tree and by talents that declare plain stat bonuses, so a key means the
	/// same thing wherever it appears. Bonuses are applied fresh every frame rather than written
	/// into saved state — they are derived from allocations, and persisting them separately is how
	/// you get bonuses that survive a respec.
	///
	/// Note MaxHealth/MaxMana are NOT here. They go through ModifyMaxStats, which is the only
	/// channel where flat and percentage max-life compose correctly.
	/// </summary>
	public static class StatApplier
	{
		public static void ApplyFlat(Player player, string statName, float value)
		{
			switch (statName)
			{
				// Damage
				case "MeleeDamage":
					player.GetDamage(DamageClass.Melee).Flat += value;
					break;
				case "MagicDamage":
					player.GetDamage(DamageClass.Magic).Flat += value;
					break;
				case "RangedDamage":
					player.GetDamage(DamageClass.Ranged).Flat += value;
					break;
				case "SummonDamage":
					player.GetDamage(DamageClass.Summon).Flat += value;
					break;
				case "GenericDamage":
					player.GetDamage(DamageClass.Generic).Flat += value;
					break;
				case "CritChance":
					player.GetCritChance(DamageClass.Generic) += (int)value;
					break;
				case "ArmorPenetration":
					player.GetArmorPenetration(DamageClass.Generic) += (int)value;
					break;
				case "MinionSlots":
					player.maxMinions += (int)value;
					break;

				// Defence
				case "Defense":
					player.statDefense += (int)value;
					break;
				case "LifeRegen":
					player.lifeRegen += (int)value;
					break;

				// Tertiary
				case "MovementSpeed":
					player.moveSpeed += value;
					break;

				// Shared "effect" stats, read by notable/talent behaviours rather than mapping to a
				// vanilla field. Flat nodes state these as whole percents; percent nodes state them
				// as fractions and convert below. Same destination, two conventions — see ApplyPercent.
				case "LifeLeech":
					player.GetModPlayer<CombatEffectStats>().LifeLeechPercent += value;
					break;
				case "Healing":
					player.GetModPlayer<CombatEffectStats>().HealingPercent += value;
					break;
				case "AilmentEffect":
					player.GetModPlayer<CombatEffectStats>().AilmentPercent += value;
					break;
				case "AreaEffect":
					player.GetModPlayer<CombatEffectStats>().AreaPercent += value;
					break;
			}
		}

		public static void ApplyPercent(Player player, string statName, float value)
		{
			switch (statName)
			{
				// Damage
				case "MeleeDamage":
					player.GetDamage(DamageClass.Melee) *= 1f + value;
					break;
				case "MagicDamage":
					player.GetDamage(DamageClass.Magic) *= 1f + value;
					break;
				case "RangedDamage":
					player.GetDamage(DamageClass.Ranged) *= 1f + value;
					break;
				case "SummonDamage":
					player.GetDamage(DamageClass.Summon) *= 1f + value;
					break;
				case "GenericDamage":
					player.GetDamage(DamageClass.Generic) *= 1f + value;
					break;
				case "AttackSpeed":
					// Asked, not told: Juggernaut states that attack speed cannot be increased, and
					// every contributor checks that itself. The alternative — Juggernaut setting a
					// flag and clamping afterwards — would depend on this running first, which is
					// only true by accident of how ModPlayers happen to be ordered.
					if (!TalentPlayer.Get(player).Suppresses(TalentSuppression.AttackSpeedIncreases))
						player.GetAttackSpeed(DamageClass.Generic) += value;
					break;
				case "ManaEfficiency":
					player.manaCost -= value;
					if (player.manaCost < 0f) player.manaCost = 0f;
					break;

				// Defence
				case "Endurance":
					player.endurance += value;
					if (player.endurance > 1f) player.endurance = 1f;
					break;
				case "DefensePercent":
					// statDefense is a Player.DefenseStat, not an int — it collects adds and
					// multiplies separately and resolves them correctly, so this composes with armor
					// regardless of what order contributions arrive in.
					player.statDefense *= 1f + value;
					break;
				case "LifeRegenPercent":
					// Only scale regeneration, never degeneration. lifeRegen goes negative while a
					// DoT ticks, so multiplying unconditionally would make every Bleeding/Poison/
					// On Fire tick proportionally MORE lethal — a life-regen bonus that hurts you.
					if (player.lifeRegen > 0)
						player.lifeRegen = (int)(player.lifeRegen * (1f + value));
					break;

				// Tertiary
				case "Knockback":
					player.GetKnockback(DamageClass.Generic) += value;
					break;
				case "MovementSpeed":
					player.moveSpeed *= 1f + value;
					break;

				// The tree states percents as fractions (0.10 = 10%); CombatEffectStats accumulates
				// whole percents. See the flat cases above for the other half of this convention.
				case "LifeLeech":
					player.GetModPlayer<CombatEffectStats>().LifeLeechPercent += value * 100f;
					break;
				case "Healing":
					player.GetModPlayer<CombatEffectStats>().HealingPercent += value * 100f;
					break;
				case "AilmentEffect":
					player.GetModPlayer<CombatEffectStats>().AilmentPercent += value * 100f;
					break;
				case "AreaEffect":
					player.GetModPlayer<CombatEffectStats>().AreaPercent += value * 100f;
					break;
			}
		}
	}
}
