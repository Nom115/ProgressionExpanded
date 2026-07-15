using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ProgressionExpanded.Src.Levels.PlayerSystems.Attributes;
using ProgressionExpanded.Src.Levels.PlayerSystems.PassivePoints;
using ProgressionExpanded.Src.Levels.PlayerSystems.Talents;
using ProgressionExpanded.Utils.DataManagers;

namespace ProgressionExpanded.Src.Levels.PlayerSystems
{
	/// <summary>
	/// Manages passive points that players earn on level up, and owns the point economy's
	/// invariant: available + spent == totalEarned, where totalEarned is append-only.
	///
	/// Points are spent on two competing sinks — attributes (AttributeManager) and minor talents
	/// (PassiveTreeManager). Because the true spend is split across ModPlayers whose LoadData order
	/// is an implementation detail, this class pulls both totals on the first update tick rather
	/// than letting either sink push its own reconcile. See PostUpdateMiscEffects.
	/// </summary>
	public class PassivePointManager : ModPlayer
	{
		private const string AVAILABLE_POINTS_KEY = "AvailablePassivePoints";
		private const string TOTAL_POINTS_KEY = "TotalPassivePointsEarned";
		private const string SPENT_POINTS_KEY = "SpentPassivePoints";
		private const string SCHEMA_VERSION_KEY = "ProgressionSchemaVersion";

		/// <summary>
		/// Bump when a change invalidates existing allocations. Characters below this get a one-shot
		/// wipe-and-refund on load (see RunSchemaMigration).
		///
		/// v2 = attributes + boss-gated talent slots.
		/// v3 = pinnacle rebalance + masteries gated on total attribute points. The wipe is not
		///      cosmetic here: the gate makes states a v2 save can freely be in (eight masteries,
		///      zero attributes) unreachable, so those allocations have to be rebuilt under the new
		///      rule rather than grandfathered.
		/// </summary>
		private const int SCHEMA_VERSION = 3;

		private int availablePoints = 0;
		private int totalPointsEarned = 0;
		private int spentPoints = 0;
		private int schemaVersion = 0;
		private bool reconciled = false;

		public override void Initialize()
		{
			availablePoints = 0;
			totalPointsEarned = 0;
			spentPoints = 0;
			schemaVersion = 0;
			reconciled = false;
		}

		/// <summary>
		/// Migrate then reconcile, exactly once per character load. This runs here rather than in
		/// either sink because by the first update tick every ModPlayer's LoadData has definitively
		/// run — so this is the earliest point where the true spend can be summed without depending
		/// on load order between managers.
		/// </summary>
		public override void PostUpdateMiscEffects()
		{
			if (reconciled)
				return;

			RunSchemaMigration();

			int spent = Player.GetModPlayer<PassiveTreeManager>().GetTotalSpentAcrossTrees()
					  + Player.GetModPlayer<AttributeManager>().GetTotalSpentOnAttributes();
			ReconcileSpentPoints(spent);

			reconciled = true;
		}

		/// <summary>
		/// One-shot upgrade of a pre-rework character. Old node ids are gone, flat bonuses became
		/// percentages and the slot cap was replaced, so no honest old-to-new mapping exists —
		/// everything is refunded and respent instead. Worlds need no equivalent: talent slots read
		/// vanilla's live boss flags, so an existing world simply has the right slots already open.
		/// </summary>
		private void RunSchemaMigration()
		{
			if (schemaVersion >= SCHEMA_VERSION)
				return;

			Player.GetModPlayer<PassiveTreeManager>().WipeAllocations();
			Player.GetModPlayer<AttributeManager>().WipeAll();

			// All three pinnacle choices were redefined in v3, so the pick a player made no longer
			// describes the talent they now have — someone who took Juggernaut for double life would
			// silently be holding a half-life, half-speed armour talent instead. Only this slot is
			// cleared: the other five slots' talents are untouched, and clearing them would be
			// busywork rather than a re-decision. ClearSlot fires OnDeactivate, so an in-flight
			// Stagger bleed tears itself down cleanly rather than outliving the pick.
			Player.GetModPlayer<TalentPlayer>().ClearSlot("pinnacle");

			// Allocations used to live in PlayerDataManager's string dictionary. They have their own
			// TagCompound now, so the old key is orphaned data that would otherwise ride along in
			// every save forever.
			PlayerDataManager.RemoveKey(Player, "PassiveTreeAllocations");

			// Rebuild the budget from level, not from the stored counter. Levels 2..MAX award one
			// point each, so level-1 is the true lifetime total. If a save predates the award path
			// or ever missed a level-up, this repairs the shortfall rather than locking it in.
			// totalPointsEarned is append-only, so it is only ever revised upward.
			int earnedByLevel = PlayerLevelManager.GetLevel(Player) - 1;
			if (earnedByLevel < 0)
				earnedByLevel = 0;
			if (totalPointsEarned < earnedByLevel)
				totalPointsEarned = earnedByLevel;

			ResetAllPoints();
			schemaVersion = SCHEMA_VERSION;

			if (Main.netMode != NetmodeID.Server && totalPointsEarned > 0)
			{
				Main.NewText(
					$"Progression Expanded has been rebalanced — {totalPointsEarned} passive points refunded, "
					+ "and your Pinnacle talent has been cleared to re-pick. Press P.",
					new Color(255, 215, 0));
				Main.NewText(
					"New: each mastery past your first requires 10 more total attribute points than the last.",
					new Color(255, 215, 0));
			}
		}

