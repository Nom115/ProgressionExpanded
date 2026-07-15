using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.NotablePassives
{
	/// <summary>
	/// Base for notable passives that apply a vanilla debuff to enemies you hit — the player-side
	/// mirror of the enemy affix modifiers (Bleeding, Igniting, Venomous, Chilling, Shocking).
	/// Each subclass names its tree node, the buff to apply, and how long it lasts per tier.
	/// The effect is gated on the node's allocated tier (like Stagger), read fresh each frame.
	/// </summary>
	public abstract class OnHitDebuffNotable : ModPlayer
	{
		protected const string TreeId = "warrior_tree";

		/// <summary>Node id in warrior_tree.json that gates this notable.</summary>
		protected abstract string NodeId { get; }

		/// <summary>Vanilla BuffID to apply to hit enemies.</summary>
		protected abstract int BuffType { get; }

		/// <summary>Debuff duration in ticks at tier 1.</summary>
		protected abstract int BaseTicks { get; }

		/// <summary>Extra debuff duration in ticks per tier beyond the first.</summary>
		protected abstract int TicksPerTier { get; }

		protected int tier;

		public override void ResetEffects()
		{
			tier = Player.GetModPlayer<PassiveTreeManager>().GetNodeTier(TreeId, NodeId);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (tier <= 0 || target.friendly)
				return;

			// Ailment Effect from gear/passives (CombatEffectStats) extends the debuff — the generic
			// hook that lets any class's on-hit ailment scale past its tier ceiling.
			int ticks = BaseTicks + TicksPerTier * (tier - 1);
			float ailmentMul = 1f + Player.GetModPlayer<CombatEffectStats>().AilmentPercent / 100f;
			target.AddBuff(BuffType, (int)(ticks * ailmentMul));
		}
	}

	/// <summary>Rend — your hits inflict Bleeding (mirrors the Bleeding / Draining affixes).</summary>
	public class Rend : OnHitDebuffNotable
	{
		protected override string NodeId => "rend_notable";
		protected override int BuffType => BuffID.Bleeding;
		protected override int BaseTicks => 180;   // 3s
		protected override int TicksPerTier => 60; // +1s per tier
	}

	/// <summary>Immolation — your hits set enemies On Fire (mirrors the Igniting affix).</summary>
	public class Immolation : OnHitDebuffNotable
	{
		protected override string NodeId => "immolation_notable";
		protected override int BuffType => BuffID.OnFire;
		protected override int BaseTicks => 180;
		protected override int TicksPerTier => 60;
	}

	/// <summary>Venom — your hits inflict Venom, a potent poison (mirrors the Venomous affix).</summary>
	public class Venom : OnHitDebuffNotable
	{
		protected override string NodeId => "venom_notable";
		protected override int BuffType => BuffID.Venom;
		protected override int BaseTicks => 120;   // 2s (Venom ticks hard)
		protected override int TicksPerTier => 45;
	}

	/// <summary>Frostbite — your hits inflict Frostburn (mirrors the Chilling affix).</summary>
	public class Frostbite : OnHitDebuffNotable
	{
		protected override string NodeId => "frostbite_notable";
		protected override int BuffType => BuffID.Frostburn;
		protected override int BaseTicks => 180;
		protected override int TicksPerTier => 60;
	}

	/// <summary>Shock — your hits inflict Electrified (mirrors the Shocking affix).</summary>
	public class Shock : OnHitDebuffNotable
	{
		protected override string NodeId => "shock_notable";
		protected override int BuffType => BuffID.Electrified;
		protected override int BaseTicks => 120;
		protected override int TicksPerTier => 45;
	}
}
