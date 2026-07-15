using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.NPCs.Enemy.Elemental;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// Turns a hit into damage-over-time. The sole consumer of
	/// <see cref="CombatEffectStats.ElementalConversion"/>, deliberately mirroring
	/// <see cref="LifeLeechApplier"/>'s relationship to LifeLeechPercent.
	///
	/// One consumer matters here for the same reason it did for leech: the elemental masteries,
	/// Plaguebearer and (Phase 3) item modifiers all grant conversion, and if each paid out its own
	/// DoT they would each get their own duration, their own stack cap and their own resistance
	/// lookup, and none would see the others. Pooling first and paying out once means a rank of
	/// Immolation and a rolled "+4% of damage as Fire" simply add up.
	///
	/// The DoT is <b>added on top</b> of the hit — the hit keeps its full damage. Conversion is a
	/// straight increase, not a trade.
	///
	/// <b>Mod-dealt AoE does not convert.</b> Detonate, Hellfire and Corrupted Blood all damage
	/// enemies with <c>NPC.SimpleStrikeNPC</c>, which calls <c>NPC.StrikeNPC</c> directly.
	/// <c>ModPlayer.OnHitNPC</c> is dispatched only from <c>Player.StrikeNPCDirect</c>, so those
	/// explosions never reach this hook and never apply DoT. Verified against the assembly — only
	/// real weapon and projectile hits convert. (Hellfire's <c>exploding</c> re-entrancy guard is
	/// dead code for the same reason; its comment claiming SimpleStrikeNPC "runs the hit pipeline
	/// again" is wrong.)
	/// </summary>
	public class ElementalDotApplier : ModPlayer
	{
		/// <summary>
		/// Base DoT duration in seconds, before AilmentEffect.
		///
		/// This is half of the tuning identity: for any weapon under the stack cap, a DoT's share of
		/// your damage is <c>conversion x duration</c>, independent of attack speed. Lengthening
		/// this is a flat damage buff to every element at once — it does not merely spread the same
		/// damage thinner, because instances are a rate rather than a pool.
		/// </summary>
		public const float BaseDurationSeconds = 4f;

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// lifeMax > 5 skips critters and the like, matching LifeLeechApplier.IsLeechableTarget.
			if (damageDone <= 0 || target.friendly || target.lifeMax <= 5)
				return;

			CombatEffectStats effects = CombatEffectStats.Get(Player);

			// AilmentEffect scales duration, which under a per-second DoT is a real damage increase
			// rather than the cosmetic one it used to be when vanilla owned the (flat, unscaling)
			// damage and duration was all AddBuff could give us.
			float duration = BaseDurationSeconds * (1f + effects.AilmentPercent / 100f);
			if (duration <= 0f)
				return;

			int buffTicks = (int)(duration * 60f);
			ElementalDotNPC dots = null;

			for (int e = 0; e < DamageElementInfo.Count; e++)
			{
				// Pool is whole percents; everything below wants a fraction.
				float conversion = effects.ElementalConversion[e] / 100f;
				if (conversion <= 0f)
					continue;

				var element = (DamageElement)e;

				// Penetration is 0 until Phase 3 gives items a way to roll it.
				float resistance = ElementalResistance.Get(target, element, penetration: 0f);

				// damageDone, not hit.Damage: post-defense and post-crit, so the DoT inherits every
				// bonus that shaped the hit without re-applying any of them. It is also clamped to
				// the target's remaining life, so a killing blow yields a small DoT — irrelevant,
				// since the target is already dead. Matches LifeLeechApplier and Hellfire.
				float damagePerSecond = damageDone * conversion * (1f - resistance);
				if (damagePerSecond <= 0f)
					continue;

				if (dots == null)
					dots = target.GetGlobalNPC<ElementalDotNPC>();

				dots.AddInstance(element, damagePerSecond, duration);

				// Icon only — we deal the damage ourselves. This silently no-ops on an enemy that is
				// buffImmune to the carrier, which costs us the icon but not the DoT: the instance
				// above lives in our own GlobalNPC. Phase 2 turns buffImmune into a resistance
				// source, so such an enemy will take reduced (never zero) elemental damage while
				// showing no icon for it.
				target.AddBuff(DamageElementInfo.BuffIdFor(element), buffTicks);
			}
		}
	}
}
