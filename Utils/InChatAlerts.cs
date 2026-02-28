using Microsoft.Xna.Framework;
using Terraria;

namespace ProgressionExpanded.Utils
{
	public enum AlertType
	{
		Info,
		Warning,
		Debug
	}

	public static class InChatAlerts
	{
		/// <summary>
		/// Send a message to the in-game chat with appropriate styling based on alert type
		/// </summary>
		/// <param name="message">The message to display</param>
		/// <param name="type">The type of alert (Info, Warning, or Debug)</param>
		public static void SendAlert(string message, AlertType type = AlertType.Info)
		{
			Color color = GetColorForType(type);
			string prefix = GetPrefixForType(type);
			
			Main.NewText($"{prefix}{message}", color);
		}

		/// <summary>
		/// Send an info message to chat (cyan color)
		/// </summary>
		public static void Info(string message)
		{
			SendAlert(message, AlertType.Info);
		}

		/// <summary>
		/// Send a warning message to chat (yellow/orange color)
		/// </summary>
		public static void Warning(string message)
		{
			SendAlert(message, AlertType.Warning);
		}

		/// <summary>
		/// Send a debug message to chat (gray color)
		/// Only shows if debug mode is enabled
		/// </summary>
		public static void Debug(string message)
		{
			#if DEBUG
			SendAlert(message, AlertType.Debug);
			#endif
		}

		private static Color GetColorForType(AlertType type)
		{
			return type switch
			{
				AlertType.Info => new Color(100, 200, 255),      // Light blue/cyan
				AlertType.Warning => new Color(255, 200, 50),    // Yellow/orange
				AlertType.Debug => new Color(150, 150, 150),     // Gray
				_ => Color.White
			};
		}

		private static string GetPrefixForType(AlertType type)
		{
			return type switch
			{
				AlertType.Info => "[Progression Expanded] ",
				AlertType.Warning => "[Progression Expanded - Warning] ",
				AlertType.Debug => "[Progression Expanded - Debug] ",
				_ => "[Progression Expanded] "
			};
		}
	}
}
