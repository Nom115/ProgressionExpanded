using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.NPCs.Enemy.Modifiers;
using ProgressionExpanded.Utils;

namespace ProgressionExpanded.Src.NPCs.Enemy.Elemental
{
	/// <summary>
	/// A modifier that resists one or more elements. Implemented as a capability interface rather
	/// than a method on <see cref="IModifier"/> so the sixteen existing modifiers need no edit and
	/// future affixes opt in.
	///
	/// It lives here, next to the arithmetic that consumes it, because this file is the single place
	/// a resistance number is decided.
	/// </summary>
	public interface IElementalResistanceModifier
	{
		/// <summary>Resistance this modifier grants to <paramref name="element"/>, as a fraction.</summary>
		float GetResistance(DamageElement element);
	}

	/// <summary>
	/// How much an enemy resists each element. This is the single place a resistance number is
	/// decided, so every consumer agrees on both the sources and the clamp.
	///
	/// Resistance is a fraction: 0 is neutral, positive reduces the damage, and <b>negative
	/// amplifies it</b> — an enemy at -50% takes 150% of the element's damage. Callers multiply by
	/// <c>1 - Get(...)</c>.
	///
	/// <b>Three sources, and resistance is a rolled property rather than a species trait.</b> Every
	/// enemy rolls its own per-element resistance; a ward affix can add a large amount to one
	/// element; and Bleed ignores both and faces the enemy's armour instead. The design is
	/// element-symmetric by construction and <b>type-agnostic</b>, so modded enemies — Calamity's and
	/// Fargo's, which are most of what actually spawns — are differentiated exactly like vanilla's.
	///
	/// <b>Pull, never cache.</b> Get is only ever reached from ElementalDotApplier.OnHitNPC, which is
	/// necessarily after NewNPC -> SetDefaults -> ScaleStats -> OnSpawn (NPC.NewNPC:91956 vs :91980).
	/// Every source is stable by then, so there is no ordering problem provided nothing is cached.
	/// Caching at spawn would make us depend on GlobalNPC hook order between EnemyModifierSystem's
	/// OnSpawn and ours, which is not ours to rely on. Every source also defaults to zero resistance
	/// (empty modifier list, unrolled seed), so a missed init can only ever under-resist.
	///
	/// <b>Why npc.buffImmune is NOT a source, and please do not add it back.</b> It looks like the
	/// obvious answer — vanilla appears to curate which enemies shrug off which element — and it is a
	/// trap. Verified against the decompiled assembly:
	/// <list type="bullet">
	/// <item>It is not element curation. buffImmune is a bool, so there is no graded data to inherit
	/// and the percentage would be invented anyway. The set encodes "should this icon appear / would
	/// this debuff break the AI": Mimic, EnchantedSword, CursedHammer and PossessedArmor are
	/// OnFire-immune because they are <i>inanimate</i>; Ghost is immune to Fire, Cold and Venom
	/// because it is <i>incorporeal</i>; Confused sits on 410 types because it breaks pathing.</item>
	/// <item>It is wildly asymmetric. Explicitly-immune type counts are Fire 45, Cold 25, Venom 2 and
	/// <b>Electrified 0</b> — not one vanilla enemy specifically resists Lightning. Fire and Cold
	/// would be strictly worse masteries for identical point cost, by accident.</item>
	/// <item>Poison's carrier is misaligned. Vanilla's poison curation is on Poisoned (id 20, 171
	/// types), not Venom (id 70, 2 types), and BuffID.Sets.GrantImmunityWith[20] = {70} is one-way.
	/// Probing Poisoned instead would fix that but make the asymmetry worse still.</item>
	/// <item>"Modded NPCs work for free" means free <i>zero</i>: an NPC with no DebuffImmunitySets
	/// entry gets an all-null row, so buffImmune is all-false and resistance is 0 across the board.
	/// On the actual modpack that is no differentiation at all.</item>
	/// <item>NPCID.Sets.ImmuneToRegularBuffs implies immunity to all four carriers (none is a tag
	/// buff). That set is The Destroyer, the Lunatic Cultist and all four Celestial Pillars, among
	/// others — the whole mech-to-Lunar spine — while Moon Lord, Plantera, Duke and the Twins are
	/// immune to none of the four. Reading it would switch a sixty-point elemental investment off for
	/// that entire stretch and back on for Moon Lord: a cliff, not a slope.</item>
	/// </list>
	/// A hardcoded id table is not the fallback either — it has the same free-zero problem for modded
	/// enemies. The random roll is the answer precisely because it works for everything.
	///
	/// <b>Rarity is deliberately not a source.</b> It already multiplies health by up to 3 and defense
	/// by up to 2 (EnemyModifierSystem.ApplyRarityStats), and that defense bump already raises Bleed's
	/// resistance below. Adding elemental resistance on top inverts what the masteries are for: under
	/// DoT, time-to-kill scales as 3/(1-r), while under direct damage it scales as 3 — so rarity
	/// resistance would make the elemental investment fall behind on exactly the enemies it should
	/// shine against. Rarity still buys resistance legitimately, and legibly, through MaxModifiers: a
	/// Mythic rolls five affixes and is therefore far likelier to be warded, which you can read in its
	/// name.
	/// </summary>
	public static class ElementalResistance
	{
		/// <summary>
		/// The most an enemy can resist. Deliberately below 1.0: nothing is ever immune, so an
		/// elemental build always does *something* everywhere rather than reading 0 on a bad
		/// matchup. This matters more than usual here because elemental DoT is added on top of the
		/// hit rather than converted out of it — a hard immunity would delete the investment
		/// outright instead of merely blunting it.
		/// </summary>
		public const float MaxResistance = 0.90f;

