using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// Queen Bee row: the swarm, the frenzy, and the hive that keeps you alive.
	/// </summary>
	public class HiveMindTalent : TalentBehaviour
	{
		public override string Id => "hive_mind";
		public override string SlotKey => "bee";
		public override string DisplayName => "Hive Mind";

		public override string Description => "+4 minion slots and +60% summon damage.";

		private static readonly Dictionary<string, float> flatBonuses = new Dictionary<string, float>
		{
			{ "MinionSlots", 4f },
		};

		private static readonly Dictionary<string, float> percentBonuses = new Dictionary<string, float>
		{
			{ "SummonDamage", 0.60f },
		};

		public override IReadOnlyDictionary<string, float> FlatBonuses => flatBonuses;
		public override IReadOnlyDictionary<string, float> PercentBonuses => percentBonuses;
	}

	/// <summary>
	/// Build speed by never stopping. Worthless under Juggernaut, which is the point — the pinnacle
	/// choice is supposed to close doors further down.
	/// </summary>
	public class ApiaryFrenzyTalent : TalentBehaviour
	{
		public override string Id => "apiary_frenzy";
		public override string SlotKey => "bee";
		public override string DisplayName => "Apiary Frenzy";

		public override string Description =>
			"Each hit grants a stack: +3% attack speed and +2% movement speed, up to 10 stacks. "
			+ "Stacks fall off 4 seconds after your last hit.";

		private const int MaxStacks = 10;
		private const float AttackSpeedPerStack = 0.03f;
		private const float MoveSpeedPerStack = 0.02f;
		private const int StackDurationTicks = 60 * 4;

		private int stacks;
		private int timer;

		public override void OnDeactivate(Player player)
		{
			stacks = 0;
			timer = 0;
		}

		public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (target.friendly)
				return;

			if (stacks < MaxStacks)
				stacks++;

			timer = StackDurationTicks;
		}

		public override void PostUpdate(Player player)
		{
			if (stacks <= 0)
				return;

			if (--timer <= 0)
			{
				// All stacks drop together rather than one at a time — a hard cliff makes the "keep
				// swinging" pressure legible, where a slow decay just feels like noise.
				stacks = 0;
				timer = 0;
			}
		}

		public override void ResetEffects(Player player)
		{
			if (stacks <= 0)
				return;

			player.moveSpeed *= 1f + MoveSpeedPerStack * stacks;

			// Juggernaut says attack speed cannot be increased, and means it. Movement still applies.
			if (TalentPlayer.Get(player).Suppresses(TalentSuppression.AttackSpeedIncreases))
				return;

			player.GetAttackSpeed(DamageClass.Generic) += AttackSpeedPerStack * stacks;
		}

		public int GetStacks() => stacks;
	}

	/// <summary>
	/// The hive heals its own, even mid-fight.
	/// </summary>
	public class RoyalJellyTalent : TalentBehaviour
	{
		public override string Id => "royal_jelly";
		public override string SlotKey => "bee";
		public override string DisplayName => "Royal Jelly";

		public override string Description =>
			"+30% maximum life. Your life regeneration is five times faster and no longer stops when you take damage.";

		private const float RegenMultiplier = 5f;

		private static readonly Dictionary<string, float> percentBonuses = new Dictionary<string, float>
		{
			{ "MaxLifePercent", 0.30f },
		};

		public override IReadOnlyDictionary<string, float> PercentBonuses => percentBonuses;

		public override void PostUpdateMiscEffects(Player player)
		{
			// Vanilla zeroes lifeRegenTime when you get hit, which is what makes natural regen useless
			// in a fight. Holding it up keeps the ramp maxed so regen actually applies under fire.
			if (player.lifeRegenTime < 600)
				player.lifeRegenTime = 600;

			// Only multiply regeneration, never degeneration. lifeRegen goes negative under a DoT, so
			// an unguarded multiply would make every Bleeding/Poison tick five times deadlier — the
			// exact opposite of what this talent says it does.
			if (player.lifeRegen > 0)
				player.lifeRegen = (int)(player.lifeRegen * RegenMultiplier);
		}
	}
}
