using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerSystems;

namespace ProgressionExpanded.Items.Modifiers
{
	/// <summary>
	/// Applies rolled item modifiers to the player / weapon, implementing the PoE keyword math:
	/// <list type="bullet">
	/// <item><b>increased</b> — feeds the vanilla additive bucket (all "increased" of a stat sum,
	/// then multiply the base once).</item>
	/// <item><b>more</b> — a separate compounding multiplier (vanilla multiplicative bucket).</item>
	/// <item><b>flat</b> — a flat add.</item>
	/// </list>
	/// Percent-keyword values are stored as whole percent numbers in JSON (8 = +8%), so they are
	/// divided by 100 here. Stat-key → Player-field mapping mirrors
	/// <c>PassiveTreeManager.ApplyFlat/PercentBonusToStat</c> so item mods stack consistently with
	/// the passive tree.
	/// </summary>
	public static class ItemModifierApplier
	{
		private const string Increased = "increased";
		private const string More = "more";
		private const string Flat = "flat";

		private static string Norm(string keyword) => keyword?.ToLowerInvariant() ?? Increased;

		#region Equip (armor + accessory) — player-wide stats while worn

		/// <summary>
		/// Apply an equipped item's modifiers to the player. Defense is handled base-relative off the
		/// item's own <c>item.defense</c> (the anti-"god-rolled-copper" safeguard); every other stat
		/// is player-wide.
		/// </summary>
		public static void ApplyEquipMods(Player player, Item item, List<RolledModifier> mods)
		{
			if (mods == null || mods.Count == 0) return;

			// Gather defense contributions to combine before touching statDefense.
			float increasedDefPct = 0f;   // summed "% increased Armour"
			float moreDefMul = 1f;        // product of "% more Armour"
			int flatDefense = 0;          // flat "+X Defense" (and any flat ArmorIncrease)

			foreach (var mod in mods)
			{
				string key = mod.StatKey;
				string kw = Norm(mod.Keyword);

				switch (key)
				{
					case "ArmorIncrease":
						if (kw == More) moreDefMul *= 1f + mod.Value / 100f;
						else if (kw == Flat) flatDefense += (int)mod.Value;
						else increasedDefPct += mod.Value;
						break;

					case "Defense":
						flatDefense += (int)mod.Value;
						break;

					default:
						ApplyPlayerStat(player, key, kw, mod.Value);
						break;
				}
			}

			// Percent armour scales off the item's own base defense (keeps weak bases weak).
			if (item.defense > 0 && (increasedDefPct != 0f || moreDefMul != 1f))
			{
				int scaled = (int)Math.Round(item.defense * (1f + increasedDefPct / 100f) * moreDefMul);
				player.statDefense += scaled - item.defense;
			}

			player.statDefense += flatDefense;
		}

		#endregion

		#region Held weapon — the player-side-only weapon stats (attack speed, mana, armour pen)

		public static void ApplyHeldWeaponStats(Player player, Item item, List<RolledModifier> mods)
		{
			if (mods == null || mods.Count == 0) return;

			foreach (var mod in mods)
			{
				switch (mod.StatKey)
				{
					case "AttackSpeed":
						player.GetAttackSpeed(item.DamageType) += mod.Value / 100f;
						break;
					case "ManaEfficiency":
						player.manaCost -= mod.Value / 100f;
						if (player.manaCost < 0f) player.manaCost = 0f;
						break;
					case "ArmorPenetration":
						player.GetArmorPenetration(item.DamageType) += (int)mod.Value;
						break;

					// A held weapon's "effect" stats feed the shared CombatEffectStats accumulator so
					// its leech/ailment/area can amplify notable passives while the weapon is held.
					case "LifeLeech":
						CombatEffectStats.Get(player).LifeLeechPercent += mod.Value;
						break;
					case "Healing":
						CombatEffectStats.Get(player).HealingPercent += mod.Value;
						break;
					case "AilmentEffect":
						CombatEffectStats.Get(player).AilmentPercent += mod.Value;
						break;
					case "AreaEffect":
						CombatEffectStats.Get(player).AreaPercent += mod.Value;
						break;
				}
			}
		}

		#endregion

		#region Weapon per-hit hooks

