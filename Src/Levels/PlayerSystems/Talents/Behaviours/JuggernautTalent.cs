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
	/// <b>Its sustain is regeneration and potions, and Strength is what pays for both</b> (added
	/// 2026-07-16). Before that this talent had NO sustain whatsoever — 3.3 HP/s from vanilla potions,
	/// against Stagger's 144 and Vengeance's 111 (.scripts/juggernaut_sustain.py). It was not merely
	/// behind; it was the only pinnacle with no way to recover life at all, and the -50% movement means
	/// it cannot disengage to lean on the natural regen ramp the way the other two can.
	///
	/// The shape is deliberate and it is NOT a rival to leech: every number here is flat, so it fades
	/// as world level scales enemy damage (todo.md §3). That is the same structural flaw that made
	/// Stagger the weakest pinnacle, and it is accepted here rather than solved, because Juggernaut's
	/// late-game answer is the hit cap — the only defense in the mod that is proportional to anything.
	/// Regen is what carries it through the early and middle game, where the model says the hole was.
	///
	/// <b>Vanilla DoTs switch this whole engine off, and that is the point.</b> The Strength regen is
	/// added in PostUpdateMiscEffects, so it lands in lifeRegen BEFORE Player.UpdateLifeRegen's debuff
	/// block runs `if (lifeRegen > 0) lifeRegen = 0` for Poisoned/On Fire/etc — exactly like Stagger's
	/// flat regen and every other regen source. DoTs already bypass the hit cap (they never touch
	/// Player.Hurt), so they were already the wall's answer key; now they shut off its sustain too.
	/// Potions are the counter-play, because a direct heal is not regeneration and ignores all of it.
	/// </summary>
	public class JuggernautTalent : TalentBehaviour
	{
		public override string Id => "juggernaut";
		public override string SlotKey => "pinnacle";
		public override string DisplayName => "Juggernaut";

		public override string Description =>
			"+50% maximum life. 65% more damage. No single hit can take more than half "
			+ "of your maximum life. Each point of Strength also grants +1 life regeneration per second "
			+ "and +1% defense. Healing potions recharge four times faster, heal 50% more, and double "
			+ "your life regeneration for 4 seconds. -15% attack speed, and attack speed cannot be "
			+ "increased. -50% movement speed. You permanently have no mana. Dexterity and Intellect "
			+ "grant you nothing.";

		private const float LifeBonus = 0.50f;

		/// <summary>
		/// Strength's Juggernaut-only lines, in HP/s and defense-% per point.
		///
		/// These are ADDITIVE with Strength's normal +2 max life and +0.5% melee damage — Strength does
		/// strictly more for a Juggernaut than for anyone else. That is a real design cost knowingly
		/// paid: the talent's stated price is that Dexterity and Intellect grant nothing, and the
		/// breadth gate forces ~50 attribute points regardless, so this hands the already-forced
		/// Strength investment a payoff and blunts that price. It was taken anyway because the model
		/// says Juggernaut STILL finishes last on sustain afterwards (46.7 HP/s vs 144 and 111), so
		/// the cost being blunted does not make the pick strong — it makes it viable.
		///
		/// At a realistic 40 Strength: +40 HP/s and +40% defense. Both untested first guesses.
		/// </summary>
		private const float RegenPerStrength = 1.0f;
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
		/// it is worth ~nothing at 0 Strength and ~+40 HP/s at 40, and it is worth exactly zero while a
		/// DoT is holding lifeRegen at or below zero. That is the intended reading — it makes the potion
		/// and the Strength regen multiply rather than merely add, and it does not rescue the DoT
		/// matchup that the direct heal is there to answer.
		/// </summary>
		private const float PotionRegenBonus = 1.00f;
		private const int PotionRegenDurationTicks = 240;

		private int potionRegenTicks;

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
			// against bosses. Mitigation now comes from ClassBaselines.WarriorEndurance, which every
			// warrior has and which is multiplicative.
			//
			// The Strength-driven defense in ApplyStrengthSustain BELOW SURVIVES, and it is now the
			// only thing separating this talent from the other two on defense — which is the right
			// shape: it is bought with an attribute investment the talent already forces, rather than
			// handed over as a flat multiplier.
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
		}

		public override void OnPotionUsed(Player player, Item item)
		{
			potionRegenTicks = PotionRegenDurationTicks;
		}

		public override void OnDeactivate(Player player)
		{
			potionRegenTicks = 0;
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

			ApplyStrengthLines(player);

			// The consumables-only channel, so this cannot reach LifeLeechApplier. Juggernaut has no
			// leech of its own, but an item can roll it, and "potions heal more" should not quietly
			// become "the leech you rolled heals more" — see CLAUDE.md §8 on the two channels.
			CombatEffectStats.Get(player).ConsumableHealingPercent += PotionHealingBonus * 100f;
		}

		/// <summary>
		/// Strength's regen and defense. Added here, in PostUpdateMiscEffects, deliberately:
		///
		/// - The regen is a FLAT ADD, and this hook runs immediately before Player.UpdateLifeRegen
		///   (Player.cs:23190/23191) — so it lands in lifeRegen in time for vanilla's debuff block to
		///   zero it under a DoT, exactly as it does to StatApplier's LifeRegen key and to Stagger's
		///   flat regen. Adding it from the UpdateLifeRegen hook instead would place it AFTER that
		///   block (PlayerLoader.cs:17305) and quietly make Juggernaut the one build whose regen
		///   ignores Poisoned and On Fire. See the class doc: DoTs shutting this off is the design.
		/// - The defense is a DefensePercent-shaped multiply on a Player.DefenseStat, which tracks adds
		///   and multiplies separately — so it is order-independent and compounds with Ironhide and
		///   this talent's own x1.5 rather than summing into them. tML forbids touching statDefense in
		///   PostUpdate or later; this hook is safely before that.
		/// </summary>
		private static void ApplyStrengthLines(Player player)
		{
			int strength = AttributeManager.Get(player).Get(PlayerAttribute.Strength);
			if (strength <= 0)
				return;

			// lifeRegen is in HALF-HP per second, hence the 2. Getting this wrong halves the talent.
			player.lifeRegen += (int)(RegenPerStrength * strength * 2f);

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
	}
}
