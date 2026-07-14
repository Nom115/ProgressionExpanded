using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerSystems;
using ProgressionExpanded.Utils;

namespace ProgressionExpanded.Src.NPCs.Enemy
{
	/// <summary>
	/// Applies the enemy -> player level-difference damage multiplier to HOSTILE projectiles,
	/// so ranged and boss attacks scale with the firing enemy's level exactly like contact
	/// damage does (see <see cref="NPCLevelManager.ModifyHitPlayer"/>).
	///
	/// The firing NPC's level (and whether it's an untouched "pinnacle" boss) is captured at
	/// spawn from its <see cref="NPCLevelManager"/>. Because it reads any NPC's level generically,
	/// this works for projectiles fired by modded enemies with no per-enemy registration.
	/// </summary>
	public class HostileProjectileScaling : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		private bool fromLeveledEnemy = false;
		private bool sourceIsPinnacleBoss = false;
		private int sourceEnemyLevel = 1;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			// Only projectiles spawned directly by a hostile NPC carry that NPC's level.
			if (source is EntitySource_Parent parent && parent.Entity is NPC npc && !npc.friendly)
			{
				fromLeveledEnemy = true;
				sourceEnemyLevel = npc.GetGlobalNPC<NPCLevelManager>().GetLevel(npc);
				sourceIsPinnacleBoss = BossProgressionTracker.IsPinnacleEncounter(npc);
			}
		}

		public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
		{
			// Pinnacle bosses ignore level entirely — their projectiles are untouched.
			if (!fromLeveledEnemy || !projectile.hostile || sourceIsPinnacleBoss) return;

			int playerLevel = PlayerLevelManager.GetLevel(target);
			float multiplier = NPCLevelManager.GetEnemyDamageMultiplier(sourceEnemyLevel, playerLevel);
			if (multiplier != 1.0f)
			{
				modifiers.FinalDamage *= multiplier;
			}
		}
	}
}
