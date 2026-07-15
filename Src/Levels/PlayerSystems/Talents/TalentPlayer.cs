using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ProgressionExpanded.Src.Levels.PlayerSystems.Stats;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents
{
	/// <summary>
	/// Owns the player's six talent picks and dispatches hooks to the behaviours behind them.
	///
	/// One ModPlayer for all talents, rather than one per talent. Unpicked talents then cost nothing
	/// per frame, and the full set of hooks the talent system touches is visible in this one file
	/// instead of scattered across twenty.
	///
	/// Picks live in this ModPlayer's own TagCompound, so LoadData is authoritative and runs before
	/// any update tick — no deferred read, no save-wipe window.
	/// </summary>
	public class TalentPlayer : ModPlayer
	{
		private const string PicksKey = "TalentPicks";
		private const string SlotTag = "Slot";
		private const string TalentTag = "Talent";

		/// <summary>slot key -> talent id. Sparse: absent means nothing picked for that slot.</summary>
		private readonly Dictionary<string, string> picks = new Dictionary<string, string>();

		/// <summary>Live behaviour instances, rebuilt only when the SET of picks changes.</summary>
		private readonly List<TalentBehaviour> active = new List<TalentBehaviour>();

		private TalentSuppression suppression = TalentSuppression.None;

		public override void Initialize()
		{
			picks.Clear();
			active.Clear();
			suppression = TalentSuppression.None;
		}

		#region Persistence

		public override void SaveData(TagCompound tag)
		{
			List<TagCompound> entries = new List<TagCompound>();
			foreach (KeyValuePair<string, string> pick in picks)
			{
				if (string.IsNullOrEmpty(pick.Value))
					continue;

				entries.Add(new TagCompound { [SlotTag] = pick.Key, [TalentTag] = pick.Value });
			}
			tag[PicksKey] = entries;
		}

		public override void LoadData(TagCompound tag)
		{
			picks.Clear();

			// Keyed by slot rather than stored positionally: a positional list silently shifts every
			// pick if a slot is ever inserted or reordered.
			IList<TagCompound> entries = tag.GetList<TagCompound>(PicksKey);
			if (entries != null)
			{
				foreach (TagCompound entry in entries)
				{
					string slotKey = entry.GetString(SlotTag);
					string talentId = entry.GetString(TalentTag);

					// Drop anything we no longer recognise instead of throwing. A renamed or removed
					// talent then just frees its slot for a re-pick, rather than bricking the save.
					if (TalentSlots.Get(slotKey) == null || !TalentRegistry.Exists(talentId))
						continue;

					picks[slotKey] = talentId;
				}
			}

			RebuildActive();
		}

		#endregion

		#region Picks

		public string GetPick(string slotKey)
		{
			if (slotKey != null && picks.TryGetValue(slotKey, out string id))
				return id;
			return null;
		}

		public bool Has(string talentId)
		{
			foreach (string id in picks.Values)
			{
				if (id == talentId)
					return true;
			}
			return false;
		}

		public bool IsSlotUnlocked(TalentSlot slot)
		{
			return slot != null && slot.IsUnlocked(Player);
		}

		/// <summary>
		/// Choose a talent for a slot, replacing whatever was there. Talents cost no passive points —
		/// they are gated by boss kills, not by the pool — so there is no economy to touch here.
		/// </summary>
		public bool TryPick(string slotKey, string talentId)
		{
			TalentSlot slot = TalentSlots.Get(slotKey);
			if (slot == null || !IsSlotUnlocked(slot))
				return false;

			if (!TalentRegistry.Exists(talentId))
				return false;

			// A talent may only go in the slot it was declared for.
			TalentBehaviour prototype = TalentRegistry.GetPrototype(talentId);
			if (prototype == null || prototype.SlotKey != slotKey)
				return false;

			if (GetPick(slotKey) == talentId)
				return false;

			picks[slotKey] = talentId;
			RebuildActive();
			return true;
		}

		public bool ClearSlot(string slotKey)
		{
			if (!picks.Remove(slotKey))
				return false;

			RebuildActive();
			return true;
		}

		public void ClearAll()
		{
			if (picks.Count == 0)
				return;

			picks.Clear();
			RebuildActive();
		}

		#endregion

		#region Active set

		/// <summary>
		/// Rebuild the live behaviour instances from the picks. Only called when the set of picks
		/// actually changes — never per frame — because rebuilding throws away behaviour state
		/// (Stagger's in-flight DoT instances, Soul Harvest's stacks).
		/// </summary>
		private void RebuildActive()
		{
			foreach (TalentBehaviour behaviour in active)
				behaviour.OnDeactivate(Player);

			active.Clear();
			suppression = TalentSuppression.None;

			// Iterate slots rather than the dictionary so the active order is the slot order, which
			// makes hook dispatch deterministic instead of dependent on dictionary internals.
			foreach (TalentSlot slot in TalentSlots.All)
			{
				string id = GetPick(slot.Key);
				if (id == null)
					continue;

				TalentBehaviour behaviour = TalentRegistry.Create(id);
				if (behaviour == null)
					continue;

				active.Add(behaviour);
				suppression |= behaviour.Suppresses;
				behaviour.OnActivate(Player);
			}
		}

		/// <summary>
		/// Whether a picked talent switches off the given effect. Derived from picks, so the answer
		/// is the same at every point in the frame — callers can ask whenever suits them.
		/// </summary>
		public bool Suppresses(TalentSuppression flag)
		{
			return (suppression & flag) != 0;
		}

		public IReadOnlyList<TalentBehaviour> Active => active;

		#endregion

		#region Hook dispatch

		public override void ResetEffects()
		{
			for (int i = 0; i < active.Count; i++)
				active[i].ResetEffects(Player);
		}

		public override void PostUpdate()
		{
			for (int i = 0; i < active.Count; i++)
				active[i].PostUpdate(Player);
		}

		public override void PostUpdateMiscEffects()
		{
			for (int i = 0; i < active.Count; i++)
			{
				TalentBehaviour behaviour = active[i];

				// Declarative bonuses go through the same applier the mastery tree uses, so a stat
				// key means the same thing whether a mastery or a talent grants it.
				if (behaviour.FlatBonuses != null)
				{
					foreach (KeyValuePair<string, float> bonus in behaviour.FlatBonuses)
						StatApplier.ApplyFlat(Player, bonus.Key, bonus.Value);
				}

				if (behaviour.PercentBonuses != null)
				{
					foreach (KeyValuePair<string, float> bonus in behaviour.PercentBonuses)
						StatApplier.ApplyPercent(Player, bonus.Key, bonus.Value);
				}

				behaviour.PostUpdateMiscEffects(Player);
			}
		}

		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			for (int i = 0; i < active.Count; i++)
				active[i].ModifyHurt(Player, ref modifiers);
		}

		public override void PostHurt(Player.HurtInfo info)
		{
			for (int i = 0; i < active.Count; i++)
				active[i].PostHurt(Player, info);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < active.Count; i++)
			{
				active[i].OnHitNPC(Player, target, hit, damageDone);

				// The killing blow. Vanilla has no player-side "you killed it" hook, and OnKillNPC
				// on GlobalNPC does not tell us which player did it, so this is the seam.
				if (target.life <= 0)
					active[i].OnKillNPC(Player, target);
			}
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			for (int i = 0; i < active.Count; i++)
				active[i].ModifyHitNPC(Player, target, ref modifiers);
		}

		public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
		{
			health = StatModifier.Default;
			mana = StatModifier.Default;

			for (int i = 0; i < active.Count; i++)
			{
				TalentBehaviour behaviour = active[i];

				// Max life/mana are picked out of the declarative dictionaries here rather than in
				// StatApplier, because StatModifier is the only channel where flat and percentage
				// max-life compose correctly. Base sits inside the multipliers so that a percentage
				// bonus scales it; += is the additive-percent operator.
				if (behaviour.FlatBonuses != null)
				{
					if (behaviour.FlatBonuses.TryGetValue("MaxHealth", out float flatLife))
						health.Base += flatLife;
					if (behaviour.FlatBonuses.TryGetValue("MaxMana", out float flatMana))
						mana.Base += flatMana;
				}

				if (behaviour.PercentBonuses != null)
				{
					if (behaviour.PercentBonuses.TryGetValue("MaxLifePercent", out float pctLife))
						health += pctLife;
					if (behaviour.PercentBonuses.TryGetValue("MaxManaPercent", out float pctMana))
						mana += pctMana;

					// "More" life, as distinct from "increased" above. StatModifier resolves as
					// (statMax + Base) * Additive * Multiplicative + Flat, so += lands in Additive
					// and sums with every other % life source, while *= lands in Multiplicative and
					// compounds with them. tML's CombineWith multiplies Multiplicative across
					// ModPlayers, so this still compounds against AttributeManager's and
					// PassiveTreeManager's contributions rather than being flattened into them.
					if (behaviour.PercentBonuses.TryGetValue("MaxLifeMore", out float moreLife))
						health *= 1f + moreLife;
				}

				behaviour.ModifyMaxStats(Player, ref health, ref mana);
			}
		}

		public override void OnRespawn()
		{
			for (int i = 0; i < active.Count; i++)
				active[i].OnRespawn(Player);
		}

		#endregion

		public static TalentPlayer Get(Player player)
		{
			return player.GetModPlayer<TalentPlayer>();
		}
	}
}
