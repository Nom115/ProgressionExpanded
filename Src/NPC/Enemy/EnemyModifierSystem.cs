using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using ProgressionExpanded.Src.NPCs.Enemy.Modifiers;
using ProgressionExpanded.Utils;

namespace ProgressionExpanded.Src.NPCs.Enemy
{
	/// <summary>
	/// Manages enemy modifiers and rarity for each NPC instance
	/// </summary>
	public class EnemyModifierSystem : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		// Per-NPC instance data
		private EnemyRarity rarity = EnemyRarity.Common;
		private List<IModifier> modifiers = new List<IModifier>();
		private bool modifiersInitialized = false;
		private string displayName = "";

		#region Initialization

		/// <summary>
		/// Per-NPC init runs from PostAI, NOT OnSpawn. Worm-boss segment linkage
		/// (<see cref="NPC.realLife"/>) is stamped by the head's AI and is not yet available at
		/// OnSpawn. PostAI runs after vanilla AI, and the head (lower index) initialises before the
		/// segments it spawned, so a segment can copy the head's roll the first time it ticks. The
		/// one-frame delay for ordinary enemies is immaterial (nothing reads rarity/modifiers before
		/// the enemy is first drawn or hit).
		/// </summary>
		public override void PostAI(Terraria.NPC npc)
		{
			if (modifiersInitialized) return;

			// Only apply to hostile NPCs (matches the old OnSpawn filter).
			if (npc.friendly || npc.townNPC || npc.lifeMax <= 5) return;

			InitializeModifiers(npc);
		}

		private void InitializeModifiers(Terraria.NPC npc)
		{
			if (modifiersInitialized) return;

			// Pinnacle first encounter (works for vanilla AND modded bosses — and, because boss identity
			// is now worm-aware, for every segment of a worm boss): leave completely untouched, no
			// rarity stat changes, no affix modifiers.
			if (BossProgressionTracker.IsPinnacleEncounter(npc))
			{
				rarity = EnemyRarity.Common;
				modifiers = new List<IModifier>();
				GenerateDisplayName(npc, EnemyRarityConfig.GetRarityInfo(rarity));
				modifiersInitialized = true;
				return;
			}

			// Worm-boss segment: copy the head's roll so the whole body is uniform (one rarity + one
			// modifier set across every piece) instead of each segment rolling independently. Gated on
			// IsTrackedBoss so only BOSS worms harmonise; non-boss worms (desert worms) roll per-segment
			// as before.
			if (WormBossHelper.IsSegment(npc) && BossProgressionTracker.IsTrackedBoss(npc))
			{
				CopyFromHead(npc);
				return;
			}

			// Roll rarity (worm-boss head or standalone enemy).
			rarity = EnemyRarityConfig.RollRarity();
			var rarityInfo = EnemyRarityConfig.GetRarityInfo(rarity);

			// Apply rarity stat multipliers
			ApplyRarityStats(npc, rarityInfo);

			// Roll modifiers.
			if (BossProgressionTracker.IsTrackedBoss(npc))
			{
				// Bosses (including worm-boss heads): a CURATED set — at most one offensive + one
				// defensive affix, and never an HP-inflating one. This keeps a repeat boss varied without
				// the old 2–5-affix HP blow-up; its bulk comes from the boss curve instead. Worm segments
				// copy this via CopyFromHead.
				modifiers = ModifierPool.RollBossModifiers(npc);
			}
			else
			{
				// Normal enemies — rarity-based modifier count (0 for Common).
				int modifierCount = rarityInfo.MaxModifiers > 0 ? Main.rand.Next(1, rarityInfo.MaxModifiers + 1) : 0;
				modifiers = modifierCount > 0 ? ModifierPool.RollModifiers(modifierCount, npc) : new List<IModifier>();
			}

			foreach (var modifier in modifiers)
			{
				modifier.Apply(npc);
			}

			// Generate display name
			GenerateDisplayName(npc, rarityInfo);

			modifiersInitialized = true;
		}

