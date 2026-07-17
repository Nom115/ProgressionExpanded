using Terraria;
using ProgressionExpanded.Src.NPCs.Enemy.Elemental;

namespace ProgressionExpanded.Src.NPCs.Enemy.Modifiers.Core
{
	/// <summary>
	/// Base for the four elemental wards. Each grants a large resistance to exactly one element,
	/// which <see cref="ElementalResistance"/> reads through <see cref="IElementalResistanceModifier"/>.
	///
	/// <b>These classes have no fields, and that is a requirement rather than an accident.</b>
	/// EnemyModifierSystem.Clone allocates a fresh List but copies the IModifier references into it,
	/// so two cloned enemies share one modifier instance — the live bug behind VileSpitModifier's
	/// shootTimer cross-talk. A single "Warded" affix that rolled its element into a field would
	/// reproduce that bug exactly: split a warded worm and both halves would suddenly ward the same
	/// element, or worse, change which one mid-fight. Aliasing a stateless object is harmless, so one
	/// class per element sidesteps the whole thing.
	///
	/// ⚠️ Do not add a rolled field to any of these without first fixing EnemyModifierSystem.Clone.
	///
	/// Per-element classes buy three more things for free. The rolled-ness comes from
	/// ModifierPool.RollModifiers, which already picks by weight and removes what it picked, so
	/// duplicates are impossible. Rarity's influence returns legitimately through MaxModifiers — a
	/// Mythic rolls five affixes and is simply likelier to be warded — with none of the double-dipping
	/// that giving rarity its own resistance would cause. And the prefix names the element, so
	/// "Flamewarded Zombie" tells the player to switch to Rend <i>before</i> the Phase 4 hover panel
	/// exists to tell them properly.
	///
	/// ⚠️ <b>That last one only half works today, and the half it misses is bosses.</b>
	/// EnemyModifierSystem.GenerateDisplayName renders at most the FIRST TWO modifiers' prefixes, so a
	/// ward rolled into slot 3+ is applied but never named. Trash is fine — Uncommon and Rare roll 1-2
	/// modifiers, so a ward is always shown. A previously-defeated boss always rolls 2-5, so roughly
	/// 43% of warded bosses hide it: the fire DoT is quietly cut 40% with nothing on screen to say why.
	/// That is exactly the encounter where the ward matters most (~50% of repeat bosses carry one).
	/// Fixing it means either naming every modifier (long names) or floating wards to the front of the
	/// prefix (a special case). Logged in todo.md §6 rather than decided here.
	/// </summary>
	public abstract class ElementalWardModifier : IModifier, IElementalResistanceModifier
	{
		/// <summary>The one element this ward protects against.</summary>
		protected abstract DamageElement WardedElement { get; }

		public abstract string GetPrefix();

		public float GetResistance(DamageElement element)
		{
			return element == WardedElement ? ElementalResistance.WardResistance : 0f;
		}

		// Nothing to apply at spawn: resistance is pulled from this object at hit time rather than
		// written into the NPC, so there is no stat to touch and nothing to keep in sync.
		public void Apply(Terraria.NPC npc) { }

		public void OnHitByPlayer(Terraria.NPC npc, Player player) { }

		public void Update(Terraria.NPC npc) { }

		public void UpdateLifeRegen(Terraria.NPC npc, ref int damage) { }

		public void OnKill(Terraria.NPC npc) { }

		public float GetXPBonus() => 0.0f;

		/// <summary>
		/// Rare band, matching ExplosiveModifier. A ward is a large single-element swing, so it wants
		/// to be a memorable encounter rather than background noise.
		/// </summary>
		public int GetSpawnWeight() => 50; // Rare
	}

	/// <summary>Flamewarded — shrugs off Fire damage-over-time.</summary>
	public class FlamewardedModifier : ElementalWardModifier
	{
		protected override DamageElement WardedElement => DamageElement.Fire;

		public override string GetPrefix() => "Flamewarded";
	}

	/// <summary>Frostwarded — shrugs off Cold damage-over-time.</summary>
	public class FrostwardedModifier : ElementalWardModifier
	{
		protected override DamageElement WardedElement => DamageElement.Cold;

		public override string GetPrefix() => "Frostwarded";
	}

	/// <summary>Stormwarded — shrugs off Lightning damage-over-time.</summary>
	public class StormwardedModifier : ElementalWardModifier
	{
		protected override DamageElement WardedElement => DamageElement.Lightning;

		public override string GetPrefix() => "Stormwarded";
	}

	/// <summary>Venomwarded — shrugs off Poison damage-over-time.</summary>
	public class VenomwardedModifier : ElementalWardModifier
	{
		protected override DamageElement WardedElement => DamageElement.Poison;

		public override string GetPrefix() => "Venomwarded";
	}

	// There is deliberately no Bleed ward. Bleed ignores elemental resistance and faces armour
	// instead, so ToughModifier — which multiplies defense by 1.5 — already is the bleed ward, for
	// free and with no new code.
}