		/// <summary>
		/// The most an enemy can be amplified by. At -1.0 the element deals double damage; the
		/// floor stops a stack of negative-resistance sources compounding into a one-shot.
		/// </summary>
		public const float MinResistance = -1.00f;

		/// <summary>
		/// Centre of the per-element roll — i.e. the average elemental resistance in the game.
		///
		/// This is Phase 2's headline dial. The roll is the only source that touches every enemy, so
		/// this number alone decides how much the elemental masteries are blunted overall: five
		/// masteries at rank 4 add ~160% of direct DPS unmitigated (ElementalMastery.ConversionPerTier
		/// x ElementalDotApplier.BaseDurationSeconds, per element), and this brings that to ~134%.
		///
		/// <b>A mean of zero was the considered alternative and is a real fork, not an oversight.</b>
		/// At zero, Phase 2 changes the tuning identity by nothing — it merely turns a flat 32% per
		/// element into a distribution centred on 32% — and total output stays ConversionPerTier's job
		/// to tune, which is arguably the honest dial for it. A positive mean was chosen so that Phase
		/// 2 does the mitigating it exists to do. If elemental output ends up over-nerfed, move this
		/// toward zero and cut ConversionPerTier instead, rather than fighting the two against
		/// each other. Untested in play.
		/// </summary>
		public const float RollMean = 0.15f;

		/// <summary>
		/// Half-width of the per-element roll, so it spans [RollMean - this, RollMean + this] =
		/// [-0.10, +0.40].
		///
		/// Deliberately moderate, and the reason is a UI gap rather than a balance one: <b>the roll is
		/// invisible until Phase 4's hover panel exists.</b> A ward announces itself in the enemy's
		/// name; a +0.40 fire roll does not, so until the panel lands it reads as Immolation randomly
		/// underperforming with no counterplay. Widen this when Phase 4 ships, not before.
		///
		/// The negative tail is load-bearing in its own small way: it is the only thing in Phase 2 that
		/// ever produces a negative resistance, so it is what keeps the "negative amplifies" half of
		/// this class's contract from being dead letter.
		/// </summary>
		public const float RollHalfWidth = 0.25f;

		/// <summary>
		/// Resistance one ward affix grants to its single element.
		///
		/// Large on purpose. Because a ward covers one element rather than all four, it can afford to
		/// be a real "switch element" moment: a warded enemy that also rolled badly reaches 0.80, at
		/// which point Rend does roughly four times what Immolation does — the pillar working exactly
		/// as intended, and legible from the enemy's name before you swing. Untested in play.
		/// </summary>
		public const float WardResistance = 0.40f;

