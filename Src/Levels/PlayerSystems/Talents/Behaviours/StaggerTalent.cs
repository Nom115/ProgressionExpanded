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
	/// The stagger itself does not reduce damage — it buys you a window, and how you spend that window
	/// IS the talent. Everything hits eventually, so it only pays off if you answer the bleed, which is
	/// why Fleshless (no regeneration) is a trap next to it, and why Vengeance, which wants damage to
	/// land hard and fast, is its opposite number in the same slot. The defense and flat life are
	/// separate: they shrink the hit BEFORE it is split, so they make the window easier to answer
	/// rather than longer.
	///
	/// <b>The move/plant trade is the tension, and it is armed only while a bleed is live.</b> Take a
	/// hit and you have 5.5 seconds to choose: run, and your regeneration doubles while your damage is
	/// halved — you out-run the bleed; or plant your feet, and you hit 50% harder while the bleed eats
	/// you. Once the bleed clears there is no bonus and no penalty, so this is a recurring, bounded
	/// decision rather than a permanent tax.
	///
	/// <b>Why bounded matters.</b> An always-on "half damage while moving" would not be a choice at
	/// all: Terraria bosses force movement, so Stagger would simply deal half damage for whole fights
	/// against Juggernaut's flat x1.65. Tying the mode to the bleed means the cost only exists in the
	/// window where the choice is real, and the window only opens because you were hit.
	///
	/// This is deliberately the opposite pole from Juggernaut, which is the wall that cannot move.
	/// Stagger is the one that has to.
	/// </summary>
	public class StaggerTalent : TalentBehaviour
	{
		public override string Id => "stagger";
		public override string SlotKey => "pinnacle";
		public override string DisplayName => "Stagger";

		public override string Description =>
			"150% more defense, +150 maximum life and +8 life regeneration per second. 55% of the damage "
			+ "you take is dealt to you over 5.5 seconds instead of all at once. It is delayed, not "
			+ "prevented — you need to answer it. While it is running, keep moving and your life "
			+ "regeneration doubles but you deal half damage; hold your ground for a second and you "
			+ "deal 50% more instead. Run it off, or stand and trade.";

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

		/// <summary>
		/// Time spent stationary before you count as planted.
		///
		/// One threshold, two states, no neutral middle: you are "moving" from the instant you move
		/// until you have held still this long. So a momentary pause does not cost you the heal, and
		/// the damage stance has to be committed to. Erring toward the heal is deliberate — it is the
		/// half you take the talent for.
		///
		/// It also kills the frame-perfect trick the alternative invites. Damage is judged at hit time
		/// (see ModifyHitNPC), so with an instant threshold you could run for the healing and stop for
		/// the single frame your hit lands to bank the bonus as well. A second of commitment makes that
		/// cost a second of not healing, which is the trade working as intended.
		/// </summary>
		private const int PlantTicks = 60;

		/// <summary>Untested first guesses, all three. These are the dials. See the class docs.</summary>
		private const float MovingRegenMultiplier = 2f;
		private const float MovingDamageMultiplier = 0.5f;
		private const float PlantedDamageMultiplier = 1.5f;

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
		/// moving doubling below catches it, taking it to 16 HP/s.
		///
		/// The stagger bleed itself does NOT wipe it — StaggerDebuff is display-only and never sets
		/// player.bleed, which vanilla would otherwise treat as a regen stopper. So this is the
		/// regeneration you answer your own bleed with, and running is what doubles it.
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

		/// <summary>Consecutive ticks spent stationary. Clamped, so it never wraps.</summary>
		private int ticksStandingStill;

		/// <summary>
		/// Whether the trade is armed. Nothing below does anything without a live bleed — no bonus, no
		/// penalty, which is what keeps this from being a permanent damage tax in fights that force you
		/// to move.
		/// </summary>
		private bool IsBleeding => instances.Count > 0;

		/// <summary>
		/// Planted, i.e. holding ground rather than running. See PlantTicks for why the threshold is
		/// one-sided.
		///
		/// A real hit knocks you loose from this on its own and no combat check is needed to make it
		/// so: Player.Hurt writes velocity whenever hitDirection != 0 (Player.cs:37929). Our own bleed
		/// passes hitDirection 0 and so deliberately does not — otherwise the bleed would break your
		/// stance every tick and the planted half of the talent could never be held at all.
		/// </summary>
		private bool IsPlanted => ticksStandingStill >= PlantTicks;

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

			// Zero rather than full: the doubling must be re-earned by actually standing still. Handing
			// it out at respawn would have it live the instant you drop in.
			ticksStandingStill = 0;

			int buffType = ModContent.BuffType<StaggerDebuff>();
			if (player.HasBuff(buffType))
				player.ClearBuff(buffType);
		}

		/// <summary>
		/// The healing half: run and your regeneration doubles. Applies to the flat component only —
		/// gear, buffs, and this talent's own +8/s, taking it to 16.
		///
		/// <b>There is deliberately no NaturalLifeRegen counterpart, and it would be dead code if there
		/// were.</b> Vanilla's natural ramp is driven by player.lifeRegenTime, and Player.Hurt zeroes
		/// that on every call (Player.cs:37838) — including our own bleed's, which fires every few
		/// ticks. The ramp needs 300 unbroken ticks to reach even its first step, so it sits pinned at
		/// zero for the entire bleed. Since this trade only ever arms while a bleed is live, doubling
		/// the ramp would be doubling zero, every time, without exception.
		///
		/// That is also why doubling the flat component is the right lever rather than a compromise:
		/// vanilla penalises the natural ramp for moving (x0.5 against x1.25 stationary,
		/// Player.cs:18003) but does nothing of the sort to flat regen. A x2 on the ramp would not even
		/// reach parity with standing still; a x2 on flat regen means exactly what it says.
		///
		/// The lifeRegen > 0 guard is real: a vanilla DoT has already driven it negative by the time we
		/// run, and doubling a negative would make Poison and On Fire twice as lethal precisely when
		/// you are trying to escape.
		/// </summary>
		public override void UpdateLifeRegen(Player player)
		{
			if (!IsBleeding || IsPlanted || player.lifeRegen <= 0)
				return;

			player.lifeRegen = (int)(player.lifeRegen * MovingRegenMultiplier);
		}

		/// <summary>
		/// The damage half of the trade, judged at HIT time rather than fire time. That is what makes
		/// this fair across classes, and it is the whole reason the multiplier is not on GetDamage.
		///
		/// <b>The trap this avoids.</b> Terraria snapshots a projectile's damage when it spawns
		/// (Projectile.cs:93467), so a GetDamage multiplier would let a ranged player stop for a single
		/// frame, fire, and move off with a full-damage projectile in flight. Minions and sentries are
		/// the opposite — they recalculate from live player stats every frame
		/// (Projectile.cs:15924) — so a summoner would eat the penalty constantly with no counterplay,
		/// and melee, computed at contact, would eat it in full too. The penalty would land hardest on
		/// the classes that kite worst and let the best kiters off free.
		///
		/// ModifyHitNPCWithProj routes through ModifyHitNPC (PlayerLoader.cs:1606), so this one hook
		/// covers melee, ranged, magic and minions alike, and every one of them is judged on where the
		/// player actually is when the damage lands.
		/// </summary>
		public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (!IsBleeding)
				return;

			modifiers.FinalDamage *= IsPlanted ? PlantedDamageMultiplier : MovingDamageMultiplier;
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
			// Our own bleed re-enters Hurt every tick it lands. It is not a fresh hit.
			if (applyingStaggerDamage)
				return;

			// Nothing here touches the stance timer. Being hit does not itself break your stance — the
			// knockback the hit carries does, by moving you. A hit that lands without knockback leaves
			// you planted, which is correct: you did not move.
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
			// IsStandingStillForSpecialEffects is vanilla's own definition of stationary (|velocity|
			// under 0.05 on both axes) — the one Shiny Stone uses. Counted here rather than in
			// PostUpdate because Player.Update runs PostUpdateMiscEffects immediately before
			// UpdateLifeRegen (Player.cs:24940), and UpdateLifeRegen reads the result; that is the same
			// point in the frame at which vanilla reads this property for Shiny Stone, so we see the
			// velocity vanilla sees. Clamped rather than left to climb, so it cannot overflow across a
			// long session.
			//
			// Deliberately NOT gated on itemAnimation == 0 the way Shiny Stone is: planting your feet
			// to swing is the entire point of the stance. Only movement breaks it.
			if (!player.IsStandingStillForSpecialEffects)
				ticksStandingStill = 0;
			else if (ticksStandingStill < PlantTicks)
				ticksStandingStill++;

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

		/// <summary>
		/// Which side of the trade is live right now, for the buff tooltip.
		///
		/// This is worth surfacing rather than leaving to be felt: the two stances are a 3x swing in
		/// damage dealt, and a player who cannot see which one they are in reads that as their damage
		/// numbers randomly tripling.
		/// </summary>
		public string GetStanceText()
		{
			return IsPlanted
				? "Planted: +50% damage"
				: "Moving: double regeneration, half damage";
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
					tip = $"Taking {(int)stagger.GetTotalStaggeredDamage()} damage over {stagger.GetLongestStaggerDuration():F1} seconds"
						+ $"\n{stagger.GetStanceText()}";
					return;
				}
			}

			tip = "Taking damage over time";
		}
	}
}
