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

		public override void OnSpawn(Terraria.NPC npc, Terraria.DataStructures.IEntitySource source)
		{
			// Only apply to hostile NPCs
			if (npc.friendly || npc.townNPC || npc.lifeMax <= 5) return;

			InitializeModifiers(npc);
		}

		private void InitializeModifiers(Terraria.NPC npc)
		{
			if (modifiersInitialized) return;

			// Roll rarity
			rarity = EnemyRarityConfig.RollRarity();
			var rarityInfo = EnemyRarityConfig.GetRarityInfo(rarity);

			// Apply rarity stat multipliers
			ApplyRarityStats(npc, rarityInfo);

			// Special boss modifier logic
			if (npc.boss)
			{bool isBossDefeated = IsBossAlreadyDefeated(npc.type);
				
				// First kill of this specific boss - no modifiers
				if (!isBossDefeated)
				{
					// No modifiers for first kill of this boss
					modifiers = new List<IModifier>();
				}
				// Subsequent kills of this boss - 2-5 modifiers
				else
				{
					int modifierCount = Main.rand.Next(2, 6); // 2-5 modifiers
					modifiers = ModifierPool.RollModifiers(modifierCount, npc);

					// Apply each modifier
					foreach (var modifier in modifiers)
					{
						modifier.Apply(npc);
					}
				}
			}
			// Normal enemies - use rarity-based modifiers
			else if (rarityInfo.MaxModifiers > 0)
			{
				int modifierCount = Main.rand.Next(1, rarityInfo.MaxModifiers + 1);
				modifiers = ModifierPool.RollModifiers(modifierCount, npc);

				// Apply each modifier
				foreach (var modifier in modifiers)
				{
					modifier.Apply(npc);
				}
			}

			// Generate display name
			GenerateDisplayName(npc, rarityInfo);

			modifiersInitialized = true;
		}

		#endregion
/// <summary>
		/// Check if a specific boss has been defeated before
		/// </summary>
		private bool IsBossAlreadyDefeated(int npcType)
		{
			return npcType switch
			{
				NPCID.KingSlime => BossKillTracker.DownedKingSlime,
				NPCID.EyeofCthulhu => BossKillTracker.DownedEyeOfCthulhu,
				NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail => BossKillTracker.DownedEvilBoss,
				NPCID.BrainofCthulhu => BossKillTracker.DownedEvilBoss,
				NPCID.QueenBee => BossKillTracker.DownedQueenBee,
				NPCID.SkeletronHead => BossKillTracker.DownedSkeletron,
				NPCID.Deerclops => BossKillTracker.DownedDeerclops,
				NPCID.WallofFlesh or NPCID.WallofFleshEye => BossKillTracker.DownedWallOfFlesh,
				_ => false // Unknown boss or not tracked
			};
		}

		
		#region Rarity & Stats

		private void ApplyRarityStats(Terraria.NPC npc, RarityInfo rarityInfo)
		{
			if (rarityInfo.StatMultiplier > 1.0f)
			{
				// Increase health
				npc.lifeMax = (int)(npc.lifeMax * rarityInfo.StatMultiplier);
				npc.life = npc.lifeMax;

				// Increase damage
				npc.damage = (int)(npc.damage * rarityInfo.StatMultiplier);

				// Increase defense slightly
				npc.defense = (int)(npc.defense * (1.0f + (rarityInfo.StatMultiplier - 1.0f) * 0.5f));
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

		public override void ModifyHitByItem(Terraria.NPC npc, Terraria.Player player, Item item, ref Terraria.NPC.HitModifiers hitModifiers)
		{
			// Apply modifier effects on hit
			foreach (var modifier in modifiers)
			{
				modifier.OnHit(npc, player);
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
					modifier.OnHit(npc, player);
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
			toGlobal.modifiers = new List<IModifier>(fromGlobal.modifiers);
			toGlobal.modifiersInitialized = fromGlobal.modifiersInitialized;
			toGlobal.displayName = fromGlobal.displayName;

			return toGlobal;
		}

		#endregion
	}
}
