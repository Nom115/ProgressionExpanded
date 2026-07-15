using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents.Behaviours
{
	/// <summary>
	/// Pinnacle: Stagger. Damage you take is dealt over time instead of instantly.
	///
	/// This is the balance yardstick for every other talent — if a talent slot choice would not
	/// swing your survival or damage about as far as this does, it is not big enough to be one of
	/// only six picks. It is fixed at what used to be the tier-4 form; there are no ranks any more.
	///
	/// It does not reduce damage. It buys you a window. Everything hits eventually, so Stagger only
	/// pays off if you can out-heal or out-run the bleed — which is why Fleshless (no regeneration)
	/// is a trap next to it, and why Vengeance, which wants damage to land hard and fast, is its
	/// opposite number in the same slot.
	/// </summary>
	public class StaggerTalent : TalentBehaviour
	{
		public override string Id => "stagger";
		public override string SlotKey => "pinnacle";
		public override string DisplayName => "Stagger";

		public override string Description =>
			"55% of the damage you take is dealt to you over 5.5 seconds instead of all at once. "
			+ "It is delayed, not prevented — you need to answer it.";

		private const float DamagePercent = 0.55f;
		private const float Duration = 5.5f;

		/// <summary>Backstop on concurrent instances. See ModifyHurt for why the check lives there.</summary>
		private const int MaxInstances = 10;

		private readonly List<StaggerInstance> instances = new List<StaggerInstance>();

		/// <summary>Sub-1 damage carried between ticks so a slow bleed still resolves exactly.</summary>
		private float accumulatedDamage;

		private float pendingStaggerPercent;

		/// <summary>
		/// Set while we deal the bleed. Player.Hurt re-enters ModifyHurt/PostHurt, so without this
		/// the stagger damage would itself be staggered, renewing its own duration forever.
		/// </summary>
		private bool applyingStaggerDamage;

		public override void OnDeactivate(Player player)
		{
			ClearStagger(player);
		}

		public override void OnRespawn(Player player)
		{
			// Never carry a bleed across a life.
			ClearStagger(player);
		}

		private void ClearStagger(Player player)
		{
			instances.Clear();
			accumulatedDamage = 0f;

			int buffType = ModContent.BuffType<StaggerDebuff>();
			if (player.HasBuff(buffType))
				player.ClearBuff(buffType);
		}

		public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
		{
			if (applyingStaggerDamage)
			{
				pendingStaggerPercent = 0f;
				return;
			}

			// Decide the instance cap HERE, before mitigating. PostHurt is what carries the staggered
			// portion into a bleed, so if it won't be able to create an instance then the hit has to
			// land in full — otherwise we shave the damage here and the shaved portion is never dealt
			// at all, silently turning Stagger into flat damage reduction exactly when the player is
			// under the most pressure.
			if (instances.Count >= MaxInstances)
			{
				pendingStaggerPercent = 0f;
				return;
			}

			pendingStaggerPercent = DamagePercent;
			modifiers.FinalDamage *= 1f - pendingStaggerPercent;
		}

		public override void PostHurt(Player player, Player.HurtInfo info)
		{
			if (applyingStaggerDamage || pendingStaggerPercent <= 0f)
				return;

			// info.Damage is post-mitigation, so this inverts the ModifyHurt multiply to recover the
			// portion that was removed.
			float staggeredDamage = info.Damage * (pendingStaggerPercent / (1f - pendingStaggerPercent));
			pendingStaggerPercent = 0f;

			if (staggeredDamage <= 0)
				return;

			// Deliberately unclamped. Anything refused here is damage already removed from the hit,
			// so it would simply vanish. A huge bleed from a freak hit is correct — it is damage the
			// player would have eaten instantly anyway, and the window to answer it is the point.
			instances.Add(new StaggerInstance
			{
				RemainingDamage = staggeredDamage,
				TimeRemaining = Duration,
				DamagePerSecond = staggeredDamage / Duration,
			});

			player.AddBuff(ModContent.BuffType<StaggerDebuff>(), 60 * (int)Duration + 60);

			if (Main.netMode != Terraria.ID.NetmodeID.Server)
				CombatText.NewText(player.getRect(), Color.Orange, $"{(int)staggeredDamage} Staggered", false, false);
		}

		public override void PostUpdateMiscEffects(Player player)
		{
			if (instances.Count == 0)
			{
				accumulatedDamage = 0f;
				return;
			}

			const float deltaTime = 1f / 60f;
			float damageThisTick = 0f;

			for (int i = instances.Count - 1; i >= 0; i--)
			{
				StaggerInstance instance = instances[i];
				instance.TimeRemaining -= deltaTime;

				if (instance.TimeRemaining <= 0f)
				{
					// Expired — dump whatever is left rather than letting it evaporate.
					damageThisTick += instance.RemainingDamage;
					instances.RemoveAt(i);
					continue;
				}

				float tick = instance.DamagePerSecond * deltaTime;
				if (tick > instance.RemainingDamage)
					tick = instance.RemainingDamage;

				instance.RemainingDamage -= tick;
				damageThisTick += tick;
			}

			accumulatedDamage += damageThisTick;

			if (accumulatedDamage >= 1f)
			{
				int toApply = (int)accumulatedDamage;
				accumulatedDamage -= toApply;

				applyingStaggerDamage = true;
				try
				{
					player.Hurt(
						Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
							NetworkText.FromLiteral(player.name + " was staggered to death.")),
						toApply, 0, false, false, -1, false, 0);
				}
				finally
				{
					applyingStaggerDamage = false;
				}
			}

			if (instances.Count == 0)
			{
				accumulatedDamage = 0f;
				return;
			}

			// Keep the buff alive for as long as the longest bleed has left.
			float longest = 0f;
			foreach (StaggerInstance instance in instances)
			{
				if (instance.TimeRemaining > longest)
					longest = instance.TimeRemaining;
			}

			int buffIndex = player.FindBuffIndex(ModContent.BuffType<StaggerDebuff>());
			if (buffIndex >= 0)
				player.buffTime[buffIndex] = (int)(longest * 60f) + 10;
		}

		public float GetTotalStaggeredDamage()
		{
			float total = 0f;
			foreach (StaggerInstance instance in instances)
				total += instance.RemainingDamage;
			return total;
		}

		public float GetLongestStaggerDuration()
		{
			float longest = 0f;
			foreach (StaggerInstance instance in instances)
			{
				if (instance.TimeRemaining > longest)
					longest = instance.TimeRemaining;
			}
			return longest;
		}

		private class StaggerInstance
		{
			public float RemainingDamage { get; set; }
			public float TimeRemaining { get; set; }
			public float DamagePerSecond { get; set; }
		}
	}

	/// <summary>Display-only buff. All the logic lives in StaggerTalent.</summary>
	public class StaggerDebuff : ModBuff
	{
		public override string Texture => "Terraria/Images/Buff_156"; // Bleeding icon

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = false;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override bool RightClick(int buffIndex)
		{
			return false;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			buffName = "Staggered";
			rare = 1;

			// Read the live numbers off whichever StaggerTalent instance this player actually has.
			foreach (TalentBehaviour behaviour in TalentPlayer.Get(Main.LocalPlayer).Active)
			{
				if (behaviour is StaggerTalent stagger)
				{
					tip = $"Taking {(int)stagger.GetTotalStaggeredDamage()} damage over {stagger.GetLongestStaggerDuration():F1} seconds";
					return;
				}
			}

			tip = "Taking damage over time";
		}
	}
}
