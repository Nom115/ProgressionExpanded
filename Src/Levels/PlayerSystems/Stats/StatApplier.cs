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
				// LifeRegenPercent is deliberately NOT here — see ApplyLifeRegenPercent below. It is the
				// third category of key, alongside the ModifyMaxStats-only ones: a key whose only correct
				// home is one specific hook, so routing it through this switch is what broke it.

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

		/// <summary>
		/// The LifeRegenPercent key. <b>Call this from UpdateLifeRegen and nowhere else</b> — it is not
		/// in ApplyPercent's switch, and that is the fix rather than an inconsistency.
		///
		/// <b>What was wrong (fixed 2026-07-17).</b> It used to be an ordinary ApplyPercent case, which
		/// meant it ran from PostUpdateMiscEffects, and it was very nearly dead code twice over:
		/// - <b>Wrong hook.</b> PostUpdateMiscEffects fires immediately BEFORE Player.UpdateLifeRegen
		///   (Player.cs:24940/24941), so at that point lifeRegen holds none of the campfire (:17931),
		///   heart lantern (:17934) or gear/buff regen it is supposed to scale. It multiplied an almost
		///   empty number. This is the same wrong-hook bug that made Vengeance's regen line dead until
		///   2026-07-16 and that Juggernaut's potion burst was written to avoid.
		/// - <b>Wrong ORDER, which is the subtler half.</b> tML autoloads ModPlayers sorted by
		///   type.FullName (Mod.cs:569, StringComparer.InvariantCulture), so
		///   ...PlayerSystems.PassivePoints.PassiveTreeManager always registers before
		///   ...PlayerSystems.Talents.TalentPlayer — P before T. Every hook list inherits that order, so
		///   Second Wind fired before TalentPlayer had contributed anything at all, and could not scale
		///   Stagger's +8/s or Juggernaut's Strength regen no matter what number it was given.
		///
		/// From UpdateLifeRegen both problems go away and neither can come back by accident: the hook
		/// runs after every flat contributor, ours and vanilla's alike, so there is nothing left to be
		/// ordered against. Two contributors both multiplying is order-independent anyway.
		///
		/// <b>The lifeRegen > 0 guard is load-bearing, not defensive.</b> lifeRegen is NEGATIVE while a
		/// vanilla DoT ticks — the debuff block at :17914 has already run by the time we get here — so an
		/// unguarded multiply would make every Bleeding/Poisoned/On Fire tick proportionally MORE lethal:
		/// a life-regen bonus that kills you. Same guard, same reason, as RoyalJelly and Fleshless.
		/// </summary>
		public static void ApplyLifeRegenPercent(Player player, float value)
		{
			if (value == 0f || player.lifeRegen <= 0)
				return;

			player.lifeRegen = (int)(player.lifeRegen * (1f + value));
		}
	}
}
