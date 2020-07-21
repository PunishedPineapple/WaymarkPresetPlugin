using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using Newtonsoft.Json.Schema;

namespace WaymarkPresetPlugin
{
	public class Plugin : IDalamudPlugin
	{
		//	Initialization
		public void Initialize( DalamudPluginInterface pluginInterface )
		{
			//	Configuration
			mPluginInterface = pluginInterface;
			mConfiguration = mPluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
			mConfiguration.Initialize( mPluginInterface );
			mGameMemoryHandler = new MemoryHandler( mPluginInterface );

			//	Get the game sheets that we need to populate a zone dictionary.
			ExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType> territorySheet = mPluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>();
			ExcelSheet<Lumina.Excel.GeneratedSheets.PlaceName> placeNameSheet = mPluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.PlaceName>();
			ExcelSheet<Lumina.Excel.GeneratedSheets.ContentFinderCondition> contentFinderSheet = mPluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.ContentFinderCondition>();
			
			//	Get the name for every "MapID" that is an instance zone.
			Dictionary<UInt16, Tuple<string, string>> zoneNames = new Dictionary<UInt16, Tuple<string, string>>();
			foreach( TerritoryType zone in territorySheet.ToList() )
			{
				//*****TODO: unknown24 should be changed to unknown10 for new version of Dalamud.  Currently unknown10 in test version.*****
				if( zone.ExclusiveType == 2 && !zoneNames.ContainsKey( zone.Unknown10 ) )
				{
					string dutyName = contentFinderSheet.GetRow( zone.Unknown10 ).Name.Trim();
					if( dutyName.Length > 0 )
					{
						dutyName = dutyName.First().ToString().ToUpper() + dutyName.Substring( 1 );
					}
					zoneNames.Add( zone.Unknown10, Tuple.Create( dutyName, zone.PlaceName.Value.Name ) );
				}
			}
			//	There are several zones with an "ID" of zero, but none of them allow waymarks to be saved, so change the name of that zone to reflect this.
			if( zoneNames.ContainsKey( 0 ) )
			{
				zoneNames[0] = Tuple.Create( "Unknown Zone", "Unknown Zone" );
			}

			//	Text Command Initialization
			mPluginInterface.CommandManager.AddHandler( mTextCommandName, new CommandInfo( ProcessTextCommand )
			{
				HelpMessage = "Performs waymark preset commands.  Use \"/pwaymark help\" for detailed usage information."
			} );

			//	UI Initialization
			mUI = new PluginUI( mConfiguration, zoneNames, mGameMemoryHandler );
			mPluginInterface.UiBuilder.OnBuildUi += DrawUI;
			mPluginInterface.UiBuilder.OnOpenConfigUi += ( sender, args ) => DrawConfigUI();

			//	Tell the user if there's something out of the ordinary.
			if( !mGameMemoryHandler.FoundAllSigs() )
			{
				mPluginInterface.Framework.Gui.Chat.Print( "Error initializing WaymarkPresetPlugin: Cannot write to or read from game." );
			}
		}

		//	Cleanup
		public void Dispose()
		{
			mUI.Dispose();
			mPluginInterface.CommandManager.RemoveHandler( mTextCommandName );
			mPluginInterface.Dispose();
		}

		//	Text Commands
		protected void ProcessTextCommand( string command, string args )
		{
			//*****TODO: Don't split, just substring off of the first space so that other stuff is preserved verbatim.
			//	Seperate into sub-command and paramters.
			string subCommand = "";
			string subCommandArgs = "";
			string[] argsArray = args.Split( ' ' );
			if( argsArray.Length > 0 )
			{
				subCommand = argsArray[0];
			}
			if( argsArray.Length > 1 )
			{
				//	Recombine because there might be spaces in JSON or something, that would make splitting it bad.
				for( int i = 1; i < argsArray.Length; ++i )
				{
					subCommandArgs += argsArray[i] + ' ';
				}
				subCommandArgs = subCommandArgs.Trim();
			}

			//	Process the commands.
			string commandResponse = "";
			if( subCommand.Length == 0 )
			{
				mUI.MainWindowVisible = true;
			}
			else if( subCommand.ToLower() == "config" )
			{
				mUI.SettingsWindowVisible = true;
			}
			else if( subCommand.ToLower() == "slotinfo" )
			{
				commandResponse = ProcessTextCommand_SlotInfo( subCommandArgs );
			}
			else
			{
				commandResponse = ProcessTextCommand_Help( subCommandArgs );
			}

			//	Send any feedback to the user.
			if( commandResponse.Length > 0 )
			{
				mPluginInterface.Framework.Gui.Chat.Print( commandResponse );
			}
		}

		public string ProcessTextCommand_Help( string args )
		{
			if( args.ToLower() == "commands" )
			{
				return "Valid commands are as follows: config, slotinfo.  If no command is provided, the GUI will be opened.  Type /pwaymark help <command> for usage information.";
			}
			else if( args.ToLower() == "config" )
			{
				return "Opens the settings window.";
			}
			else if( args.ToLower() == "slotinfo" )
			{
				return "Prints the data saved in the game's slots to the chat window.  Usage \"/pwaymark slotinfo <slot>\".  The slot number can be any valid game slot.";
			}
			else
			{
				return "Use \"/pwaymark\" to open the GUI.  Use \"/pwaymark help commands\" for a list of text commands.";
			}
		}

		public string ProcessTextCommand_SlotInfo( string args )
		{
			uint gameSlotToCopy;
			if( args.Length == 1 &&
				uint.TryParse( args, out gameSlotToCopy ) &&
				gameSlotToCopy >= 1 &&
				gameSlotToCopy <= 5 )
			{
				if( mGameMemoryHandler.FoundAllSigs() )
				{
					byte[] gamePreset = new byte[104];

					try
					{
						WaymarkPreset tempPreset = WaymarkPreset.Parse( mGameMemoryHandler.ReadSlot( gameSlotToCopy ) );
						return "Slot " + gameSlotToCopy.ToString() + " Contents:\r\n" + tempPreset.GetPresetDataString();
					}
					catch( Exception e )
					{
						PluginLog.Log( $"An unknown error occured while trying to read the game's waymark data: {e}" );
						return "An unknown error occured while trying to read the game's waymark data.";
					}
				}
				else
				{
					return "Unable to read game's waymark data.";
				}
			}
			else
			{
				return "An invalid game slot number was provided.";
			}
		}

		protected void DrawUI()
		{
			mUI.Draw();
		}

		protected void DrawConfigUI()
		{
			mUI.SettingsWindowVisible = true;
		}

		public string Name => "WaymarkPresetPlugin";
		protected const string mTextCommandName = "/pwaymark";

		protected DalamudPluginInterface mPluginInterface;
		protected Configuration mConfiguration;
		protected PluginUI mUI;
		protected MemoryHandler mGameMemoryHandler;
	}
}
