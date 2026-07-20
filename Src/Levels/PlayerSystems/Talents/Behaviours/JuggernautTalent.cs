using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerSystems.Attributes;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// Pinnacle: Juggernaut. An armoured wall that cannot chase anything down.
	///
	/// Its life is deliberately "increased" rather than "more" — it lands in StatModifier.Additive
	/// and merely sums with Fortitude/Royal Jelly/Avatar of Flesh, where Vengeance's doubling
	/// compounds against them. Juggernaut used to be the default pick on raw life alone; the hit cap
	/// and its Strength-driven defense are what it trades for, and it pays in mobility.
	///
	/// It used to carry a flat 50% more defense as well. That went on 2026-07-17 when all three
	/// pinnacles surrendered their defense multipliers to ClassBaselines — see percentBonuses. The
	/// Strength line survives and is now the only defense that separates this talent from the others.
	///
	/// It also kills two of your three attributes outright — Dexterity and Intellect grant nothing at
	/// all, neither their attack-speed/mana line nor their damage line. Since the mastery gate keys
	/// off TOTAL attribute points rather than Strength, that dead weight is a real cost to build
	/// breadth and not just to the stat line.
	///
	/// The damage suppression is not incidental. When attributes gained an offensive line, leaving
	/// these flags narrow would have quietly handed a ranged Juggernaut a live Dexterity and softened
	/// the one cost that stops this pick reading as strictly-best. Juggernaut is Strength's build; the
	/// other two staying dead is the price of the wall.
	///
	/// The suppression is declared via Suppresses rather than enforced here. Contributors ask
	/// TalentPlayer.Suppresses(...) before applying themselves, which means there is no dependency
	/// on this talent's hooks running before theirs.
	///
	/// <b>Nothing can take more than half your life in one hit.</b> That is the wall's own flavour of
	/// staying power, and it is a different promise from its neighbours in the slot: Stagger spreads a
	/// hit out over time, Vengeance takes it whole and buys the life back, and Juggernaut simply
	/// refuses to accept more than half of the bar at once. It cannot be one-shot from full — but the
	/// cap is per hit, not per second, so two hits still kill and the -50% movement means walking out
	/// of the second one is not on the table. It buys a guaranteed second hit's worth of reaction time,
	/// not immortality.
	///
	/// <b>Resolve is the wall's staying power</b> (reworked 2026-07-20, replacing Entrenchment). The
	/// more damage you have taken in the last ResolveWindowSeconds, the harder you become — a 0..1 value
	/// driven by a sliding damage window (the exact shape as Vengeance's ramp), NOT by standing still. It
	/// scales two proportional payoffs together: up to +20% damage reduction (Player.endurance —
	/// multiplicative, so no subtractive-defense cliff, CLAUDE.md §5b) and up to 4% of maximum life per
	/// second in regen (the same scaling shape as Stagger's moving 2%/s).
	///
	/// It is movement-agnostic on purpose. Entrenchment (the design this replaces) paid out only while
	/// perfectly still — unreachable in a bullet-hell boss fight where you must dodge, doubly so under
	/// this talent's -50% movement — so its mitigation peaked when you were safe and vanished under
	/// pressure. That is the exact anti-synergy the mod already fought on Vengeance's ramp
	/// ("it peaks at exactly the moment you most need to leave"). Resolve inverts it: it peaks precisely
	/// when you are being pounded, whether you are planted or kiting.
	///
	/// <b>Self-stabilising, not spiralling.</b> The window records POST-mitigation damage
	/// (Player.HurtInfo.Damage), so as Resolve raises endurance each hit lands smaller and therefore
	/// records smaller — Resolve settles at an equilibrium instead of running away. Reaching the cap
	/// takes genuinely heavy incoming fire, which is the point. This REPLACES the old flat Strength
	/// regen, the only pinnacle sustain that did not scale (65 HP/s against Stagger's 144 and Vengeance's
	/// 111, .scripts/juggernaut_sustain.py) and faded further every world level; Resolve's regen is
	/// proportional, so it holds forever.
	///
	/// <b>Vanilla DoTs never build Resolve and switch its regen off, and both are the point.</b> DoTs
	/// bypass Player.Hurt/PostHurt entirely, so they never enter the window — the same reason they bypass
	/// the hit cap; they remain the wall's answer key. And the Resolve regen is a flat ADD in
	/// PostUpdateMiscEffects, so it lands in lifeRegen BEFORE Player.UpdateLifeRegen's debuff block runs
	/// `if (lifeRegen > 0) lifeRegen = 0` for Poisoned/On Fire/etc — exactly like Stagger's moving regen
	/// and every other regen source. Potions are the counter-play, because a direct heal is not
	/// regeneration and ignores all of it. The damage-reduction half still applies under a DoT — only the
	/// regen is switched off.
	/// </summary>
	public class JuggernautTalent : TalentBehaviour
	{
		public override string Id => "juggernaut";
		public override string SlotKey => "pinnacle";
		public override string DisplayName => "Juggernaut";

		public override string Description =>
			"+50% maximum life. 65% more damage. No single hit can take more than half of your maximum "
			+ "life. RESOLVE: the more damage you have taken in the last 10 seconds, the harder you "
			+ "become — up to 20% damage reduction and 4% of your maximum life per second in regeneration "
			+ "— strongest while you are under fire and fading in a lull. Each point of Strength also "
			+ "grants +1% defense. Healing potions recharge four times faster, heal 50% more, and double "
			+ "your life regeneration for 4 seconds. -15% attack speed, and attack speed cannot be "
			+ "increased. -50% movement speed. You permanently have no mana. Dexterity and Intellect "
			+ "grant you nothing.";

		private const float LifeBonus = 0.50f;

		/// <summary>
		/// Strength's Juggernaut-only defense line: +1% defense per point.
		///
		/// ADDITIVE with Strength's normal +2 max life and +0.5% melee damage — Strength does strictly
		/// more for a Juggernaut than for anyone else. That is a real design cost knowingly paid: the
		/// talent's price is that Dexterity and Intellect grant nothing and the breadth gate forces ~50
		/// attribute points regardless, so this hands the already-forced Strength investment a payoff.
		/// It is the only defense that separates this talent from the other two.
		///
		/// The old +1 HP/s-per-point regen that sat here was removed on 2026-07-20 — it was flat and
		/// could not scale with world level; Resolve's proportional regen replaces it. At a realistic
		/// 40 Strength this is +40% defense. Untested first guess.
		/// </summary>
		private const float DefensePercentPerStrength = 0.01f;

		/// <summary>
		/// Potion cooldown multiplier. 0.25 turns vanilla's 60s (Item.potionDelay = 3600, verified) into
		/// 15s, and compounds with the Philosopher's Stone's own 0.75 for 11s.
		///
		/// Worth ~13 HP/s as a rate, which is not why it is here. Its value is that the panic button is
		/// available often, and that pairs with the hit cap specifically: the cap guarantees you survive
		/// hit #1 from full, and a potion is what buys back the margin to survive hit #2. It is also the
		/// only sustain this talent has that a DoT cannot switch off.
		/// </summary>
		private const float PotionDelayMultiplier = 0.25f;

		/// <summary>+50% potion healing, via the consumables-only channel. Untested first guess.</summary>
		private const float PotionHealingBonus = 0.50f;

		/// <summary>
		/// A drunk potion also doubles life regeneration, decaying linearly to nothing over 4 seconds.
		///
		/// This is an "increased" bonus and means it: it multiplies whatever regen you already have, so
		/// it is worth ~nothing while you have no Resolve and most while dug in, and it is worth exactly
		/// zero while a DoT is holding lifeRegen at or below zero. That is the intended reading — it makes
		/// the potion and the Resolve regen multiply rather than merely add, and it does not rescue the
		/// DoT matchup that the direct heal is there to answer.
		/// </summary>
		private const float PotionRegenBonus = 1.00f;
		private const int PotionRegenDurationTicks = 240;

		private int potionRegenTicks;

		/// <summary>
		/// Resolve ramp, 0..1, driven by the sliding damage window below. Scales both the damage
		/// reduction and the regen together. Exposed for the HUD readout; never cache the instance
		/// (TalentPlayer recreates behaviours on pick changes). Recomputed every frame from the window,
		/// so it can never drift from the payoff it drives.
		/// </summary>
		private float resolve;
		public float Resolve => resolve;
		public float DamageReduction => ResolveEndurance * resolve;

		/// <summary>
		/// Each hit taken, held for ResolveWindowSeconds then dropped — the sliding window that drives
		/// Resolve. Same shape as VengeanceTalent's list; Juggernaut owns its own DamageEntry copy
		/// (behaviours are not shared). Records POST-mitigation damage (info.Damage), which is what makes
		/// Resolve self-stabilising rather than a spiral.
		/// </summary>
		private readonly List<DamageEntry> recentHits = new List<DamageEntry>();

		/// <summary>
		/// Resolve tuning, all untested first guesses (see todo.md). ResolveWindowSeconds is how long each
		/// hit keeps hardening you; ResolvePerLifeFraction sets how fast Resolve fills (1.5 → full at ~67%
		/// of max life taken inside the window — the ramp-speed dial). At full it is worth ResolveEndurance
		/// damage reduction (multiplicative, on top of the 50% warrior baseline = 70% total; tunable to
		/// 0.25 = Fargo's 0.75 endurance cap) and ResolveRegenFraction of max life per second in regen.
		/// The two ceilings are unchanged from the Entrenchment design they replace — only the driver
		/// changed, from standing still to taking damage.
		/// </summary>
		private const float ResolveWindowSeconds = 10f;
		private const float ResolvePerLifeFraction = 1.5f;
		private const float ResolveEndurance = 0.20f;
		private const float ResolveRegenFraction = 0.04f;

		/// <summary>
		/// The most of your maximum life any one hit is allowed to take. Untested first guess in the
		/// sense that 0.5 is the number asked for rather than one played with — but note it is not a
		/// dial with a smooth curve behind it. Above ~0.5 it stops mattering (few hits get there through
		/// this much defense); much below it, the wall stops being killable by anything that is not a
		/// DoT, since the cap re-arms on every hit.
		/// </summary>
		private const float MaxHitFractionOfMaxLife = 0.50f;

		/// <summary>
		/// Note this is "more", not "increased": StatApplier's GenericDamage percent case does
		/// GetDamage(Generic) *= 1f + value, which lands in StatModifier.Multiplicative and compounds
		/// with gear rather than summing into the additive bucket. The old description said
		/// "+50% damage", which understated what it was already doing.
		/// </summary>
		private const float DamageBonus = 0.65f;

		private const float AttackSpeedPenalty = 0.15f;
		private const float MoveSpeedPenalty = 0.50f;

		private static readonly Dictionary<string, float> percentBonuses = new Dictionary<string, float>
		{
			{ "MaxLifePercent", LifeBonus },
			// The flat "50% more defense" that used to sit here was removed on 2026-07-17, along with
			// Stagger's x2.5 and Vengeance's x0.7 penalty — see ClassBaselines. Defense is subtractive
			// and vanilla floors damage to 1 before FinalDamage, so a pinnacle-wide defense SPREAD is
			// a cliff, not a gradient, and it was why this talent felt fine against trash and hopeless
			// against bosses. Mitigation now comes from ClassBaselines.WarriorEndurance (which every
			// warrior has and which is multiplicative) plus this talent's own Resolve endurance.
			//
			// The Strength-driven defense in ApplyStrengthDefense BELOW SURVIVES, and it is now the
			// only thing separating this talent from the other two on subtractive defense — which is the
			// right shape: it is bought with an attribute investment the talent already forces, rather
			// than handed over as a flat multiplier.
			{ "GenericDamage", DamageBonus },
			{ "MovementSpeed", -MoveSpeedPenalty },
		};

		public override IReadOnlyDictionary<string, float> PercentBonuses => percentBonuses;

		public override TalentSuppression Suppresses =>
			TalentSuppression.AttackSpeedIncreases | TalentSuppression.ManaFromIntellect
			| TalentSuppression.DamageFromDexterity | TalentSuppression.DamageFromIntellect;

		/// <summary>
		/// The hit cap. SetMaxDamage is the right tool and the only one that expresses this: FinalDamage
		/// is a multiplier and cannot say "no more than N", and writing the clamp by hand in PostHurt
		/// would mean refunding life after the hit had already landed — visibly, and after anything that
		/// reads statLife on the way through.
		///
		/// Three properties of vanilla's implementation are what make this safe rather than lucky
		/// (Player.cs:731/790):
		/// - It clamps LAST, after SourceDamage, IncomingDamageMultiplier, defense, armour penetration
		///   and FinalDamage have all resolved. So this is a bound on the damage actually dealt, and
		///   the defense underneath still does its work — the cap only binds on the hits big enough to
		///   punch through it. (Measured: at WL20 the cap is DORMANT against every boss except Duke
		///   Fishron; defense is what is actually binding until ~WL50. See .scripts/juggernaut_stuck.py.
		///   The two are mutually exclusive per hit — a hit big enough to be capped had its defense
		///   subtraction rendered irrelevant, and a hit small enough for defense to blunt never reaches
		///   the cap.)
		/// - Multiple callers take the LOWEST limit (Math.Min), so this composes with any other mod's
		///   cap and does not depend on hook order.
		/// - It floors at 1, so there is no way to reach a zero-damage invulnerability, however small
		///   maximum life gets.
		///
		/// statLifeMax2 is the right field, though not for the reason it looks like. Both it and
		/// statLifeMax already contain this talent's own +50% and every Strength point: ResetEffects
		/// runs PlayerLoader.ModifyMaxStats FIRST — which resets statLifeMax to the vanilla base
		/// (100 + crystals*20 + fruit*5) and applies every ModPlayer's contribution to it — and only
		/// then does statLifeMax2 = statLifeMax (Player.cs:17200, PlayerLoader.cs:439/451). So
		/// statLifeMax is NOT the pre-bonus base. What statLifeMax2 adds on top is vanilla's own
		/// life bonuses, which makes it the bar the player actually sees — and "half your maximum
		/// life" has to mean half of the number on the screen.
		///
		/// The read is safe at this point in the frame: statLifeMax2 is resolved during ResetEffects,
		/// long before any hit is processed, and the field is initialised to 100 rather than 0. It can
		/// never be zero here, so vanilla's floor-at-1 can never quietly become the binding cap.
		///
		/// Note this bounds hits, not damage over time. Vanilla DoTs bypass Player.Hurt entirely and
		/// subtract through lifeRegen, so they are unaffected — which is correct, and is the wall's
		/// intended answer key.
		/// </summary>
		public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
		{
			modifiers.SetMaxDamage((int)(player.statLifeMax2 * MaxHitFractionOfMaxLife));
		}

		/// <summary>
		/// Feed the Resolve window. info.Damage is the POST-mitigation number, so a tankier wall records
		/// smaller hits and Resolve settles at an equilibrium instead of spiralling — the same property
		/// that makes Vengeance's fraction-of-max ramp stable. DoTs never reach here (they bypass
		/// Player.Hurt), so they never build Resolve; that is deliberate and mirrors the hit cap.
		/// </summary>
		public override void PostHurt(Player player, Player.HurtInfo info)
		{
			if (info.Damage <= 0)
				return;

			recentHits.Add(new DamageEntry { Damage = info.Damage, TimeRemaining = ResolveWindowSeconds });
		}

		/// <summary>
		/// The potion cooldown. <b>This MUST be contributed from ResetEffects and nowhere else</b>, and
		/// the reason is not style — it is a live bug in any other hook.
		///
		/// Player.Update stores the old modifier, resets it, applies the Philosopher's Stone, and only
		/// then calls ResetEffects() — which is what dispatches this (Player.cs:22738/22742/22745/22750,
		/// PlayerLoader.cs:16999). Later, at Player.cs:22892, it compares the stored old value against
		/// the rebuilt one and calls AdjustRemainingPotionSickness if they differ, rescaling any
		/// in-flight Potion Sickness to the new multiplier.
		///
		/// PostUpdateEquips (22942) and PostUpdateMiscEffects (23190) both run AFTER that comparison.
		/// Contributing from either would mean the comparison sees last frame's 0.25 against a current
		/// 1.0 EVERY frame, so AdjustRemainingPotionSickness would fire every frame and multiply the
		/// remaining sickness by 4 each time — a permanent, compounding Potion Sickness. From here the
		/// value is stable frame to frame, the comparison finds nothing, and the rescale only happens
		/// when it should: on picking or clearing the talent.
		///
		/// Note this is the one CombatEffectStats-adjacent contribution that belongs in ResetEffects.
		/// The rule in CLAUDE.md §8 ("do not contribute from ResetEffects, it races the wipe") is about
		/// CombatEffectStats, which zeroes its own pool in its own ResetEffects. PotionDelayModifier is
		/// vanilla's field, wiped by vanilla at 22742, strictly before any ModPlayer hook sees it.
		/// </summary>
		public override void ResetEffects(Player player)
		{
			// *=, not assignment: StatModifier's Multiplicative channel compounds, so this stacks with
			// the Philosopher's Stone rather than overwriting it (0.25 * 0.75 = 11s).
			player.PotionDelayModifier *= PotionDelayMultiplier;
		}

		public override void PostUpdate(Player player)
		{
			if (potionRegenTicks > 0)
				potionRegenTicks--;

			// Age out the Resolve window. Same literal sliding-window as VengeanceTalent — a hit is
			// dropped exactly ResolveWindowSeconds after it landed, not decayed proportionally.
			if (recentHits.Count == 0)
				return;

			const float deltaTime = 1f / 60f;
			for (int i = recentHits.Count - 1; i >= 0; i--)
			{
				recentHits[i].TimeRemaining -= deltaTime;
				if (recentHits[i].TimeRemaining <= 0f)
					recentHits.RemoveAt(i);
			}
		}

		public override void OnPotionUsed(Player player, Item item)
		{
			potionRegenTicks = PotionRegenDurationTicks;
		}

		public override void OnRespawn(Player player)
		{
			// Never carry a Resolve ramp across a life.
			recentHits.Clear();
			resolve = 0f;
		}

		public override void OnDeactivate(Player player)
		{
			potionRegenTicks = 0;
			resolve = 0f;
			recentHits.Clear();
		}

		public override void PostUpdateMiscEffects(Player player)
		{
			// The attack-speed penalty is applied here rather than declared as a PercentBonus,
			// because StatApplier's AttackSpeed case refuses to run while AttackSpeedIncreases is
			// suppressed — which is correct for everyone else and would silently swallow our own
			// penalty. Suppression blocks increases; this is a decrease, and it still applies.
			ApplyAttackSpeedPenalty(player, DamageClass.Melee);
			ApplyAttackSpeedPenalty(player, DamageClass.Ranged);
			ApplyAttackSpeedPenalty(player, DamageClass.Magic);
			ApplyAttackSpeedPenalty(player, DamageClass.Summon);
			ApplyAttackSpeedPenalty(player, DamageClass.Generic);

			// No mana, permanently. statManaMax2 is recomputed every frame, so this has to be
			// reasserted every frame rather than set once.
			player.statManaMax2 = 0;
			player.statMana = 0;

			ApplyResolve(player);
			ApplyStrengthDefense(player);

			// The consumables-only channel, so this cannot reach LifeLeechApplier. Juggernaut has no
			// leech of its own, but an item can roll it, and "potions heal more" should not quietly
			// become "the leech you rolled heals more" — see CLAUDE.md §8 on the two channels.
			CombatEffectStats.Get(player).ConsumableHealingPercent += PotionHealingBonus * 100f;
		}

		/// <summary>
		/// Resolve — the wall's whole identity (reworked 2026-07-20). Driven by damage taken in the last
		/// ResolveWindowSeconds, not by standing still, so it hardens you whether you are planted or
		/// dodging and peaks exactly when you are under fire. The ramp scales BOTH payoffs together, so a
		/// wall under heavy fire hardens AND heals, and both fade as the fire lets up.
		///
		/// - <b>Damage reduction via Player.endurance</b> (multiplicative, no subtractive-defense cliff —
		///   the whole reason defense was pulled from the pinnacles on 2026-07-17, CLAUDE.md §5b). Added
		///   here, not in ResetEffects, because ResetEffects is where endurance is zeroed; ClassBaselines
		///   (which runs first, alphabetically) has already added the warrior's 0.50, and this stacks on
		///   top. Clamp to 1 to match ClassBaselines. On Fargo Eternity endurance is separately clamped
		///   to 0.75.
		/// - <b>Proportional life regen</b> — statLifeMax2 * fraction, the exact scaling shape as Stagger's
		///   moving 2%/s (StaggerTalent.ApplyMovingLifeRegen). A plain unguarded ADD in
		///   PostUpdateMiscEffects: it lands in lifeRegen before UpdateLifeRegen's DoT blocks (so a DoT
		///   positionally zeroes it, the wall's answer key) and before Second Wind's LifeRegenPercent (so
		///   that scales it). The * 2f is the half-HP-per-second unit. No lifeRegen > 0 guard, because it
		///   is an add — the guard exists on multiplies. This REPLACES the old flat Strength regen.
		/// </summary>
		private void ApplyResolve(Player player)
		{
			resolve = ComputeResolve(player);
			if (resolve <= 0f)
				return;

			player.endurance += ResolveEndurance * resolve;
			if (player.endurance > 1f)
				player.endurance = 1f;

			// lifeRegen is in HALF-HP per second, hence the 2. Getting this wrong halves the talent.
			player.lifeRegen += (int)(player.statLifeMax2 * ResolveRegenFraction * resolve * 2f);
		}

		/// <summary>
		/// Resolve as a 0..1 fraction: total damage taken in the window as a fraction of max life, times
		/// ResolvePerLifeFraction, capped at 1. Same shape as VengeanceTalent.GetCurrentBonus, but it
		/// feeds defense rather than offense. Guarded on statLifeMax2 for the divide.
		/// </summary>
		private float ComputeResolve(Player player)
		{
			if (player.statLifeMax2 <= 0)
				return 0f;

			float fraction = (GetRecentDamage() / player.statLifeMax2) * ResolvePerLifeFraction;
			return fraction > 1f ? 1f : fraction;
		}

		/// <summary>Total post-mitigation damage taken inside the window — what drives Resolve.</summary>
		private float GetRecentDamage()
		{
			float total = 0f;
			for (int i = 0; i < recentHits.Count; i++)
				total += recentHits[i].Damage;
			return total;
		}

		/// <summary>
		/// Strength's Juggernaut-only defense. A DefensePercent-shaped multiply on a Player.DefenseStat,
		/// which tracks adds and multiplies separately — so it is order-independent and compounds with
		/// Ironhide and this talent's own x1.5 rather than summing into them. tML forbids touching
		/// statDefense in PostUpdate or later; this hook is safely before that.
		///
		/// The flat +1 HP/s-per-point regen that used to sit alongside this was removed on 2026-07-20 —
		/// it was the only pinnacle sustain that could not scale with world level, and Resolve's
		/// proportional regen replaces it.
		/// </summary>
		private static void ApplyStrengthDefense(Player player)
		{
			int strength = AttributeManager.Get(player).Get(PlayerAttribute.Strength);
			if (strength <= 0)
				return;

			player.statDefense *= 1f + DefensePercentPerStrength * strength;
		}

		/// <summary>
		/// The potion's regen burst, decaying linearly over its 4 seconds.
		///
		/// UpdateLifeRegen is the only hook where scaling regen means anything — it fires after every
		/// debuff, campfire and heart lantern has already contributed, where PostUpdateMiscEffects
		/// fires before all of them and would multiply an almost-empty number. This is the same wrong
		/// hook that made Vengeance's regen line near-dead code until 2026-07-16, and the same bug
		/// StatApplier's LifeRegenPercent case still has (todo.md §5).
		///
		/// The lifeRegen > 0 guard is load-bearing rather than defensive: lifeRegen goes NEGATIVE under
		/// a DoT, so an unguarded multiply would make every Bleeding/Poisoned/On Fire tick proportionally
		/// MORE lethal — a potion would deepen the DoT that is already Juggernaut's answer key.
		/// </summary>
		public override void UpdateLifeRegen(Player player)
		{
			if (potionRegenTicks <= 0 || player.lifeRegen <= 0)
				return;

			float decay = potionRegenTicks / (float)PotionRegenDurationTicks;
			player.lifeRegen += (int)(player.lifeRegen * PotionRegenBonus * decay);
		}

		private static void ApplyAttackSpeedPenalty(Player player, DamageClass damageClass)
		{
			// GetAttackSpeed throws if the result lands at or below zero, so never let it.
			ref float speed = ref player.GetAttackSpeed(damageClass);
			speed -= AttackSpeedPenalty;
			if (speed < 0.05f)
				speed = 0.05f;
		}

		private class DamageEntry
		{
			public float Damage { get; set; }
			public float TimeRemaining { get; set; }
		}
	}
}