		/// <summary>
		/// Copy a worm-boss head's rolled rarity + modifiers onto this segment so the whole body is
		/// uniform. Leaves this segment uninitialised (retries next PostAI tick) if the head has not
		/// rolled yet — though given NPC update order the head always initialises first.
		/// </summary>
		private void CopyFromHead(Terraria.NPC npc)
		{
			NPC head = WormBossHelper.ResolveHead(npc);
			if (head == null) return; // head not resolvable yet — retry next tick

			var headSystem = head.GetGlobalNPC<EnemyModifierSystem>();
			if (!headSystem.modifiersInitialized)
				headSystem.InitializeModifiers(head); // ensure the head has rolled first (order-independent)
			if (!headSystem.modifiersInitialized) return; // head still couldn't roll — retry next tick

			rarity = headSystem.rarity;
			var rarityInfo = EnemyRarityConfig.GetRarityInfo(rarity);

			// Scale this segment's own base stats by the shared rarity multiplier.
			ApplyRarityStats(npc, rarityInfo);

			// Fresh instances of the head's modifier types, applied to this segment. Deep-copying by
			// type (rather than sharing references) keeps stateful affixes from cross-talking — the same
			// idiom as Clone(). Each modifier's one-time stat changes are applied here to this segment.
			modifiers = new List<IModifier>(headSystem.modifiers.Count);
			foreach (var modifier in headSystem.modifiers)
			{
				var instance = (IModifier)System.Activator.CreateInstance(modifier.GetType());
				instance.Apply(npc);
				modifiers.Add(instance);
			}

			GenerateDisplayName(npc, rarityInfo);
			modifiersInitialized = true;
		}

		#endregion

		#region Rarity & Stats

		private void ApplyRarityStats(Terraria.NPC npc, RarityInfo rarityInfo)
		{
			// Bosses: rarity is cosmetic only (name / color / XP / loot). Their HP and damage come
			// solely from the deterministic boss curve (NPCLevelManager.ApplyLevelScaling), so no rarity
			// stat multiplier — this is half of what stops the old rarity × Juggernaut HP blow-up (the
			// other half is excluding HP-inflating affixes in RollBossModifiers). Trash is unaffected.
			if (BossProgressionTracker.IsTrackedBoss(npc))
				return;

			if (rarityInfo.StatMultiplier > 1.0f)
			{
				// Increase health
				npc.lifeMax = (int)(npc.lifeMax * rarityInfo.StatMultiplier);
				npc.life = npc.lifeMax;

				// Increase damage
				npc.damage = (int)(npc.damage * rarityInfo.StatMultiplier);

				// Increase defense slightly
				npc.defense = (int)(npc.defense * (1.0f + (rarityInfo.StatMultiplier - 1.0f) * 0.5f));
				// Keep the armor snapshot in sync so aiStyles that reset defense = defDefense each frame
				// (Skeletron, EoC, Golem, Prime) don't wipe this bonus. See NPCLevelManager.ApplyLevelScaling.
				npc.defDefense = npc.defense;
			}
		}

		private void GenerateDisplayName(Terraria.NPC npc, RarityInfo rarityInfo)
		{
			string baseName = npc.TypeName;
			
			// Build prefix from modifiers
			string prefix = "";
			if (modifiers.Count > 0)
			{
				prefix = modifiers[0].GetPrefix();
				if (modifiers.Count > 1)
				{
					prefix = $"{modifiers[0].GetPrefix()} {modifiers[1].GetPrefix()}";
				}
			}

			// Combine: [Rarity] [Modifier Prefix] [Base Name]
			if (rarity != EnemyRarity.Common)
			{
				if (!string.IsNullOrEmpty(prefix))
					displayName = $"{rarityInfo.Name} {prefix} {baseName}";
				else
					displayName = $"{rarityInfo.Name} {baseName}";
			}
			else if (!string.IsNullOrEmpty(prefix))
			{
				displayName = $"{prefix} {baseName}";
			}
			else
			{
				displayName = baseName;
			}
		}

		#endregion

		#region Accessors

		public EnemyRarity GetRarity() => rarity;
		
		public RarityInfo GetRarityInfo() => EnemyRarityConfig.GetRarityInfo(rarity);

