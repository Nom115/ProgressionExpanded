using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// World-evil row: rot, consumption, and blood that fights back.
	/// </summary>
	public class PlaguebearerTalent : TalentBehaviour
	{
		public override string Id => "plaguebearer";
		public override string SlotKey => "evil";
		public override string DisplayName => "Plaguebearer";

		public override string Description =>
			"Your hits inflict Bleeding, On Fire, Venom, Frostburn and Electrified at once. "
			+ "Enemies suffering 3 or more debuffs take 40% more damage from you.";

		private const int DebuffDurationTicks = 60 * 5;
		private const int DebuffsForBonus = 3;
		private const float DamageBonus = 0.40f;

		private static readonly int[] Plagues =
		{
			BuffID.Bleeding,
			BuffID.OnFire,
			BuffID.Venom,
			BuffID.Frostburn,
			BuffID.Electrified,
		};

		public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (target.friendly)
				return;

			float ailmentMultiplier = 1f + player.GetModPlayer<CombatEffectStats>().AilmentPercent / 100f;
			int duration = (int)(DebuffDurationTicks * ailmentMultiplier);

			foreach (int buff in Plagues)
				target.AddBuff(buff, duration);
		}

		public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (CountDebuffs(target) >= DebuffsForBonus)
				modifiers.FinalDamage *= 1f + DamageBonus;
		}

		/// <summary>
		/// Counts real debuffs, not every buff. An NPC's buff array holds both, and Main.debuff is
		/// the only thing that distinguishes them — so a friendly buff would otherwise count toward
		/// the bonus.
		/// </summary>
		private static int CountDebuffs(NPC target)
		{
			int count = 0;
			for (int i = 0; i < NPC.maxBuffs; i++)
			{
				int type = target.buffType[i];
				if (type > 0 && target.buffTime[i] > 0 && Main.debuff[type])
					count++;
			}
			return count;
		}
	}

	/// <summary>
	/// Eat to live. The intended partner for Fleshless — leech replaces the regeneration it takes.
	/// </summary>
	public class DevourerTalent : TalentBehaviour
	{
		public override string Id => "devourer";
		public override string SlotKey => "evil";
		public override string DisplayName => "Devourer";

		public override string Description =>
			"Leech 12% of the damage you deal as life, up to 15% of your maximum life per hit. "
			+ "Killing an enemy restores 8% of your maximum life.";

		private const float LeechFraction = 0.12f;
		private const float KillHealFraction = 0.08f;

		/// <summary>
		/// The on-hit leech is declarative so it pools with every other leech source (gear,
		/// Bloodthirst, Vengeance) into one capped payout in LifeLeechApplier. This used to be a
		/// bespoke OnHitNPC heal with its own cooldown, which meant a Devourer + Vengeance player
		/// would proc TWO independent leech heals per hit and neither cap would see the other.
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
			// The kill burst stays bespoke: it is a fraction of MAX LIFE, not of damage dealt, so it
			// is not a leech and has no business in the leech pool. Deliberately not cooldowned —
			// kills are their own rate limit, and the burst is what makes this feel like devouring
			// rather than sipping.
			if (!LifeLeechApplier.IsLeechableTarget(target))
				return;

			LifeLeechApplier.Heal(player, (int)(player.statLifeMax2 * KillHealFraction));
		}
	}

	/// <summary>
	/// Hurt me and the room bleeds with me.
	/// </summary>
	public class CorruptedBloodTalent : TalentBehaviour
	{
		public override string Id => "corrupted_blood";
		public override string SlotKey => "evil";
		public override string DisplayName => "Corrupted Blood";

		public override string Description =>
			"When you take damage, every enemy within 20 tiles takes that much damage. "
			+ "Pairs viciously with Stagger — the bleed keeps splashing.";

		private const float RangeInTiles = 20f;

		public override void PostHurt(Player player, Player.HurtInfo info)
		{
			if (info.Damage <= 0)
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

			if (hitAnything && Main.netMode != NetmodeID.Server)
			{
				SoundEngine.PlaySound(SoundID.NPCHit1, player.Center);
				for (int i = 0; i < 16; i++)
					Dust.NewDust(player.position, player.width, player.height, DustID.Blood, 0f, 0f, 0, default, 1.4f);
			}
		}
	}
}
