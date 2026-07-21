using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerSystems;

namespace ProgressionExpanded.Src.NPCs
{
	/// <summary>
	/// Per-enemy Dread state for the Dread Gaze talent (EyeTalents.cs). Each enemy accumulates stacks
	/// from the player's hits; at <see cref="MaxStacks"/> it erupts for a share of the player's maximum
	/// life, and the eruption spreads Dread to nearby enemies — which can cascade.
	///
	/// <b>Why a GlobalNPC and not talent-side state.</b> A stack count belongs to the enemy, and the old
	/// Dread Gaze could only track ONE target on the behaviour instance. Storing per-enemy means the
	/// player can build Dread across a whole pack, which is the point of the rework.
	///
	/// <b>Int fields, so no custom Clone is needed.</b> Both fields are value types, so the default
	/// GlobalNPC.Clone (MemberwiseClone) copies them correctly — unlike a reference-type field,
	/// which needs a deep copy. A split worm segment simply inherits its parent's stacks, which is fine.
	///
	/// Single-player focused: the eruption applies damage directly via SimpleStrikeNPC and rolls no
	/// packets. Consistent with the mod's SP-first design (CLAUDE.md §7).
	/// </summary>
	public class DreadNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		/// <summary>Stacks needed to erupt.</summary>
		public const int MaxStacks = 10;

		/// <summary>Stacks each of the player's hits adds.</summary>
		public const int PlayerDreadPerHit = 1;

		/// <summary>~4s during which a freshly-erupted enemy cannot gain Dread — bounds the cascade.</summary>
		private const int ImmunityTicks = 240;

		/// <summary>Dread the eruption spreads to each nearby enemy.</summary>
		private const int ChainDreadAmount = 3;

		/// <summary>Recursion backstop: how many eruptions a single hit may chain through. The 4s
		/// immunity already stops a mob re-erupting, but a dense field could otherwise chain far in one
		/// frame — this caps the depth-first spread.</summary>
		private const int MaxChainDepth = 4;

		/// <summary>Eruption damage as a fraction of the player's MAX life (statLifeMax2).</summary>
		private const float ExplosionLifeFraction = 0.10f;

		private const float ExplosionTiles = 5f;

		private int stacks;

		/// <summary>Counts down while > 0; Dread cannot be gained during this window.</summary>
		private int immunityTicks;

		public override void PostAI(NPC npc)
		{
			if (immunityTicks > 0)
				immunityTicks--;
		}

		/// <summary>
		/// Add Dread to an enemy, erupting it if it reaches the cap. Called from the talent's OnHitNPC
		/// (chainDepth 0) and recursively from an eruption's spread.
		/// </summary>
		public static void Apply(Player player, NPC target, int amount, int chainDepth)
		{
			if (target == null || !target.active || target.friendly || target.dontTakeDamage || target.immortal)
				return;

			DreadNPC dread = target.GetGlobalNPC<DreadNPC>();
			if (dread.immunityTicks > 0)
				return;

			dread.stacks += amount;

			// A little feedback so a building enemy is legible without a full debuff icon.
			if (Main.netMode != NetmodeID.Server)
			{
				Dust dust = Dust.NewDustPerfect(target.Top, DustID.Shadowflame,
					new Vector2(0f, -1.5f), 0, default, 1.1f);
				dust.noGravity = true;
			}

			if (dread.stacks >= MaxStacks)
				Erupt(player, target, chainDepth);
		}

		/// <summary>
		/// Detonate the enemy: reset it, make it briefly Dread-immune, damage every hostile in radius
		/// (including itself), and spread Dread outward — which may chain further eruptions.
		/// </summary>
		private static void Erupt(Player player, NPC source, int chainDepth)
		{
			DreadNPC dread = source.GetGlobalNPC<DreadNPC>();
			dread.stacks = 0;
			dread.immunityTicks = ImmunityTicks;

			int damage = (int)(player.statLifeMax2 * ExplosionLifeFraction);
			if (damage < 1)
				damage = 1;

			float areaMul = 1f + CombatEffectStats.Get(player).AreaPercent / 100f;
			float radius = ExplosionTiles * 16f * areaMul;
			Vector2 center = source.Center;

			if (Main.netMode != NetmodeID.Server)
			{
				SoundEngine.PlaySound(SoundID.Item14, center);
				for (int i = 0; i < 24; i++)
				{
					Dust dust = Dust.NewDustPerfect(center, DustID.Shadowflame,
						Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(2f, 6f));
					dust.noGravity = true;
				}
			}

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC other = Main.npc[i];
				if (!other.active || other.friendly || other.dontTakeDamage || other.immortal)
					continue;

				if (Vector2.Distance(other.Center, center) > radius)
					continue;

				int hitDirection = other.Center.X < center.X ? -1 : 1;
				other.SimpleStrikeNPC(damage, hitDirection, false, 0f, DamageClass.Generic);

				// Spread Dread to everyone caught in the blast (never the source — it is now immune, so
				// this is a no-op on it, but skipping it also avoids re-processing the epicentre).
				if (chainDepth < MaxChainDepth && other.whoAmI != source.whoAmI)
					Apply(player, other, ChainDreadAmount, chainDepth + 1);
			}
		}
	}
}
