using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using ProgressionExpanded.Src.NPCs.Enemy.Elemental;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// World-evil row: rot, consumption, and blood that fights back.
	///
	/// Plaguebearer is a rhythmic plague: for a 3-second window every 10 seconds your hits convert a
	/// large share of your damage into all five ailments at once; between windows it lies dormant.
	/// </summary>
	public class PlaguebearerTalent : TalentBehaviour
	{
		public override string Id => "plaguebearer";
		public override string SlotKey => "evil";
		public override string DisplayName => "Plaguebearer";

		public override string Description =>
			"Every 10 seconds, enter a 3-second plague: your hits convert a large share of your damage "
			+ "into Bleeding, On Fire, Venom, Frostburn and Electrified at once. Dormant between windows.";

		private const int CycleTicks = 600;   // 10s cycle
		private const int WindowTicks = 180;  // 3s of that cycle is the active plague window

		/// <summary>
		/// Conversion granted in EVERY element while the window is open. 8% x five elements is 40% total
		/// conversion for three seconds — a strong burst — averaging ~12% over the full ten-second cycle.
		/// This replaced an always-on 3%/element plus a near-unconditional +40% damage rider: the talent
		/// landed all five debuffs on the first hit, so the "3+ debuffs" gate was met on essentially
		/// every hit and the +40% was effectively unconditional.
		/// </summary>
		private const float WindowConversionPerElement = 0.08f;

		private int cycleTimer;

		public override void OnActivate(Player player)
		{
			cycleTimer = 0;
		}

		public override void OnRespawn(Player player)
		{
			cycleTimer = 0;
		}

		public override void PostUpdate(Player player)
		{
			cycleTimer++;
			if (cycleTimer >= CycleTicks)
				cycleTimer = 0;
		}

		public override void PostUpdateMiscEffects(Player player)
		{
			// Only feed the conversion pool during the open window. A bare AddBuff would do nothing under
			// the elemental system — the damage comes from ElementalConversion, which ElementalDotApplier
			// reads on the next hit and also uses to place the debuff icons.
			if (cycleTimer >= WindowTicks)
				return;

			CombatEffectStats effects = player.GetModPlayer<CombatEffectStats>();

			for (int e = 0; e < DamageElementInfo.Count; e++)
				effects.ElementalConversion[e] += WindowConversionPerElement * 100f;
		}
	}

	/// <summary>
	/// Eat to live. Leech sustains you while attacking, and every kill detonates the corpse.
	/// </summary>
	public class DevourerTalent : TalentBehaviour
	{
		public override string Id => "devourer";
		public override string SlotKey => "evil";
		public override string DisplayName => "Devourer";

		public override string Description =>
			"Leech 6% of the damage you deal as life, up to 15% of your maximum life per hit. Killing an "
			+ "enemy detonates its corpse for 5% of its maximum life to everything within 5 tiles.";

		private const float LeechFraction = 0.06f;
		private const float KillExplosionFraction = 0.05f;
		private const float KillExplosionTiles = 5f;

		/// <summary>
		/// The on-hit leech is declarative so it pools with every other leech source (gear, Bloodthirst,
		/// Vengeance) into one capped payout in LifeLeechApplier — which already owns the ~0.3s cooldown
		/// (HealCooldownTicks) and the 15% per-hit cap. That shared limiter is the cooldown; a bespoke
		/// second on-hit heal would just add a second uncoordinated cap (CLAUDE.md §8).
		///
		/// Fraction, not whole percent — StatApplier's percent path multiplies by 100 on the way in.
		/// </summary>
		private static readonly Dictionary<string, float> percentBonuses = new Dictionary<string, float>
		{
			{ "LifeLeech", LeechFraction },
		};

		public override IReadOnlyDictionary<string, float> PercentBonuses => percentBonuses;

		public override void OnKillNPC(Player player, NPC target)
		{
			// Detonate the corpse instead of self-healing on kill. The old 8%-of-max-life kill burst
			// stacked with the leech and had no cap — it was most of why the talent over-healed. This
			// pays the kill out as damage to the pack instead.
			if (!LifeLeechApplier.IsLeechableTarget(target))
				return;

			int damage = (int)(target.lifeMax * KillExplosionFraction);
			if (damage < 1)
				return;

			float areaMul = 1f + player.GetModPlayer<CombatEffectStats>().AreaPercent / 100f;
			float radius = KillExplosionTiles * 16f * areaMul;
			Vector2 center = target.Center;

			bool hitAnything = false;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC other = Main.npc[i];
				if (!other.active || other.friendly || other.dontTakeDamage || other.immortal)
					continue;

				if (Vector2.Distance(other.Center, center) > radius)
					continue;

				int hitDirection = other.Center.X < center.X ? -1 : 1;
				// SimpleStrikeNPC bypasses ModPlayer.OnHitNPC (ElementalDotApplier.cs:22-27), so the blast
				// does not re-leech or re-convert — intended.
				other.SimpleStrikeNPC(damage, hitDirection, false, 0f, DamageClass.Generic);
				hitAnything = true;
			}

			if (hitAnything && Main.netMode != NetmodeID.Server)
			{
				SoundEngine.PlaySound(SoundID.Item14, center);
				for (int i = 0; i < 16; i++)
				{
					Dust dust = Dust.NewDustPerfect(center, DustID.Blood,
						Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(2f, 5f));
					dust.noGravity = true;
				}
			}
		}
	}

	/// <summary>
	/// Hurt me and the room bleeds with me — but only every four seconds.
	/// </summary>
	public class CorruptedBloodTalent : TalentBehaviour
	{
		public override string Id => "corrupted_blood";
		public override string SlotKey => "evil";
		public override string DisplayName => "Corrupted Blood";

		public override string Description =>
			"When you take damage, every enemy within 20 tiles takes that much damage. 4 second cooldown.";

		private const float RangeInTiles = 20f;
		private const int CooldownTicks = 240; // 4s — without it a per-tick DoT (Stagger) re-fires it every frame

		private int cooldown;

		public override void PostUpdate(Player player)
		{
			if (cooldown > 0)
				cooldown--;
		}

		public override void OnRespawn(Player player)
		{
			cooldown = 0;
		}

		public override void OnDeactivate(Player player)
		{
			cooldown = 0;
		}

		public override void PostHurt(Player player, Player.HurtInfo info)
		{
			if (info.Damage <= 0)
				return;

			if (cooldown > 0)
				return;

			float range = RangeInTiles * 16f;
			float areaMultiplier = 1f + player.GetModPlayer<CombatEffectStats>().AreaPercent / 100f;
			range *= areaMultiplier;

			bool hitAnything = false;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
					continue;

				if (player.Distance(npc.Center) > range)
					continue;

				int direction = npc.Center.X > player.Center.X ? 1 : -1;
				npc.SimpleStrikeNPC(info.Damage, direction, false, 0f, DamageClass.Generic);
				hitAnything = true;
			}

			// Start the cooldown whenever the effect triggers, so a barrage of DoT ticks can't re-fire it.
			cooldown = CooldownTicks;

			if (hitAnything && Main.netMode != NetmodeID.Server)
			{
				SoundEngine.PlaySound(SoundID.NPCHit1, player.Center);
				for (int i = 0; i < 16; i++)
					Dust.NewDust(player.position, player.width, player.height, DustID.Blood, 0f, 0f, 0, default, 1.4f);
			}
		}
	}
}