		/// <summary>
		/// Defense at which Bleed is resisted 50%. The curve is <c>def / (|def| + this)</c>.
		///
		/// Verified against the real distribution (581 vanilla types parsed from SetDefaults): defense
		/// is median 15, p75 28, p90 38, max 9999, and only 27 types reach 50 while 90 sit at zero. So
		/// 50 puts median Bleed resistance at 23% and p90 at 43%. The mod inflates defense by at most
		/// x3.36 (rarity <=x2.0 x Tough x1.5 x level offset x1.12) and — unlike health and damage —
		/// <b>does not scale it with world level</b>, so this curve does not drift as the game goes on.
		/// Median-defense elites do reach ~50% though, which is the number to watch.
		///
		/// <b>Bleed pays for armour twice, and that is the design.</b> NPC.HitModifiers.GetDamage
		/// already subtracts <c>max(defense - armorPen, 0) x DefenseEffectiveness</c> (0.5 for NPCs)
		/// before we ever see damageDone, so armour has reduced the hit our DoT is computed from. That
		/// first charge is universal — every element pays it. This resistance is an additional,
		/// proportional charge that only Bleed pays, which is precisely what makes Bleed "physical,
		/// limited by the enemy's armour instead". Against a median enemy it lands Bleed at roughly 90%
		/// of an average element's output, bought with zero variance and total immunity to wards.
		///
		/// <b>150 was argued for and rejected on the numbers.</b> The case was that at 50 Bleed is only
		/// 62% of an element and therefore a trap — but that figure assumes a mean-zero roll and a
		/// p75-defense enemy. Against the roll mean actually shipped and a median enemy it is ~90%.
		/// If Rend under-performs in play-test, this is the first constant to move.
		/// </summary>
		public const float BleedArmourHalfPoint = 50f;

		/// <summary>
		/// This enemy's resistance to <paramref name="element"/>, after
		/// <paramref name="penetration"/> and after clamping.
		///
		/// Penetration is subtracted from the total before the clamp, so it can push a resistant
		/// enemy down toward neutral and past it — that is the whole point of the stat. It is
		/// applied here rather than by the caller so that the clamp always gets the last word.
		///
		/// ⚠️ <b>Phase 3 must not feed the player's ArmorPenetration into penetration.</b> Armour pen
		/// already flows into damageDone through NPC.HitModifiers.ArmorPenetration, so adding it here
		/// would count it twice — the same mistake Vengeance's leech made when it contributed to
		/// HealingPercent on top of an already-ramped damageDone, and which read as a x2 for as long as
		/// nobody multiplied it out. Penetration must be a new, separate elemental-penetration stat.
		/// </summary>
		public static float Get(NPC npc, DamageElement element, float penetration)
		{
			// A boss you have never killed is left completely untouched by the mod, and that includes
			// us. EnemyModifierSystem gives it no rarity and no affixes, NPCLevelManager skips its
			// health, its damage and its level-difference multiplier, and HostileProjectileScaling
			// leaves its projectiles alone — five call sites, one rule. Rolling a mean +15% resistance
			// here would make us the only system that quietly breaks it, and it would do so on the one
			// encounter the player has never seen before.
			//
			// Above the Bleed branch on purpose: armour resistance is ours too, not vanilla's.
			if (BossProgressionTracker.IsPinnacleEncounter(npc))
				return Clamp(-penetration);

			// Bleed is physical: armour and nothing else. No roll, no ward, no type data.
			//
			// That predictability IS what it is paid for. Bleed is the one element whose number you
			// can work out by looking at the enemy, which is what makes it the reliable answer when an
			// elemental matchup goes bad — and it is why the branch sits above the roll rather than
			// adding to it. Returning early also means Bleed never touches the seed, so a pure-Rend
			// build never allocates one.
			if (element == DamageElement.Bleed)
				return Clamp(BleedResistance(npc) - penetration);

			float resistance = RandomRoll(npc, element) + WardResistanceFor(npc, element);

			return Clamp(resistance - penetration);
		}