		// Points are stored directly in this ModPlayer's own TagCompound, so LoadData
		// populates the fields straight from the save. There is no Initialize-before-LoadData
		// hazard here (unlike managers that read PlayerDataManager in Initialize), so no
		// deferred read is needed and SaveData is invoked automatically by tModLoader.
		public override void SaveData(Terraria.ModLoader.IO.TagCompound tag)
		{
			tag[AVAILABLE_POINTS_KEY] = availablePoints;
			tag[TOTAL_POINTS_KEY] = totalPointsEarned;
			tag[SPENT_POINTS_KEY] = spentPoints;
			tag[SCHEMA_VERSION_KEY] = schemaVersion;
		}

		public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
		{
			availablePoints = tag.GetInt(AVAILABLE_POINTS_KEY);
			totalPointsEarned = tag.GetInt(TOTAL_POINTS_KEY);
			spentPoints = tag.GetInt(SPENT_POINTS_KEY);
			// Absent on every pre-rework save, so GetInt's 0 default is exactly the signal that a
			// character still needs migrating.
			schemaVersion = tag.GetInt(SCHEMA_VERSION_KEY);
		}

		/// <summary>
		/// Get the number of available (unspent) passive points.
		/// </summary>
		public int GetAvailablePoints()
		{
			return availablePoints;
		}

		/// <summary>
		/// Get the total number of passive points earned (spent + available).
		/// </summary>
		public int GetTotalPointsEarned()
		{
			return totalPointsEarned;
		}

		/// <summary>
		/// Get the number of spent passive points.
		/// </summary>
		public int GetSpentPoints()
		{
			return spentPoints;
		}

		/// <summary>
		/// Award passive points to the player (called on level up).
		/// </summary>
		/// <param name="amount">Number of points to award.</param>
		public void AwardPoints(int amount)
		{
			if (amount <= 0)
				return;

			availablePoints += amount;
			totalPointsEarned += amount;
		}

		/// <summary>
		/// Spend passive points.
		/// </summary>
		/// <returns>True if the player had enough points to spend.</returns>
		public bool SpendPoints(int amount)
		{
			if (amount <= 0)
				return false;

			if (availablePoints < amount)
				return false;

			availablePoints -= amount;
			spentPoints += amount;
			return true;
		}

		/// <summary>
		/// Check if the player has enough points to spend.
		/// </summary>
		public bool HasEnoughPoints(int amount)
		{
			return availablePoints >= amount;
		}

		/// <summary>
		/// Refund passive points (for deallocation / respec functionality).
		/// </summary>
		/// <param name="amount">Number of points to refund.</param>
		public void RefundPoints(int amount)
		{
			if (amount <= 0)
				return;

			int refundAmount = System.Math.Min(amount, spentPoints);
			availablePoints += refundAmount;
			spentPoints -= refundAmount;
		}

		/// <summary>
		/// Force spent (and thus available) to match the true allocated total. Called once on
		/// load so the point economy can never drift out of sync with the actual allocations —
		/// e.g. if a corrupted allocation save reset the tree but not the spent counter.
		/// totalPointsEarned is authoritative (append-only, +1 per level), so available is
		/// derived as total - spent.
		/// </summary>
		public void ReconcileSpentPoints(int actualSpent)
		{
			if (actualSpent < 0)
				actualSpent = 0;

			if (actualSpent == spentPoints)
				return;

			spentPoints = actualSpent;
			availablePoints = totalPointsEarned - spentPoints;
			if (availablePoints < 0)
				availablePoints = 0;
		}

		/// <summary>
		/// Reset all passive points (refund all spent points back to available).
		/// </summary>
		public void ResetAllPoints()
		{
			availablePoints = totalPointsEarned;
			spentPoints = 0;
		}

		/// <summary>
		/// Clear all passive points (for a new character or a full reset).
		/// </summary>
		public void ClearAllPoints()
		{
			availablePoints = 0;
			totalPointsEarned = 0;
			spentPoints = 0;
		}

		/// <summary>
		/// Static helper: Award passive points to a player.
		/// </summary>
		public static void AwardPlayerPoints(Player player, int amount)
		{
			player.GetModPlayer<PassivePointManager>().AwardPoints(amount);
		}

		/// <summary>
		/// Static helper: Get a player's available passive points.
		/// </summary>
		public static int GetPlayerAvailablePoints(Player player)
		{
			return player.GetModPlayer<PassivePointManager>().GetAvailablePoints();
		}

		/// <summary>
		/// Static helper: Spend passive points for a player.
		/// </summary>
		public static bool SpendPlayerPoints(Player player, int amount)
		{
			return player.GetModPlayer<PassivePointManager>().SpendPoints(amount);
		}

		/// <summary>
		/// Static helper: Check if a player has enough points.
		/// </summary>
		public static bool PlayerHasEnoughPoints(Player player, int amount)
		{
			return player.GetModPlayer<PassivePointManager>().HasEnoughPoints(amount);
		}
	}
}
