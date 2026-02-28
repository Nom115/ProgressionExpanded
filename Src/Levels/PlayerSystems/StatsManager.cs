using Terraria;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Damage;
using ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Defences;
using ProgressionExpanded.Src.Levels.PlayerSystems.Stats.Tertiary;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// Central manager for all player stats (Damage, Defences, Tertiary)
	/// Coordinates stat modifications and applies them to the player
	/// </summary>
	public class StatsManager : ModPlayer
	{
		// Damage Stats
		public MeleeDamage MeleeDamage { get; private set; }
		public MagicDamage MagicDamage { get; private set; }
		public RangedDamage RangedDamage { get; private set; }
		public SummonDamage SummonDamage { get; private set; }
		public CritChance CritChance { get; private set; }
		public AttackSpeed AttackSpeed { get; private set; }
		public ArmorPenetration ArmorPenetration { get; private set; }
		public GenericDamage GenericDamage { get; private set; }
		public MinionSlots MinionSlots { get; private set; }
		public ManaEfficiency ManaEfficiency { get; private set; }
		public CriticalDamageMultiplier CriticalDamageMultiplier { get; private set; }

		// Defence Stats
		public Defense Defense { get; private set; }
		public Endurance Endurance { get; private set; }
		public Vitality Vitality { get; private set; }
		public LifeRegen LifeRegen { get; private set; }

		// Tertiary Stats
		public KnockbackScaling KnockbackScaling { get; private set; }
		public ProjectileSpeed ProjectileSpeed { get; private set; }
		public UseSpeed UseSpeed { get; private set; }
		public MovementSpeed MovementSpeed { get; private set; }

		public override void Initialize()
		{
			// Initialize all damage stats
			MeleeDamage = new MeleeDamage(Player);
			MagicDamage = new MagicDamage(Player);
			RangedDamage = new RangedDamage(Player);
			SummonDamage = new SummonDamage(Player);
			CritChance = new CritChance(Player);
			AttackSpeed = new AttackSpeed(Player);
			ArmorPenetration = new ArmorPenetration(Player);
			GenericDamage = new GenericDamage(Player);
			MinionSlots = new MinionSlots(Player);
			ManaEfficiency = new ManaEfficiency(Player);
			CriticalDamageMultiplier = new CriticalDamageMultiplier(Player);

			// Initialize all defence stats
			Defense = new Defense(Player);
			Endurance = new Endurance(Player);
			Vitality = new Vitality(Player);
			LifeRegen = new LifeRegen(Player);

			// Initialize all tertiary stats
			KnockbackScaling = new KnockbackScaling(Player);
			ProjectileSpeed = new ProjectileSpeed(Player);
			UseSpeed = new UseSpeed(Player);
			MovementSpeed = new MovementSpeed(Player);
		}

		public override void ResetEffects()
		{
			// Reset all stats to base values each frame
			// This ensures buffs/debuffs are recalculated
			MeleeDamage.ResetEffects();
			MagicDamage.ResetEffects();
			RangedDamage.ResetEffects();
			SummonDamage.ResetEffects();
			CritChance.ResetEffects();
			AttackSpeed.ResetEffects();
			ArmorPenetration.ResetEffects();
			GenericDamage.ResetEffects();
			MinionSlots.ResetEffects();
			ManaEfficiency.ResetEffects();
			CriticalDamageMultiplier.ResetEffects();

			Defense.ResetEffects();
			Endurance.ResetEffects();
			Vitality.ResetEffects();
			LifeRegen.ResetEffects();

			KnockbackScaling.ResetEffects();
			ProjectileSpeed.ResetEffects();
			UseSpeed.ResetEffects();
			MovementSpeed.ResetEffects();
		}

		public override void PostUpdateMiscEffects()
		{
			// Apply all stat modifications after vanilla updates
			MeleeDamage.Apply();
			MagicDamage.Apply();
			RangedDamage.Apply();
			SummonDamage.Apply();
			CritChance.Apply();
			AttackSpeed.Apply();
			ArmorPenetration.Apply();
			GenericDamage.Apply();
			MinionSlots.Apply();
			ManaEfficiency.Apply();
			CriticalDamageMultiplier.Apply();

			Defense.Apply();
			Endurance.Apply();
			Vitality.Apply();
			LifeRegen.Apply();

			KnockbackScaling.Apply();
			ProjectileSpeed.Apply();
			UseSpeed.Apply();
			MovementSpeed.Apply();
		}

		/// <summary>
		/// Static helper to get a player's stats manager
		/// </summary>
		public static StatsManager GetStatsManager(Player player)
		{
			return player.GetModPlayer<StatsManager>();
		}
	}
}
