using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.Localization;
using Microsoft.Xna.Framework;
using ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints.NotablePassives
{
	/// <summary>
	/// Notable Passive: Stagger
	/// Redirects a percentage of damage taken to be dealt over time instead of instantly
	/// </summary>
	public class Stagger : ModPlayer
	{
		// Base values
		private const float BASE_DAMAGE_PERCENT = 0.40f; // 40% of damage
		private const float BASE_DURATION = 4f; // 4 seconds
		private const float PERCENT_PER_TIER = 0.05f; // 5% per tier
		private const float DURATION_PER_TIER = 0.5f; // 0.5 seconds per tier
		private const int MAX_TIER = 4;

		// Tracking stagger instances
		private List<StaggerInstance> staggerInstances = new List<StaggerInstance>();
		private int currentTier = 0;
		private bool isActive = false;
		private float accumulatedDamage = 0f; // Track fractional damage between ticks
		private float pendingStaggerPercent = 0f; // Track stagger percent for PostHurt

		public override void ResetEffects()
		{
			// Check if player has allocated the Stagger notable
			PassiveTreeManager treeManager = Player.GetModPlayer<PassiveTreeManager>();
			currentTier = treeManager.GetNodeTier("warrior_tree", "stagger_notable");
			isActive = currentTier > 0;
		}

		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			if (!isActive || currentTier <= 0)
			{
				pendingStaggerPercent = 0f;
				return;
			}

			// Calculate stagger percentage based on tier
			pendingStaggerPercent = BASE_DAMAGE_PERCENT + (PERCENT_PER_TIER * (currentTier - 1));
			
			// Reduce incoming damage by the stagger percentage
			modifiers.FinalDamage *= (1f - pendingStaggerPercent);
		}

		public override void PostHurt(Player.HurtInfo info)
		{
			if (!isActive || currentTier <= 0 || pendingStaggerPercent <= 0f)
				return;

			// Calculate stagger duration based on tier
			float staggerDuration = BASE_DURATION + (DURATION_PER_TIER * (currentTier - 1));

			// info.Damage is the reduced damage after ModifyHurt
			// Calculate the original damage and staggered portion
			// If original damage was X and we reduced it by staggerPercent:
			// info.Damage = X * (1 - staggerPercent)
			// staggeredDamage = X * staggerPercent = info.Damage * (staggerPercent / (1 - staggerPercent))
			float staggeredDamage = info.Damage * (pendingStaggerPercent / (1f - pendingStaggerPercent));

			// Only apply if there's damage to stagger
			if (staggeredDamage > 0)
			{
				// Create a new stagger instance
				StaggerInstance instance = new StaggerInstance
				{
					TotalDamage = staggeredDamage,
					RemainingDamage = staggeredDamage,
					Duration = staggerDuration,
					TimeRemaining = staggerDuration,
					DamagePerSecond = staggeredDamage / staggerDuration
				};
				
				staggerInstances.Add(instance);
				
				// Apply stagger debuff
				Player.AddBuff(ModContent.BuffType<StaggerDebuff>(), 60 * (int)staggerDuration + 60);
				
				// Visual feedback
				if (Main.netMode != Terraria.ID.NetmodeID.Server)
				{
					CombatText.NewText(Player.getRect(), Color.Orange, $"{(int)staggeredDamage} Staggered", false, false);
				}
			}
			
			// Reset pending stagger
			pendingStaggerPercent = 0f;
		}

		public override void PostUpdateMiscEffects()
		{
			if (!isActive || staggerInstances.Count == 0)
			{
				accumulatedDamage = 0f;
				return;
			}

			float deltaTime = 1f / 60f; // Assuming 60 FPS
			List<StaggerInstance> instancesToRemove = new List<StaggerInstance>();

			// Process all active stagger instances
			float totalDamageThisTick = 0f; // Changed to float to track fractional damage
			
			foreach (var instance in staggerInstances)
			{
				instance.TimeRemaining -= deltaTime;
				
				if (instance.TimeRemaining <= 0)
				{
					// Instance expired, deal any remaining damage
					if (instance.RemainingDamage > 0)
					{
						totalDamageThisTick += instance.RemainingDamage;
						instance.RemainingDamage = 0;
					}
					instancesToRemove.Add(instance);
				}
				else
				{
					// Deal damage over time
					float damageThisTick = instance.DamagePerSecond * deltaTime;
					instance.RemainingDamage -= damageThisTick;
					totalDamageThisTick += damageThisTick;
					
					// Ensure we don't deal more than remaining
					if (instance.RemainingDamage < 0)
					{
						totalDamageThisTick += instance.RemainingDamage; // Add back the overflow
						instance.RemainingDamage = 0;
					}
				}
			}

			// Accumulate fractional damage
			accumulatedDamage += totalDamageThisTick;
			
			// Only apply damage when we have at least 1 full point
			if (accumulatedDamage >= 1f)
			{
				int damageToApply = (int)accumulatedDamage;
				accumulatedDamage -= damageToApply; // Keep the fractional remainder
				
				// Apply damage
				Player.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
					NetworkText.FromLiteral(Player.name + " was staggered to death.")), 
					damageToApply, 0, false, false, -1, false, 0);
			}

			// Remove expired instances
			foreach (var instance in instancesToRemove)
			{
				staggerInstances.Remove(instance);
			}

			// Update buff duration
			if (staggerInstances.Count > 0)
			{
				// Find the longest remaining duration
				float longestDuration = 0;
				foreach (var instance in staggerInstances)
				{
					if (instance.TimeRemaining > longestDuration)
						longestDuration = instance.TimeRemaining;
				}
				
				// Ensure buff stays active
				int buffIndex = Player.FindBuffIndex(ModContent.BuffType<StaggerDebuff>());
				if (buffIndex >= 0)
				{
					Player.buffTime[buffIndex] = (int)(longestDuration * 60) + 10;
				}
			}
			else
			{
				// All stagger instances cleared, reset accumulated damage
				accumulatedDamage = 0f;
			}
		}

		/// <summary>
		/// Get total staggered damage remaining
		/// </summary>
		public float GetTotalStaggeredDamage()
		{
			float total = 0;
			foreach (var instance in staggerInstances)
			{
				total += instance.RemainingDamage;
			}
			return total;
		}

		/// <summary>
		/// Get the longest remaining stagger duration
		/// </summary>
		public float GetLongestStaggerDuration()
		{
			float longest = 0;
			foreach (var instance in staggerInstances)
			{
				if (instance.TimeRemaining > longest)
					longest = instance.TimeRemaining;
			}
			return longest;
		}

		/// <summary>
		/// Class to track individual stagger damage instances
		/// </summary>
		private class StaggerInstance
		{
			public float TotalDamage { get; set; }
			public float RemainingDamage { get; set; }
			public float Duration { get; set; }
			public float TimeRemaining { get; set; }
			public float DamagePerSecond { get; set; }
		}
	}

	/// <summary>
	/// Visual buff to show player has staggered damage
	/// </summary>
	public class StaggerDebuff : ModBuff
	{
		public override string Texture => "Terraria/Images/Buff_156"; // Use Bleeding debuff icon

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = false;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			// Buff is purely visual, actual logic is in Stagger ModPlayer
		}

		public override bool RightClick(int buffIndex)
		{
			// Cannot be cancelled by right-clicking
			return false;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			Stagger stagger = Main.LocalPlayer.GetModPlayer<Stagger>();
			float remainingDamage = stagger.GetTotalStaggeredDamage();
			float duration = stagger.GetLongestStaggerDuration();
			
			buffName = "Staggered";
			tip = $"Taking {(int)remainingDamage} damage over {duration:F1} seconds";
			rare = 1; // Orange color
		}
	}
}
