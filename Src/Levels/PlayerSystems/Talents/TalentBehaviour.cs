using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents
{
	/// <summary>
	/// Effects a talent can switch off elsewhere in the mod.
	///
	/// This is derived from the player's PICKS, not accumulated per frame, so it is stable at every
	/// point in the frame and has no dependency on ModPlayer hook ordering. Contributors ask
	/// TalentPlayer.Suppresses(...) before applying themselves rather than a talent trying to reach
	/// out and cancel them after the fact.
	///
	/// The limit of that design: it can only express UNCONDITIONAL suppression. A talent that
	/// suppressed something situationally ("no mana while below half life") would need a per-frame
	/// flag, and ordering would come back with it. Nothing needs that today.
	/// </summary>
	[Flags]
	public enum TalentSuppression
	{
		None = 0,
		ManaFromIntellect = 1 << 0,
		AttackSpeedIncreases = 1 << 1,
	}

	/// <summary>
	/// Base for every talent effect.
	///
	/// Behaviours are plain classes, not ModPlayers — TalentPlayer owns one instance per picked
	/// talent per player and forwards the hooks below. Two reasons that matters: instances hold
	/// per-player state (Stagger's in-flight DoT list), and a talent you did not pick costs exactly
	/// nothing per frame. The old notables were each their own ModPlayer re-reading their node tier
	/// every single frame whether or not the player had them.
	///
	/// Id is declared HERE rather than in a data file on purpose: an id that has no behaviour, or a
	/// behaviour with no id, is then unrepresentable rather than merely detectable. The old notables
	/// bound themselves to a node with a hardcoded string that nothing validated, so a rename
	/// silently disabled the effect with no error anywhere.
	/// </summary>
	public abstract class TalentBehaviour
	{
		/// <summary>Stable save key. Renaming this orphans existing picks — treat it as permanent.</summary>
		public abstract string Id { get; }

		/// <summary>Which of the six slots this talent is a choice for. Must match a TalentSlots key.</summary>
		public abstract string SlotKey { get; }

		public abstract string DisplayName { get; }
		public abstract string Description { get; }

		/// <summary>Declarative stat bonuses, applied through the same path the mastery tree uses.</summary>
		public virtual IReadOnlyDictionary<string, float> FlatBonuses => null;
		public virtual IReadOnlyDictionary<string, float> PercentBonuses => null;

		public virtual TalentSuppression Suppresses => TalentSuppression.None;

		/// <summary>Called when the talent is picked or loaded. Set up state here.</summary>
		public virtual void OnActivate(Player player) { }

		/// <summary>Called when the talent is cleared. Tear down state here — this is where an effect
		/// with something in flight (a DoT, a buff, a stack counter) must clean up after itself.</summary>
		public virtual void OnDeactivate(Player player) { }

		public virtual void ResetEffects(Player player) { }
		public virtual void PostUpdate(Player player) { }
		public virtual void PostUpdateMiscEffects(Player player) { }
		public virtual void ModifyHurt(Player player, ref Player.HurtModifiers modifiers) { }
		public virtual void PostHurt(Player player, Player.HurtInfo info) { }
		public virtual void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) { }
		public virtual void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) { }
		public virtual void ModifyMaxStats(Player player, ref StatModifier health, ref StatModifier mana) { }
		public virtual void OnRespawn(Player player) { }
		public virtual void OnKillNPC(Player player, NPC target) { }
	}
}
