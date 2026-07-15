using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// Pinnacle: Stagger. Damage you take is dealt over time instead of instantly.
	///
	/// This is the balance yardstick for every other talent — if a talent slot choice would not
	/// swing your survival or damage about as far as this does, it is not big enough to be one of
	/// only six picks. It is fixed at what used to be the tier-4 form; there are no ranks any more.
	///
	/// The stagger itself does not reduce damage — it buys you a window. Everything hits eventually,
	/// so it only pays off if you can out-heal or out-run the bleed, which is why Fleshless (no
	/// regeneration) is a trap next to it, and why Vengeance, which wants damage to land hard and
	/// fast, is its opposite number in the same slot. The defense and flat life are separate: they
	/// shrink the hit BEFORE it is split, so they make the window easier to answer rather than
	/// longer.
	/// </summary>
	public class StaggerTalent : TalentBehaviour
	{
		public override string Id => "stagger";
		public override string SlotKey => "pinnacle";
		public override string DisplayName => "Stagger";

		public override string Description =>
			"150% more defense, +150 maximum life and +8 life regeneration per second. 55% of the damage "
			+ "you take is dealt to you over 5.5 seconds instead of all at once. It is delayed, not "
			+ "prevented — you need to answer it. Your life regeneration is doubled once the bleed has "
			+ "run out and nothing has hit you for 5 seconds.";

		private const float DamagePercent = 0.55f;
		private const float Duration = 5.5f;
		private const float DefenseBonus = 1.50f;
		private const float FlatLife = 150f;

		/// <summary>
		/// In HP per second, which is NOT the unit Terraria stores: player.lifeRegen counts half-HP
		/// per second (120 lifeRegenCount = 1 HP, accumulated 60 times a second). The dictionary below
		/// doubles this on the way in, so the constant reads in the units the talent description uses.
		/// </summary>
		private const float FlatRegenPerSecond = 8f;

		/// <summary>Quiet time after the last real hit before regeneration doubles.</summary>
		private const int OutOfCombatTicks = 60 * 5;

		private const float RegenMultiplier = 2f;

		/// <summary>
		/// DefensePercent multiplies a Player.DefenseStat, which tracks adds and multiplies
		/// separately — so this is genuinely "more" defense and compounds with Bone Armor (x1.5) and
		/// Avatar of Flesh (x1.5) rather than summing with them. Worth watching in play: that stack
		/// reaches roughly x5.6 defense, and Bone Armor converts defense into flat damage, so this
		/// talent feeds that pick's offence as well as its own defence.
		/// </summary>
		private static readonly Dictionary<string, float> percentBonuses = new Dictionary<string, float>
		{
			{ "DefensePercent", DefenseBonus },
		};

		/// <summary>
		/// MaxHealth routes to StatModifier.Base in TalentPlayer.ModifyMaxStats — inside the
		/// multipliers, so % life bonuses scale it. Flat would sit outside them and silently exclude
		/// this 150 from every % life bonus in the game.
		///
		/// LifeRegen is doubled on the way in because Terraria counts lifeRegen in half-HP per second.
		/// It lands via StatApplier from PostUpdateMiscEffects, which has two consequences worth
		/// knowing: any vanilla DoT wipes it (UpdateLifeRegen zeroes a positive lifeRegen before
		/// applying Poison/On Fire/Venom degen, so this is off entirely while one ticks), and the
		/// out-of-combat doubling below catches it, taking it to 16 HP/s once the bleed has cleared.
		/// The stagger bleed itself does NOT wipe it — StaggerDebuff is display-only and never sets
		/// player.bleed — so this is the regeneration you answer your own bleed with.
		/// </summary>
		private static readonly Dictionary<string, float> flatBonuses = new Dictionary<string, float>
		{
			{ "MaxHealth", FlatLife },
			{ "LifeRegen", FlatRegenPerSecond * 2f },
		};

		public override IReadOnlyDictionary<string, float> PercentBonuses => percentBonuses;
		public override IReadOnlyDictionary<string, float> FlatBonuses => flatBonuses;

		/// <summary>Backstop on concurrent instances. See ModifyHurt for why the check lives there.</summary>
		private const int MaxInstances = 10;

		private readonly List<StaggerInstance> instances = new List<StaggerInstance>();

		/// <summary>Sub-1 damage carried between ticks so a slow bleed still resolves exactly.</summary>
		private float accumulatedDamage;

		private float pendingStaggerPercent;

		/// <summary>
		/// Set while we deal the bleed. Player.Hurt re-enters ModifyHurt/PostHurt, so without this
		/// the stagger damage would itself be staggered, renewing its own duration forever.
		/// </summary>
		private bool applyingStaggerDamage;

		/// <summary>Ticks since the last hit that was not our own bleed. Clamped, so it never wraps.</summary>
		private int ticksSinceHurt = OutOfCombatTicks;

		/// <summary>
		/// A live bleed counts as combat no matter how quiet it has gone: the damage is still landing,
		/// and letting regeneration double while it ticks would undo the "you need to answer it" half
		/// of the talent. The timer is the backstop for the one case that makes no instance — a hit
		/// taken at MaxInstances.
		/// </summary>
		private bool IsOutOfCombat => instances.Count == 0 && ticksSinceHurt >= OutOfCombatTicks;

		public override void OnDeactivate(Player player)
		{
			ClearStagger(player);
		}

		public override void OnRespawn(Player player)
		{
			// Never carry a bleed across a life.
			ClearStagger(player);
		}

		private void ClearStagger(Player player)
		{
			instances.Clear();
			accumulatedDamage = 0f;
			ticksSinceHurt = OutOfCombatTicks;

			int buffType = ModContent.BuffType<StaggerDebuff>();
			if (player.HasBuff(buffType))
				player.ClearBuff(buffType);
		}

		/// <summary>
		/// Gear- and buff-sourced regeneration, doubled from inside UpdateLifeRegen. The guard is real
		/// here: every DoT has already driven lifeRegen negative by the time this fires, and doubling
		/// a negative would make Poison and On Fire twice as lethal in exactly the quiet moment this
		/// talent is meant to reward.
		/// </summary>
		public override void UpdateLifeRegen(Player player)
		{
			if (!IsOutOfCombat || player.lifeRegen <= 0)
				return;

			player.lifeRegen = (int)(player.lifeRegen * RegenMultiplier);
		}

		/// <summary>
		/// The natural ramp, and the half that actually matters. Out of combat almost all of a player's
		/// regeneration is this, and it is added to lifeRegen after every other hook in the tick has
		/// run — so PostUpdateMiscEffects (where the rest of this mod scales regen) cannot reach it.
		/// </summary>
		public override void NaturalLifeRegen(Player player, ref float regen)
		{
			if (!IsOutOfCombat)
				return;

			regen *= RegenMultiplier;
		}

		public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
		{
			if (applyingStaggerDamage)
			{
				pendingStaggerPercent = 0f;
				return;
			}

			// Decide the instance cap HERE, before mitigating. PostHurt is what carries the staggered
			// portion into a bleed, so if it won't be able to create an instance then the hit has to
			// land in full — otherwise we shave the damage here and the shaved portion is never dealt
			// at all, silently turning Stagger into flat damage reduction exactly when the player is
			// under the most pressure.
			if (instances.Count >= MaxInstances)
			{
				pendingStaggerPercent = 0f;
				return;
			}

			pendingStaggerPercent = DamagePercent;
			modifiers.FinalDamage *= 1f - pendingStaggerPercent;
		}

		public override void PostHurt(Player player, Player.HurtInfo info)
		{
			// Our own bleed re-enters Hurt every tick it lands. It is not a fresh hit, and a live
			// bleed already holds IsOutOfCombat false on its own.
			if (applyingStaggerDamage)
				return;

			// Reset ABOVE the pendingStaggerPercent early-out. A hit taken at MaxInstances is not
			// staggered but is still the player being attacked, and returning first would let them
			// count as out of combat while under fire — precisely when the cap is being hit.
			ticksSinceHurt = 0;

			if (pendingStaggerPercent <= 0f)
				return;

			// info.Damage is post-mitigation, so this inverts the ModifyHurt multiply to recover the
			// portion that was removed.
			float staggeredDamage = info.Damage * (pendingStaggerPercent / (1f - pendingStaggerPercent));
			pendingStaggerPercent = 0f;

			if (staggeredDamage <= 0)
				return;

			// Deliberately unclamped. Anything refused here is damage already removed from the hit,
			// so it would simply vanish. A huge bleed from a freak hit is correct — it is damage the
			// player would have eaten instantly anyway, and the window to answer it is the point.
			instances.Add(new StaggerInstance
			{
				RemainingDamage = staggeredDamage,
				TimeRemaining = Duration,
				DamagePerSecond = staggeredDamage / Duration,
			});

			player.AddBuff(ModContent.BuffType<StaggerDebuff>(), 60 * (int)Duration + 60);

			if (Main.netMode != Terraria.ID.NetmodeID.Server)
				CombatText.NewText(player.getRect(), Color.Orange, $"{(int)staggeredDamage} Staggered", false, false);
		}

		public override void PostUpdateMiscEffects(Player player)
		{
			// Counted here rather than in PostUpdate because Player.Update runs PostUpdateMiscEffects
			// immediately before UpdateLifeRegen, and the regen hooks below read the result. Clamped
			// rather than left to climb, so it cannot overflow across a long session.
			if (ticksSinceHurt < OutOfCombatTicks)
				ticksSinceHurt++;

			if (instances.Count == 0)
			{
				accumulatedDamage = 0f;
				return;
			}

			const float deltaTime = 1f / 60f;
			float damageThisTick = 0f;

			for (int i = instances.Count - 1; i >= 0; i--)
			{
				StaggerInstance instance = instances[i];
				instance.TimeRemaining -= deltaTime;

				if (instance.TimeRemaining <= 0f)
				{
					// Expired — dump whatever is left rather than letting it evaporate.
					damageThisTick += instance.RemainingDamage;
					instances.RemoveAt(i);
					continue;
				}

				float tick = instance.DamagePerSecond * deltaTime;
				if (tick > instance.RemainingDamage)
					tick = instance.RemainingDamage;

				instance.RemainingDamage -= tick;
				damageThisTick += tick;
			}

			accumulatedDamage += damageThisTick;

			if (accumulatedDamage >= 1f)
			{
				int toApply = (int)accumulatedDamage;
				accumulatedDamage -= toApply;

				applyingStaggerDamage = true;
				try
				{
					player.Hurt(
						Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
							NetworkText.FromLiteral(player.name + " was staggered to death.")),
						toApply, 0, false, false, -1, false, 0);
				}
				finally
				{
					applyingStaggerDamage = false;
				}
			}

			if (instances.Count == 0)
			{
				accumulatedDamage = 0f;
				return;
			}

			// Keep the buff alive for as long as the longest bleed has left.
			float longest = 0f;
			foreach (StaggerInstance instance in instances)
			{
				if (instance.TimeRemaining > longest)
					longest = instance.TimeRemaining;
			}

			int buffIndex = player.FindBuffIndex(ModContent.BuffType<StaggerDebuff>());
			if (buffIndex >= 0)
				player.buffTime[buffIndex] = (int)(longest * 60f) + 10;
		}

		public float GetTotalStaggeredDamage()
		{
			float total = 0f;
			foreach (StaggerInstance instance in instances)
				total += instance.RemainingDamage;
			return total;
		}

		public float GetLongestStaggerDuration()
		{
			float longest = 0f;
			foreach (StaggerInstance instance in instances)
			{
				if (instance.TimeRemaining > longest)
					longest = instance.TimeRemaining;
			}
			return longest;
		}

		private class StaggerInstance
		{
			public float RemainingDamage { get; set; }
			public float TimeRemaining { get; set; }
			public float DamagePerSecond { get; set; }
		}
	}

	/// <summary>Display-only buff. All the logic lives in StaggerTalent.</summary>
	public class StaggerDebuff : ModBuff
	{
		public override string Texture => "Terraria/Images/Buff_156"; // Bleeding icon

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = false;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override bool RightClick(int buffIndex)
		{
			return false;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			buffName = "Staggered";
			rare = 1;

			// Read the live numbers off whichever StaggerTalent instance this player actually has.
			foreach (TalentBehaviour behaviour in TalentPlayer.Get(Main.LocalPlayer).Active)
			{
				if (behaviour is StaggerTalent stagger)
				{
					tip = $"Taking {(int)stagger.GetTotalStaggeredDamage()} damage over {stagger.GetLongestStaggerDuration():F1} seconds";
					return;
				}
			}

			tip = "Taking damage over time";
		}
	}
}
