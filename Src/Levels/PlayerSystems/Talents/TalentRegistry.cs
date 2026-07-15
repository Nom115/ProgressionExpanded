using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria.ModLoader;

namespace ProgressionExpanded.Src.Levels.PlayerSystems.Talents
{
	/// <summary>
	/// Discovers every TalentBehaviour in the assembly and indexes it by id and by slot.
	///
	/// Because a behaviour declares its own id and slot, "a talent that was declared but never
	/// implemented" cannot exist. What CAN still go wrong is a duplicate id, a typo'd slot key, or a
	/// slot that ends up with the wrong number of choices — so those are checked at load and fail
	/// loudly. A typo'd SlotKey is the nasty one: it silently leaves one slot with two choices and
	/// another with four, and nothing else in the game would ever complain.
	/// </summary>
	public static class TalentRegistry
	{
		public const int ChoicesPerSlot = 3;

		private static readonly Dictionary<string, Type> typesById = new Dictionary<string, Type>();

		/// <summary>One instance per talent, used only to read display metadata without a player.</summary>
		private static readonly Dictionary<string, TalentBehaviour> prototypesById = new Dictionary<string, TalentBehaviour>();

		private static readonly Dictionary<string, List<string>> idsBySlot = new Dictionary<string, List<string>>();

		public static void Load(Mod mod)
		{
			Unload();

			IEnumerable<Type> found = Assembly.GetExecutingAssembly()
				.GetTypes()
				.Where(t => t.IsSubclassOf(typeof(TalentBehaviour)) && !t.IsAbstract);

			foreach (Type type in found)
			{
				TalentBehaviour prototype;
				try
				{
					prototype = (TalentBehaviour)Activator.CreateInstance(type);
				}
				catch (Exception e)
				{
					mod.Logger.Error($"Talent '{type.Name}' could not be constructed. It needs a public parameterless constructor.", e);
					continue;
				}

				if (string.IsNullOrEmpty(prototype.Id))
				{
					mod.Logger.Error($"Talent '{type.Name}' has an empty Id and was skipped.");
					continue;
				}

				if (typesById.ContainsKey(prototype.Id))
				{
					mod.Logger.Error($"Talent id '{prototype.Id}' is declared by both '{typesById[prototype.Id].Name}' and '{type.Name}'. Ids are save keys and must be unique; '{type.Name}' was skipped.");
					continue;
				}

				if (TalentSlots.Get(prototype.SlotKey) == null)
				{
					mod.Logger.Error($"Talent '{prototype.Id}' names slot '{prototype.SlotKey}', which does not exist. Valid slots: {string.Join(", ", TalentSlots.All.Select(s => s.Key))}. Skipped.");
					continue;
				}

				typesById[prototype.Id] = type;
				prototypesById[prototype.Id] = prototype;

				if (!idsBySlot.TryGetValue(prototype.SlotKey, out List<string> slotIds))
				{
					slotIds = new List<string>();
					idsBySlot[prototype.SlotKey] = slotIds;
				}
				slotIds.Add(prototype.Id);
			}

			ValidateSlotCoverage(mod);
			mod.Logger.Info($"Loaded {typesById.Count} talents across {TalentSlots.Count} slots.");
		}

		/// <summary>
		/// Every slot must offer exactly three choices. This is the check that catches a typo'd
		/// SlotKey, which otherwise presents in-game as one slot mysteriously offering the wrong
		/// options — with no error to trace it back to.
		/// </summary>
		private static void ValidateSlotCoverage(Mod mod)
		{
			foreach (TalentSlot slot in TalentSlots.All)
			{
				int count = idsBySlot.TryGetValue(slot.Key, out List<string> ids) ? ids.Count : 0;
				if (count != ChoicesPerSlot)
				{
					mod.Logger.Error(
						$"Slot '{slot.Key}' has {count} talents, expected {ChoicesPerSlot}"
						+ (count > 0 ? $" ({string.Join(", ", ids)})" : " (none registered)")
						+ ". Check for a mistyped SlotKey.");
				}
			}
		}

		public static void Unload()
		{
			typesById.Clear();
			prototypesById.Clear();
			idsBySlot.Clear();
		}

		public static bool Exists(string id)
		{
			return id != null && typesById.ContainsKey(id);
		}

		/// <summary>Fresh instance for a specific player. Returns null for an unknown id.</summary>
		public static TalentBehaviour Create(string id)
		{
			if (id == null || !typesById.TryGetValue(id, out Type type))
				return null;

			return (TalentBehaviour)Activator.CreateInstance(type);
		}

		/// <summary>Shared instance for reading display metadata. Never mutate it, never use it to run hooks.</summary>
		public static TalentBehaviour GetPrototype(string id)
		{
			if (id == null)
				return null;
			prototypesById.TryGetValue(id, out TalentBehaviour prototype);
			return prototype;
		}

		public static IReadOnlyList<string> GetChoices(string slotKey)
		{
			if (slotKey != null && idsBySlot.TryGetValue(slotKey, out List<string> ids))
				return ids;
			return Array.Empty<string>();
		}
	}
}