		/// <summary>
		/// This enemy's innate rolled resistance to one element.
		///
		/// <b>Triangular, not uniform.</b> A uniform roll puts half of every (enemy, element) pair a
		/// long way off centre, which is just a noisy tax. Triangular clusters near the mean, so most
		/// enemies are unremarkable and a genuinely resistant one is an event worth reacting to. It
		/// costs one extra hash.
		///
		/// <b>Per-element and independent.</b> The point is matchups — this one resists fire and is
		/// weak to cold. A single scalar per enemy would be damage variance with no decision in it.
		/// </summary>
		private static float RandomRoll(NPC npc, DamageElement element)
		{
			int seed = npc.GetGlobalNPC<ElementalDotNPC>().ResistanceSeed;

			// Two independent uniforms summed give a triangular distribution on [-1, 1] once centred.
			float u1 = Hash01(seed, (int)element * 2);
			float u2 = Hash01(seed, (int)element * 2 + 1);

			return RollMean + (u1 + u2 - 1f) * RollHalfWidth;
		}

		/// <summary>
		/// Resistance contributed by this enemy's rolled affixes.
		///
		/// Reads the live modifier list rather than anything cached — EnemyModifierSystem.GetModifiers
		/// is already public and the list is fixed at spawn, long before any hit reaches us.
		/// </summary>
		private static float WardResistanceFor(NPC npc, DamageElement element)
		{
			var modifiers = npc.GetGlobalNPC<EnemyModifierSystem>().GetModifiers();
			if (modifiers == null)
				return 0f;

			float total = 0f;
			for (int i = 0; i < modifiers.Count; i++)
			{
				if (modifiers[i] is IElementalResistanceModifier resistant)
					total += resistant.GetResistance(element);
			}

			return total;
		}

		/// <summary>
		/// Bleed's physical resistance, derived from armour.
		///
		/// <b>The absolute value is not decoration — it removes a pole and a sign flip.</b> The obvious
		/// form, <c>def / (def + K)</c>, is undefined at <c>def == -K</c> and returns a *positive*
		/// resistance below it: at def -60 with K 50 it yields +6.0, which clamps to +0.90 and makes
		/// the squishiest enemy in the game nearly bleed-immune. Negative NPC defense is real, not
		/// hypothetical — vanilla routes it to FlatBonusDamage (NPC.cs:92060) and Eye of Cthulhu's
		/// aiStyle 4 sets defense to -15 and then -30 in its Expert second phase. Vanilla stops at -30,
		/// but the mod's own rarity and Tough multipliers would carry a modded negative past -50.
		///
		/// <c>def / (|def| + K)</c> is identical for every non-negative defense, monotone over all
		/// reals, has no pole, and asymptotes to +/-1. EoC at -30 comes out at -0.20, so its exposed
		/// phase amplifies Bleed by 20% — which is exactly what a negative-armour phase should do.
		///
		/// Note there is no min(0.9, ...) here despite the original spec calling for one: Clamp already
		/// subsumes it, and writing a bound twice is how the two drift apart.
		///
		/// Reads npc.defense live, not npc.defDefense. defDefense is snapshotted at the end of
		/// SetDefaults (NPC.cs:12887) — before the mod's OnSpawn multipliers — so it does not contain
		/// them, while npc.defense does and also tracks AI-driven changes as they happen.
		/// </summary>
		private static float BleedResistance(NPC npc)
		{
			float defense = npc.defense;

			return defense / (System.Math.Abs(defense) + BleedArmourHalfPoint);
		}

		/// <summary>
		/// A stable, uniformly-distributed float in [0, 1) from a seed and a salt.
		///
		/// Deterministic by construction, which is what lets the roll be stored as one int and
		/// recomputed per element on every hit rather than kept in an array. This is the lowbias32
		/// integer hash; any decent avalanche would do, but a plain multiply-and-mask would visibly
		/// correlate the five elements with each other.
		/// </summary>
		private static float Hash01(int seed, int salt)
		{
			unchecked
			{
				uint h = (uint)seed ^ ((uint)salt * 0x9E3779B9u);

				h ^= h >> 16;
				h *= 0x7FEB352Du;
				h ^= h >> 15;
				h *= 0x846CA68Bu;
				h ^= h >> 16;

				// 24 bits is plenty of resolution for a resistance percentage and keeps the division
				// exact in float.
				return (h & 0xFFFFFFu) / (float)0x1000000;
			}
		}

		/// <summary>Clamp a raw resistance total into the legal range.</summary>
		public static float Clamp(float resistance)
		{
			if (resistance > MaxResistance)
				return MaxResistance;
			if (resistance < MinResistance)
				return MinResistance;
			return resistance;
		}
	}
}