		public static void ApplyWeaponDamage(List<RolledModifier> mods, ref StatModifier damage)
		{
			if (mods == null) return;
			foreach (var mod in mods)
			{
				if (!IsDamageKey(mod.StatKey)) continue;
				string kw = Norm(mod.Keyword);
				if (kw == Flat) damage.Flat += mod.Value;
				else if (kw == More) damage *= 1f + mod.Value / 100f;
				else damage += mod.Value / 100f; // increased -> additive bucket
			}
		}

		public static void ApplyWeaponCrit(List<RolledModifier> mods, ref float crit)
		{
			if (mods == null) return;
			foreach (var mod in mods)
			{
				if (mod.StatKey == "CritChance")
					crit += mod.Value; // crit chance is inherently additive percentage points
			}
		}

		public static void ApplyWeaponKnockback(List<RolledModifier> mods, ref StatModifier knockback)
		{
			if (mods == null) return;
			foreach (var mod in mods)
			{
				if (mod.StatKey != "Knockback") continue;
				string kw = Norm(mod.Keyword);
				if (kw == Flat) knockback.Flat += mod.Value;
				else if (kw == More) knockback *= 1f + mod.Value / 100f;
				else knockback += mod.Value / 100f;
			}
		}

		private static bool IsDamageKey(string key)
		{
			return key == "MeleeDamage" || key == "RangedDamage" || key == "MagicDamage"
				|| key == "SummonDamage" || key == "GenericDamage";
		}

		#endregion

		#region Player-wide stat switch (mirrors the passive tree)

		private static void ApplyPlayerStat(Player player, string key, string kw, float value)
		{
			switch (key)
			{
				case "MeleeDamage": ApplyDamage(player, DamageClass.Melee, kw, value); break;
				case "RangedDamage": ApplyDamage(player, DamageClass.Ranged, kw, value); break;
				case "MagicDamage": ApplyDamage(player, DamageClass.Magic, kw, value); break;
				case "SummonDamage": ApplyDamage(player, DamageClass.Summon, kw, value); break;
				case "GenericDamage": ApplyDamage(player, DamageClass.Generic, kw, value); break;

				case "CritChance": player.GetCritChance(DamageClass.Generic) += (int)value; break;
				case "ArmorPenetration": player.GetArmorPenetration(DamageClass.Generic) += (int)value; break;
				case "MinionSlots": player.maxMinions += (int)value; break;
				case "MaxHealth": player.statLifeMax2 += (int)value; break;
				case "LifeRegen": player.lifeRegen += (int)value; break;

				case "MovementSpeed":
					if (kw == More) player.moveSpeed *= 1f + value / 100f;
					else player.moveSpeed += value / 100f;
					break;

				case "AttackSpeed":
					player.GetAttackSpeed(DamageClass.Generic) += value / 100f;
					break;

				case "ManaEfficiency":
					player.manaCost -= value / 100f;
					if (player.manaCost < 0f) player.manaCost = 0f;
					break;

				case "Endurance":
					player.endurance += value / 100f;
					if (player.endurance > 1f) player.endurance = 1f;
					break;

				case "Knockback":
					player.GetKnockback(DamageClass.Generic) += value / 100f;
					break;

				// Shared "effect" stats — feed the CombatEffectStats accumulator that notable
				// passives read (leech/healing/ailment/area). Value is a whole percent; the
				// consumer divides by 100. (armor/accessory reach here via ApplyEquipMods' default.)
				case "LifeLeech":
					CombatEffectStats.Get(player).LifeLeechPercent += value;
					break;
				case "Healing":
					CombatEffectStats.Get(player).HealingPercent += value;
					break;
				case "AilmentEffect":
					CombatEffectStats.Get(player).AilmentPercent += value;
					break;
				case "AreaEffect":
					CombatEffectStats.Get(player).AreaPercent += value;
					break;

				// Unknown keys are silently ignored (same policy as the passive tree).
			}
		}

		private static void ApplyDamage(Player player, DamageClass dc, string kw, float value)
		{
			if (kw == Flat) player.GetDamage(dc).Flat += value;
			else if (kw == More) player.GetDamage(dc) *= 1f + value / 100f;
			else player.GetDamage(dc) += value / 100f; // increased -> additive bucket
		}

		#endregion
	}
}
