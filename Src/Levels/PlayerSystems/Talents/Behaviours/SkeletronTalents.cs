using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// Skeletron row: armour turned into a weapon, a curse that spreads off your kills, and giving
	/// up healing entirely.
	/// </summary>
	public class BoneArmorTalent : TalentBehaviour
	{
		public override string Id => "bone_armor";
		public override string SlotKey => "skeletron";
		public override string DisplayName => "Bone Armor";

		public override string Description =>
			"+50% defense, and 20% of your defense is added to your damage. Every armour upgrade hits twice.";

		private const float DefenseToDamage = 0.20f;

		private static readonly Dictionary<string, float> percentBonuses = new Dictionary<string, float>
		{
			{ "DefensePercent", 0.50f },
		};

		public override IReadOnlyDictionary<string, float> PercentBonuses => percentBonuses;

		public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
		{
			// Read defense at hit time, not in an update hook. statDefense is assembled across the
			// whole update — armour, buffs, our own DefensePercent — and is only final by the time a
			// hit actually resolves. Reading it earlier would silently use a partial number.
			modifiers.FlatBonusDamage += (int)player.statDefense * DefenseToDamage;
		}
	}

	/// <summary>
	/// Your kills mark the next victim.
	/// </summary>
	public class CursedSkullTalent : TalentBehaviour
	{
		public override string Id => "cursed_skull";
		public override string SlotKey => "skeletron";
		public override string DisplayName => "Cursed Skull";

		public override string Description =>
			"Killing an enemy curses the nearest one for 8 seconds. Cursed enemies take 50% more damage "
			+ "from you and deal 30% less damage.";

		public const float DamageTakenBonus = 0.50f;
		public const float DamageDealtReduction = 0.30f;

		private const int CurseDurationTicks = 60 * 8;
		private const float SearchRangeInTiles = 40f;

		public override void OnKillNPC(Player player, NPC target)
		{
			if (target.friendly || target.lifeMax <= 5)
				return;

			NPC nearest = FindNearest(player, target, SearchRangeInTiles * 16f);
			if (nearest == null)
				return;

			nearest.AddBuff(ModContent.BuffType<CursedSkullDebuff>(), CurseDurationTicks);

			if (Main.netMode != NetmodeID.Server)
			{
				for (int i = 0; i < 12; i++)
					Dust.NewDust(nearest.position, nearest.width, nearest.height, DustID.Bone, 0f, -2f, 0, default, 1.2f);
			}
		}

		private static NPC FindNearest(Player player, NPC exclude, float range)
		{
			NPC best = null;
			float bestDistance = range;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
					continue;

				// The corpse is still "active" inside OnHitNPC, so skip it explicitly or the curse
				// lands on the thing we just killed and does nothing.
				if (npc.whoAmI == exclude.whoAmI || npc.life <= 0)
					continue;

				float distance = player.Distance(npc.Center);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = npc;
				}
			}

			return best;
		}

		public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (target.HasBuff(ModContent.BuffType<CursedSkullDebuff>()))
				modifiers.FinalDamage *= 1f + DamageTakenBonus;
		}
	}

	/// <summary>The curse marker. Its effects live in CursedSkullTalent and CursedSkullEnemy.</summary>
	public class CursedSkullDebuff : ModBuff
	{
		public override string Texture => "Terraria/Images/Buff_36"; // Cursed Inferno icon

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = true;
			Main.pvpBuff[Type] = false;
		}
	}

	/// <summary>
	/// The half of Cursed Skull that has to live on the enemy: a cursed enemy hits softer. This can't
	/// go in the talent itself because it fires from the NPC's side of the exchange.
	/// </summary>
	public class CursedSkullEnemy : GlobalNPC
	{
		public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
		{
			if (npc.HasBuff(ModContent.BuffType<CursedSkullDebuff>()))
				modifiers.FinalDamage *= 1f - CursedSkullTalent.DamageDealtReduction;
		}
	}

	/// <summary>
	/// Give up healing entirely and become very hard to kill.
	///
	/// A trap next to Stagger, on purpose: Stagger converts burst into a bleed you are expected to
	/// out-heal, and this removes healing. It contradicts Royal Jelly outright. Devourer is the
	/// intended partner — leech is not regeneration, so it still works.
	/// </summary>
	public class FleshlessTalent : TalentBehaviour
	{
		public override string Id => "fleshless";
		public override string SlotKey => "skeletron";
		public override string DisplayName => "Fleshless";

		public override string Description =>
			"You no longer regenerate life, but you take 40% less damage. Leech and potions still work.";

		private const float DamageReduction = 0.40f;

		public override void PostUpdateMiscEffects(Player player)
		{
			// Only cancel regeneration, never degeneration — zeroing a negative lifeRegen would make
			// this immunity to every damage-over-time effect in the game, which it very much is not.
			if (player.lifeRegen > 0)
				player.lifeRegen = 0;
		}

		public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
		{
			modifiers.FinalDamage *= 1f - DamageReduction;
		}
	}
}
