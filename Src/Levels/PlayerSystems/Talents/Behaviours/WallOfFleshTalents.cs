using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// Wall of Flesh capstones. These are the last slot and should read as the biggest single thing
	/// on the character sheet.
	/// </summary>
	public class AvatarOfFleshTalent : TalentBehaviour
	{
		public override string Id => "avatar_of_flesh";
		public override string SlotKey => "wof";
		public override string DisplayName => "Avatar of Flesh";

		public override string Description =>
			"+75% maximum life, +50% defense, and you cannot be knocked back. Become the wall.";

		private static readonly Dictionary<string, float> percentBonuses = new Dictionary<string, float>
		{
			{ "MaxLifePercent", 0.75f },
			{ "DefensePercent", 0.50f },
		};

		public override IReadOnlyDictionary<string, float> PercentBonuses => percentBonuses;

		public override void ResetEffects(Player player)
		{
			player.noKnockback = true;
		}
	}

	/// <summary>
	/// Everything you touch burns, and armour stops mattering.
	/// </summary>
	public class HellfireTalent : TalentBehaviour
	{
		public override string Id => "hellfire";
		public override string SlotKey => "wof";
		public override string DisplayName => "Hellfire";

		public override string Description =>
			"Your hits explode for 60% of the damage dealt in a wide area, and your attacks ignore half of enemy defense.";

		private const float ExplosionFraction = 0.60f;
		private const float ArmorIgnored = 0.50f;
		private const float RadiusInTiles = 9f;

		/// <summary>Guards the explosion against re-entering itself through OnHitNPC.</summary>
		private bool exploding;

		public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
		{
			// At 1f this would ignore defense entirely; 0.5f halves it. Additive only, per the API's
			// own guidance — multiplying this compounds badly with other penetration sources.
			modifiers.ScalingArmorPenetration += ArmorIgnored;
		}

		public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
		{
			// SimpleStrikeNPC below runs the hit pipeline again, which comes back through here. Without
			// this guard each explosion would set off every enemy it hits, chaining across the screen
			// until the stack gives out.
			if (exploding || target.friendly)
				return;

			int damage = (int)(damageDone * ExplosionFraction);
			if (damage <= 0)
				return;

			float areaMultiplier = 1f + player.GetModPlayer<CombatEffectStats>().AreaPercent / 100f;
			float radius = RadiusInTiles * 16f * areaMultiplier;

			exploding = true;
			try
			{
				Explode(player, target, target.Center, radius, damage);
			}
			finally
			{
				exploding = false;
			}
		}

		private static void Explode(Player player, NPC source, Vector2 center, float radius, int damage)
		{
			bool hitAnything = false;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
					continue;

				// Don't re-hit the thing that triggered it — it already took the real hit.
				if (npc.whoAmI == source.whoAmI)
					continue;

				if (Vector2.Distance(center, npc.Center) > radius)
					continue;

				int direction = npc.Center.X > center.X ? 1 : -1;
				npc.SimpleStrikeNPC(damage, direction, false, 2f, DamageClass.Generic);
				hitAnything = true;
			}

			if (Main.netMode == NetmodeID.Server)
				return;

			SoundEngine.PlaySound(SoundID.Item14, center);
			for (int i = 0; i < 24; i++)
			{
				Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
				Dust.NewDustPerfect(center, DustID.Torch, velocity, 0, default, 1.6f);
			}

			_ = hitAnything;
		}
	}

	/// <summary>
	/// A kill streak you have to protect. Rewards clearing rooms and punishes standing still.
	/// </summary>
	public class SoulHarvestTalent : TalentBehaviour
	{
		public override string Id => "soul_harvest";
		public override string SlotKey => "wof";
		public override string DisplayName => "Soul Harvest";

		public override string Description =>
			"Each kill grants a stack: +2% damage and +1% movement speed, up to 25 stacks (+50% and +25%). "
			+ "You lose a stack every second you go without a kill.";

		private const int MaxStacks = 25;
		private const float DamagePerStack = 0.02f;
		private const float MoveSpeedPerStack = 0.01f;
		private const int DecayIntervalTicks = 60;

		private int stacks;
		private int decayTimer;

		public override void OnDeactivate(Player player)
		{
			stacks = 0;
			decayTimer = 0;
		}

		public override void OnRespawn(Player player)
		{
			stacks = 0;
			decayTimer = 0;
		}

		public override void OnKillNPC(Player player, NPC target)
		{
			if (target.friendly || target.lifeMax <= 5)
				return;

			if (stacks < MaxStacks)
				stacks++;

			decayTimer = DecayIntervalTicks;
		}

		public override void PostUpdate(Player player)
		{
			if (stacks <= 0)
				return;

			// Decay one stack at a time rather than dropping the streak on a timer. Losing everything
			// at once would make the talent feel like it was taken away from you; bleeding a stack a
			// second gives you a chance to notice and go find something to kill.
			if (--decayTimer <= 0)
			{
				stacks--;
				decayTimer = DecayIntervalTicks;
			}
		}

		public override void ResetEffects(Player player)
		{
			if (stacks <= 0)
				return;

			player.GetDamage(DamageClass.Generic) *= 1f + DamagePerStack * stacks;
			player.moveSpeed *= 1f + MoveSpeedPerStack * stacks;
		}

		public int GetStacks() => stacks;
	}
}