		public List<IModifier> GetModifiers() => modifiers;

		public string GetDisplayName() => displayName;

		public float GetTotalXPMultiplier()
		{
			float multiplier = GetRarityInfo().XPMultiplier;
			
			// Apply modifier XP bonuses
			foreach (var modifier in modifiers)
			{
				multiplier += modifier.GetXPBonus();
			}

			return multiplier;
		}

		#endregion

		#region Hooks

		/// <summary>
		/// The enemy just damaged the player — this is where ailments land. Kept strictly separate from
		/// the ModifyHitBy* hooks below, which fire when the player attacks the enemy; conflating the
		/// two is what made every ailment fire backwards until 2026-07-17. See IModifier.OnHitPlayer.
		/// </summary>
		public override void OnHitPlayer(Terraria.NPC npc, Terraria.Player target, Terraria.Player.HurtInfo hurtInfo)
		{
			foreach (var modifier in modifiers)
			{
				modifier.OnHitPlayer(npc, target, hurtInfo);
			}
		}

		public override void ModifyHitByItem(Terraria.NPC npc, Terraria.Player player, Item item, ref Terraria.NPC.HitModifiers hitModifiers)
		{
			// Apply modifier effects on hit
			foreach (var modifier in modifiers)
			{
				modifier.OnHitByPlayer(npc, player);
			}
		}

		public override void ModifyHitByProjectile(Terraria.NPC npc, Projectile projectile, ref Terraria.NPC.HitModifiers hitModifiers)
		{
			// Apply modifier effects on projectile hit
			if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
			{
				var player = Main.player[projectile.owner];
				foreach (var modifier in modifiers)
				{
					modifier.OnHitByPlayer(npc, player);
				}
			}
		}

		public override void UpdateLifeRegen(Terraria.NPC npc, ref int damage)
		{
			// Apply modifier life regen effects (DoTs, healing, etc.)
			foreach (var modifier in modifiers)
			{
				modifier.UpdateLifeRegen(npc, ref damage);
			}
		}

		public override void AI(Terraria.NPC npc)
		{
			// Apply modifier update effects (projectile firing, auras, etc.)
			foreach (var modifier in modifiers)
			{
				modifier.Update(npc);
			}
		}

		public override void OnKill(Terraria.NPC npc)
		{
			// Apply modifier on-kill effects (explosions, etc.)
			foreach (var modifier in modifiers)
			{
				modifier.OnKill(npc);
			}
		}

		public override void ModifyNPCLoot(Terraria.NPC npc, Terraria.ModLoader.NPCLoot npcLoot)
		{
			// Rarity affects drop rates
			var rarityInfo = GetRarityInfo();
			
			// This will be used by drop rules to modify chances and quantities
			// Actual implementation depends on how drops are configured
		}

		#endregion

		#region Cloning

		protected override bool CloneNewInstances => false;

		public override GlobalNPC Clone(Terraria.NPC from, Terraria.NPC to)
		{
			var fromGlobal = from.GetGlobalNPC<EnemyModifierSystem>();
			var toGlobal = (EnemyModifierSystem)base.Clone(from, to);

			toGlobal.rarity = fromGlobal.rarity;
			// Deep-copy the modifier list. A shallow copy (new List of the same references) shares the
			// IModifier instances, so a split NPC (e.g. a worm) and its clone would share stateful
			// affixes such as VileSpitModifier.shootTimer and cross-talk. Reconstruct each modifier by
			// type: its one-time stat changes (defense, etc.) are already baked into the cloned NPC, so
			// only fresh per-frame state is needed. Stateless affixes (the elemental wards) are
			// unaffected either way.
			toGlobal.modifiers = new List<IModifier>(fromGlobal.modifiers.Count);
			foreach (var modifier in fromGlobal.modifiers)
			{
				toGlobal.modifiers.Add((IModifier)System.Activator.CreateInstance(modifier.GetType()));
			}
			toGlobal.modifiersInitialized = fromGlobal.modifiersInitialized;
			toGlobal.displayName = fromGlobal.displayName;

			return toGlobal;
		}

		#endregion
	}
}
