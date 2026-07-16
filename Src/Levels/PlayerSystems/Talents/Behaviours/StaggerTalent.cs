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
	/// <b>Mind over Matter: the bleed is paid from mana, not life.</b> Maximum mana is raised by half of
	/// maximum life, and every tick of the bleed drains mana first — life is only touched once mana runs
	/// out. Mana is the talent's real health bar, and life is what is left when you mismanage it.
	///
	/// <b>Why mana, and not more regeneration.</b> Flat life regeneration cannot scale: it was 8/s at
	/// level 1 and 8/s at level 100, against enemy damage that grows 14%/level without a cap, so it fell
	/// off no matter what number it was given. Vanilla's mana regeneration is PROPORTIONAL to the pool
	/// (statManaMax2/3 per tick, doubled again for standing still — Player.UpdateManaRegen), so tying the
	/// pool to maximum life means the sustain scales with every point of Strength and every life bonus,
	/// forever. Vanilla also already tuned the resource minigame we want: regeneration runs at 20% when
	/// the pool is empty against 100% when it is full, and the post-hit delay is itself longer the emptier
	/// you are. Letting the pool bottom out is punishing, and it is supposed to be.
	///
	/// <b>Three answers, not one.</b> Stand still and vanilla's own doubling refills the pool; crit and
	/// burn a quarter of the bleed off outright; or simply out-last it behind 150% more defense. The
	/// talent asks whether you can afford to stop, whether you can land a crit, and whether you budgeted
	/// your mana — and it is a wall only for as long as all three answers hold.
	///
	/// <b>Why no damage lever.</b> A move/plant damage trade made this a positioning minigame with an
	/// offensive upside, which is neither what the talent is for nor what separates it from its
	/// neighbours. Juggernaut is the wall that cannot move; Vengeance takes the hit and buys the life back
	/// by attacking. Stagger pays in mana.
	///
	/// This also makes Intellect a live stat here — where Juggernaut kills it dead — so the three pinnacle
	/// picks now want three different attribute spreads.
	/// </summary>
	public class StaggerTalent : TalentBehaviour
	{
		public override string Id => "stagger";
		public override string SlotKey => "pinnacle";
		public override string DisplayName => "Stagger";

		public override string Description =>
			"150% more defense, +150 maximum life and +8 life regeneration per second. Your maximum mana "
			+ "is increased by 50% of your maximum life. 55% of the damage you take is dealt to you over "
			+ "5.5 seconds instead of all at once, and that damage is drained from your mana before it "
			+ "touches your life — but only while you have mana left to arm it. Critical hits burn away "
			+ "25% of the remaining damage, once per second. Your mana does not recover while you move: "
			+ "stand still and it floods back, and your life regeneration doubles. Hold your ground and "
			+ "pay in mana.";

		private const float DamagePercent = 0.55f;
		private const float Duration = 5.5f;
		private const float DefenseBonus = 1.50f;
		private const float FlatLife = 150f;

		/// <summary>
		/// Maximum mana granted as a fraction of maximum life. Untested first guess, and the single most
		/// important dial on the talent: it sets how many hits the pool can absorb before the cliff.
		/// </summary>
		private const float ManaPerMaxLifeFraction = 0.50f;

		/// <summary>
		/// How much of the outstanding bleed a critical hit burns off, and how often it may do so.
		///
		/// Both untested first guesses. A fraction rather than a flat number so it never falls off, and
		/// because it makes the purge worth most immediately after a big hit — crit INTO a fresh bleed
		/// rather than chip at a stale one. The cooldown is what keeps this from collapsing into
		/// Vengeance's "attack to sustain" loop: it is a burst clear on a timer, not a per-hit drip that
		/// scales with weapon speed.
		/// </summary>
		private const float CritPurgeFraction = 0.25f;
		private const int CritPurgeCooldownTicks = 60;

		/// <summary>
		/// In HP per second, which is NOT the unit Terraria stores: player.lifeRegen counts half-HP
		/// per second (120 lifeRegenCount = 1 HP, accumulated 60 times a second). The dictionary below
		/// doubles this on the way in, so the constant reads in the units the talent description uses.
		/// </summary>
		private const float FlatRegenPerSecond = 8f;

		/// <summary>
		/// Untested first guess, and the only dial the stance has left. See the class docs.
		///
		/// There is deliberately no commitment threshold in front of it. That existed to stop a
		/// frame-perfect trick against the old planted damage bonus — run for the healing, stop for the
		/// single frame your hit lands, bank both. With no damage lever there is nothing to game: regen
		/// accrues per tick, so a player who stops for half a second gets exactly half a second of
		/// doubled regeneration and no more.
		/// </summary>
		private const float StandingStillRegenMultiplier = 2f;

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
		/// standing-still doubling below catches it, taking it to 16 HP/s.
		///
		/// The stagger bleed itself does NOT wipe it — StaggerDebuff is display-only and never sets
		/// player.bleed, which vanilla would otherwise treat as a regen stopper. So this is the
		/// regeneration you answer your own bleed with, and standing still is what doubles it.
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

		/// <summary>Ticks until a critical hit may burn the bleed again. Counted down in PostUpdate.</summary>
		private int critPurgeCooldown;

		/// <summary>
		/// Maximum mana is half of maximum life, on top of whatever the player already had — mana
		/// crystals and Intellect still count, so this raises the ceiling rather than replacing it.
		///
		/// <b>statLifeMax2 here is LAST frame's maximum life, and that is fine.</b> Player.ResetEffects
		/// runs PlayerLoader.ModifyMaxStats and only THEN assigns statLifeMax2 = statLifeMax
		/// (verified: ResetEffects:94-96), so while we are inside this hook the field still holds the
		/// previous frame's resolved total. A one-frame lag cannot compound — maximum life only moves on
		/// level-up, respec or a gear swap, and mana never feeds back into life, so there is no loop for
		/// the lag to oscillate in.
		///
		/// <b>The ref health parameter would be the WRONG source</b>, tempting as it looks. At this point
		/// it holds only the contributions of THIS ModPlayer (TalentPlayer) — tML CombineWith's every
		/// other ModPlayer's afterwards — so reading it would silently miss every point of Strength and
		/// the entire mastery tree. statLifeMax2 is the only place the combined total exists.
		///
		/// Base rather than Flat, so MaxManaPercent scales it, mirroring why Strength uses Base
		/// (CLAUDE.md §5).
		///
		/// No Juggernaut collision to worry about: it zeroes statManaMax2 every frame, but both talents
		/// are in the pinnacle slot and so are mutually exclusive by construction.
		/// </summary>
		public override void ModifyMaxStats(Player player, ref StatModifier health, ref StatModifier mana)
		{
			mana.Base += player.statLifeMax2 * ManaPerMaxLifeFraction;
		}

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
			critPurgeCooldown = 0;

			int buffType = ModContent.BuffType<StaggerDebuff>();
			if (player.HasBuff(buffType))
				player.ClearBuff(buffType);
		}

		/// <summary>
		/// Stand still and your regeneration doubles. Applies to the flat component only — gear, buffs,
		/// and this talent's own +8/s, taking it to 16. It is unconditional: there is no bleed gate,
		/// because there is no longer a cost to gate. A bonus with no downside does not need a window
		/// in which the downside is fair.
		///
		/// IsStandingStillForSpecialEffects is vanilla's own definition of stationary (|velocity| under
		/// 0.05 on both axes) — the one Shiny Stone uses. Read straight off the player here rather than
		/// cached a hook earlier, because this is the very point in the frame at which vanilla reads it
		/// for Shiny Stone (Player.cs:24940), so we see the velocity vanilla sees. Deliberately NOT
		/// gated on itemAnimation == 0 the way Shiny Stone is: planting your feet to swing should still
		/// heal you.
		///
		/// <b>There is deliberately no NaturalLifeRegen counterpart.</b> The flat component is the right
		/// lever because vanilla already handles the ramp itself: it scales the natural ramp x1.25 while
		/// stationary against x0.5 while moving (Player.cs:18003), so standing still is rewarded there
		/// without us touching it. Flat regen has no such vanilla treatment, which is exactly why it is
		/// the part worth doubling.
		///
		/// The lifeRegen > 0 guard is real: a vanilla DoT has already driven it negative by the time we
		/// run, and doubling a negative would make Poison and On Fire twice as lethal.
		/// </summary>
		public override void UpdateLifeRegen(Player player)
		{
			if (!player.IsStandingStillForSpecialEffects || player.lifeRegen <= 0)
				return;

			player.lifeRegen = (int)(player.lifeRegen * StandingStillRegenMultiplier);
		}

		/// <summary>
		/// Mana only comes back when you plant your feet. Without this the talent does not exist.
		///
		/// <b>The problem this solves.</b> Vanilla's mana regeneration is PROPORTIONAL to the pool —
		/// which is the property that made mana worth tying to max life in the first place, and is
		/// also what made the bleed invisible. UpdateManaRegen computes
		/// <c>manaRegen = statManaMax2/3 + 1</c> <i>per frame</i>, and 120 accumulated points is one
		/// mana, so the pool refills at roughly <c>pool/5.2</c> per second moving and <c>pool/2.6</c>
		/// standing still — about 122 and 244 mana/s at a 638 pool, against a bleed that drains 0.2 to
		/// 53/s. The pool refilled from empty in 4.4 seconds and could not visibly move.
		///
		/// <b>Enlarging or shrinking the pool cannot fix that</b>, which is the trap worth remembering:
		/// regen scales with the pool by exactly the same factor, so ManaPerMaxLifeFraction only sets
		/// how big a burst the pool absorbs, never how scarce it is. The refill RATE is the only dial
		/// that changes scarcity, and vanilla gives us no hook for it — ModPlayer has no mana-regen
		/// hook at all (only ModifyMaxStats/GetHealMana/ModifyManaCost/OnMissingMana/OnConsumeMana).
		///
		/// <b>Why manaRegenBonus is nonetheless the right seam.</b> Vanilla reads it as a plain addend
		/// BEFORE the standing-still bonus is added:
		/// <code>
		///     manaRegen = statManaMax2/3 + 1 + manaRegenBonus;
		///     if (IsStandingStillForSpecialEffects || grappling[0] >= 0 || manaRegenBuff)
		///         manaRegen += statManaMax2/3;               // <- AFTER the bonus
		/// </code>
		/// so subtracting exactly the base term zeroes the passive drip while leaving the standing-still
		/// term untouched. Planting your feet does not merely double the regen any more — it is the
		/// entire regen. That is the talent's whole thesis, and it costs one line rather than a custom
		/// resource, keeping vanilla's num2 spiral (20% at empty vs 100% at full) and its
		/// emptier-means-longer regen delay for free.
		///
		/// <b>Contributed from PostUpdateMiscEffects, and it must be.</b> ResetEffects zeroes
		/// manaRegenBonus (:323) and UpdateManaRegen reads it (Update:1637), which runs immediately
		/// AFTER PostUpdateMiscEffects (:1634). Contributing from ResetEffects would race the wipe; §8's
		/// CombatEffectStats rule is the same shape.
		///
		/// <b>Unconditional, NOT gated on bleeding</b> — hence its position above the instances.Count
		/// early-return. Gated, the pool would refill at the full 122/s the instant the last bleed
		/// expired, which is a 5.5s wait for a full reservoir and no cost at all.
		///
		/// <b>RegenSuppression must stay in [0,1].</b> At 1 we subtract exactly the base term and
		/// manaRegen floors at the literal +1 vanilla adds. Above 1 manaRegen goes negative,
		/// manaRegenCount drifts negative without bound (the <c>while (manaRegenCount >= 120)</c> payout
		/// simply never fires), and the pool would then owe that debt back before the first point of
		/// mana ever returned.
		///
		/// Two escape hatches survive on purpose, both costing the player something real: a Mana
		/// Regeneration potion sets manaRegenBuff, which vanilla treats exactly like standing still, so
		/// it buys mobile regen for a buff slot; and Mana Potions write statMana directly and are
		/// untouched by any of this, which keeps them the panic button the design intends.
		///
		/// Untested first guess, like every other constant here.
		/// </summary>
		private const float RegenSuppression = 1f;

		private void SuppressPassiveManaRegen(Player player)
		{
			player.manaRegenBonus -= (int)(player.statManaMax2 / 3f * RegenSuppression);
		}

		/// <summary>
		/// The crit purge's cooldown.
		///
		/// PostUpdate rather than PostUpdateMiscEffects, which is where the rest of this talent's
		/// per-frame work lives: that hook returns early when there is no live bleed, so counting down
		/// there would stall the cooldown exactly while the player is NOT bleeding — i.e. it would still
		/// be running when the next hit lands, which is the one moment it must not be.
		/// </summary>
		public override void PostUpdate(Player player)
		{
			if (critPurgeCooldown > 0)
				critPurgeCooldown--;
		}

		/// <summary>
		/// Critical hits burn the bleed off. The third answer to your own stagger, alongside standing
		/// still and simply out-lasting it — and the only one that is offensive, which is what stops the
		/// talent from being purely a question of whether you can afford to stand still.
		///
		/// <b>Both numbers are scaled, not just the remaining total.</b> RemainingDamage alone would leave
		/// DamagePerSecond untouched, so the bleed would keep draining at full rate and merely finish 25%
		/// sooner — the same damage-per-second in a shorter window, which is not what "burn 25% of it"
		/// means and is barely a defensive gain at all. Scaling the rate by the same fraction keeps the
		/// 5.5s shape and makes the bleed genuinely weaker.
		///
		/// It also avoids a subtler trap: instances are only ever removed when TimeRemaining runs out, not
		/// when RemainingDamage hits zero, so a purged-to-empty instance would otherwise linger as a
		/// zombie holding one of the MaxInstances slots that gate whether new hits get staggered at all.
		/// At 25% per purge nothing reaches zero, but the invariant is worth not depending on.
		///
		/// StaggerInstance is a class, so mutating through the loop variable writes through — no
		/// store-back needed, same as the tick loop in PostUpdateMiscEffects.
		///
		/// This covers melee, ranged, magic and minions alike: ModifyHitNPCWithProj routes through
		/// OnHitNPC, and TalentPlayer dispatches it for every source.
		/// </summary>
		public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!hit.Crit || critPurgeCooldown > 0 || instances.Count == 0)
				return;

			float purged = 0f;
			foreach (StaggerInstance instance in instances)
			{
				float removed = instance.RemainingDamage * CritPurgeFraction;
				instance.RemainingDamage -= removed;
				instance.DamagePerSecond *= 1f - CritPurgeFraction;
				purged += removed;
			}

			critPurgeCooldown = CritPurgeCooldownTicks;

			// Only worth announcing if it actually removed something a player could notice.
			if (purged >= 1f && Main.netMode != Terraria.ID.NetmodeID.Server)
				CombatText.NewText(player.getRect(), Color.Cyan, $"{(int)purged} Purged", false, false);
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

			// The cliff: no mana, no stagger. Mana is what the bleed is paid from, so with an empty pool
			// there is nothing to arm the split and the hit lands whole. Checked here, before mitigating,
			// for exactly the same reason as the instance cap above.
			//
			// Note this is deliberately binary on EXISTENCE, not sufficiency: one point of mana buys a
			// full 55% split, and only a genuinely empty pool disarms it. Mana's quantity is what decides
			// how much of the resulting bleed it can absorb (see PostUpdateMiscEffects); mana's existence
			// is what decides whether there is a bleed at all. Gating on "enough mana to cover the whole
			// hit" instead would make the talent switch off hardest precisely when a big hit lands, which
			// is when the player needs the window most.
			//
			// The cliff has a way out of itself, and it falls out of this early return rather than being
			// built: bailing here leaves pendingStaggerPercent at 0, so PostHurt early-returns too and
			// never reaches the manaRegenDelay line. An empty pool therefore keeps regenerating even while
			// you are being hit — slowly, at UpdateManaRegen's 20%-when-empty rate — and the first point of
			// mana re-arms the split. Worth preserving: without it the twin spirals (20% rate AND a ~199
			// tick delay at empty) could make a bottomed-out pool unrecoverable mid-fight.
			if (player.statMana <= 0)
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

			// Suppress mana regeneration the way a spell cast would. We drain statMana directly rather
			// than through Player.CheckMana, and CheckMana does not touch the delay anyway — vanilla sets
			// it from ItemCheck_ApplyManaRegenDelay, which does exactly this:
			//     if (GetManaCost(sItem) > 0) manaRegenDelay = (int)maxRegenDelay;
			// so mirroring it (int cast included) is the closest thing to a "we spent mana" signal there
			// is. While the delay is above zero, UpdateManaRegen sets manaRegen = 0 outright.
			//
			// maxRegenDelay is vanilla's own, recomputed every frame in Player.Update as
			//     ((1 - statMana/statManaMax2) * 240 + 45) * 0.7
			// i.e. ~31 ticks at a full pool and ~199 at an empty one. Using it rather than a constant of
			// our own is the whole point: the delay gets LONGER the emptier you are, which stacks with
			// UpdateManaRegen's 20%-at-empty rate to punish a mismanaged pool on a curve vanilla already
			// tuned. That is the resource minigame, and we get it for free.
			//
			// Tied to being HIT rather than to each drain tick on purpose. The bleed ticks several times a
			// second for 5.5s, so refreshing the delay per tick would pin regeneration at zero for the
			// entire window and feed the cliff. "Stop taking hits and the pool comes back" is the intent.
			//
			// Max, not assignment: never SHORTEN a delay the player already owes from casting.
			player.manaRegenDelay = System.Math.Max(player.manaRegenDelay, (int)player.maxRegenDelay);

			if (Main.netMode != Terraria.ID.NetmodeID.Server)
				CombatText.NewText(player.getRect(), Color.Orange, $"{(int)staggeredDamage} Staggered", false, false);
		}

		public override void PostUpdateMiscEffects(Player player)
		{
			SuppressPassiveManaRegen(player);

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

				// Mana pays first — this is the talent. When the pool covers the tick we never reach
				// Player.Hurt at all, which means no re-entrancy, no invulnerability frames, no knockback
				// and no death check: the damage simply never becomes damage. Written straight to statMana
				// rather than through CheckMana because CheckMana is an item-cost API (it applies
				// manaCost, consults the Mana Flower, and fires OnConsumeMana) and none of that describes
				// a bleed being absorbed.
				if (player.statMana > 0)
				{
					int fromMana = System.Math.Min(player.statMana, toApply);
					player.statMana -= fromMana;
					toApply -= fromMana;
				}

				// Whatever mana could not cover still has to land. A pool that outlives its mana is a debt
				// already owed: the cliff in ModifyHurt governs whether NEW hits get split, but refusing
				// to resolve an EXISTING bleed here would turn an empty pool into infinite free
				// mitigation, which is the exact bug the instance-cap comment warns about.
				if (toApply > 0)
				{
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
					Player player = Main.LocalPlayer;

					// The mana line is the important one and is deliberately live: the pool is what
					// decides whether this bleed costs anything at all, so the player needs to watch it
					// draining against the debt, not be told a static rule about it. The last line is
					// static because both of those effects always apply.
					// The stance line is live because the pool only refills while planted — the player
					// needs to see which of the two states they are actually in, not be told the rule.
					bool planted = player.IsStandingStillForSpecialEffects;

					tip = $"Taking {(int)stagger.GetTotalStaggeredDamage()} damage over {stagger.GetLongestStaggerDuration():F1} seconds"
						+ $"\nPaid from mana: {player.statMana}/{player.statManaMax2}"
						+ (planted ? "\nPlanted — mana flooding back" : "\nMoving — mana will NOT recover")
						+ "\nCritical hits burn 25% of it";
					return;
				}
			}

			tip = "Taking damage over time";
		}
	}
}
