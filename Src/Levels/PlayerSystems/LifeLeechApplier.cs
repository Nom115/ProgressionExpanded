using System;
using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// The single consumer of <see cref="CombatEffectStats.LifeLeechPercent"/> — every source of
	/// "heal for a share of the damage you deal" pools into that one number and is paid out here.
	///
	/// This exists because leech used to be paid out inside <c>Bloodthirst</c>, which made the
	/// LifeLeech stat key a lie: an item that rolled +4% life leech did **nothing at all** unless the
	/// player happened to have the Bloodthirst mastery allocated, because Bloodthirst was the only
	/// code that ever read the field. The stat existed, the UI showed it, and it silently did nothing.
	///
	/// Pooling also means the rate limits are stated once and cannot be dodged by stacking sources.
	/// Two leech sources previously meant two independent cooldowns and two independent per-hit caps;
	/// now they raise one fraction that is paid out by one capped, cooldowned heal.
	///
	/// Contributors add in PostUpdateMiscEffects (StatApplier, Bloodthirst) and this reads in
	/// OnHitNPC. That ordering is safe rather than lucky: tModLoader runs every ModPlayer's
	/// ResetEffects (where CombatEffectStats zeroes the field) before any PostUpdateMiscEffects, and
	/// weapon/projectile hits resolve after both.
	/// </summary>
	public class LifeLeechApplier : ModPlayer
	{
		/// <summary>~0.3s between leech procs. Without it a multi-hit weapon heals per contact point
		/// and the percentage stops meaning anything — hits per second becomes the real stat.</summary>
		public const int HealCooldownTicks = 18;

		/// <summary>
		/// Per-hit ceiling as a fraction of max life. Leech scaling with the weapon is the point, but
		/// uncapped a single big hit on a boss full-heals on its own and the cooldown stops mattering.
		///
		/// This bounds the LEECH — how much of a hit converts to life — and is applied BEFORE healing
		/// bonuses, which then multiply the capped result. So it is not an absolute ceiling on the
		/// heal: increased healing raises it, deliberately. Capping last instead would mean every
		/// point of healing above the cap silently did nothing, which is the same species of lie as a
		/// leech stat with no consumer — on your sheet, quietly discarded.
		/// </summary>
		public const float MaxLeechPerHit = 0.15f;

		private int healCooldown;

		public override void PostUpdate()
		{
			if (healCooldown > 0)
				healCooldown--;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (healCooldown > 0 || !IsLeechableTarget(target))
				return;

			if (Player.statLife >= Player.statLifeMax2)
				return;

			CombatEffectStats effects = CombatEffectStats.Get(Player);
			float leechFraction = effects.LifeLeechPercent / 100f;
			float flatBonus = effects.LifeLeechFlatBonus;
			if (leechFraction <= 0f && flatBonus <= 0f)
				return;

			// Cap the leech first, THEN amplify it. Healing bonuses are the last multiplier applied —
			// see MaxLeechPerHit — so a rolled "Healing" item genuinely raises the ceiling rather than
			// being swallowed by it.
			//
			// HealingPercent, and deliberately NOT ConsumableHealingPercent: this is the leech path, and a
			// bonus that already reaches leech through damageDone must not be counted a second time here or
			// it squares. ConsumableHealingPercent is the potions-only channel that exists for exactly that
			// reason — see CombatEffectStats.
			float leeched = damageDone * leechFraction;

			float perHitCap = Math.Max(1f, Player.statLifeMax2 * MaxLeechPerHit);
			if (leeched > perHitCap)
				leeched = perHitCap;

			// A flat on-hit heal (Vengeance's damage-taken bonus, via LifeLeechFlatBonus) rides this same
			// proc and cooldown so a fast weapon can't spam it. Added AFTER the per-hit leech cap — it is
			// NOT leech, so the % cap must not bound it — and amplified by HealingPercent alongside the
			// leech it accompanies.
			int heal = (int)Math.Round((leeched + flatBonus) * (1f + effects.HealingPercent / 100f));

			if (Heal(Player, heal))
				healCooldown = HealCooldownTicks;
		}

		/// <summary>
		/// Critters, town NPCs and target dummies are not a healing supply. Shared so every leech
		/// effect draws the line in the same place.
		/// </summary>
		public static bool IsLeechableTarget(NPC target)
		{
			return !target.friendly && target.lifeMax > 5;
		}

		/// <summary>
		/// Heal without overhealing, and show the number. Returns false if nothing was actually
		/// restored, so a caller can decline to start its cooldown on a no-op.
		///
		/// Note this writes statLife directly rather than going through vanilla's lifesteal
		/// accumulator (Player.lifeSteal), so mod leech is NOT throttled by the limiter that bounds
		/// Spectre armor and Vampire Knives. The cooldown and per-hit cap above are the only rate
		/// limits that exist — do not remove them assuming vanilla will catch it.
		/// </summary>
		public static bool Heal(Player player, int amount)
		{
			if (amount <= 0)
				return false;

			int newLife = Math.Min(player.statLifeMax2, player.statLife + amount);
			int healed = newLife - player.statLife;
			if (healed <= 0)
				return false;

			player.statLife = newLife;
			player.HealEffect(healed);
			return true;
		}
	}
}
