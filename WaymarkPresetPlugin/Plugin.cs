using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace WaymarkPresetPlugin
{
	public class Plugin : IDalamudPlugin
	{
		protected delegate IntPtr GetConfigFileDelegate( byte fileIndex );
		protected delegate IntPtr GetConfigSectionDelegate( IntPtr pConfigFile, byte sectionIndex );

		protected static GetConfigFileDelegate mfpGetConfigFile;
		protected static GetConfigSectionDelegate mfpGetConfigSection;

		public string Name => "WaymarkPresetPlugin";
		private const string mTextCommandName = "/pwaymark";
		protected IntPtr mpWaymarkMemory = IntPtr.Zero;

		private DalamudPluginInterface pi;
		private Configuration configuration;
		private PluginUI ui;

		public void Initialize( DalamudPluginInterface pluginInterface )
		{
			this.pi = pluginInterface;
			this.configuration = this.pi.GetPluginConfig() as Configuration ?? new Configuration();
			this.configuration.Initialize( this.pi );

			//	No idea if this is how this actually works.  First thing to test would be to read the mem and see if you can copy a slot from it to a preset.
			//mpWaymarkMemory = pi.TargetModuleScanner.ScanText( "46 4D 41 52 4B 45 52 2E 44 41 54" );	//FMARKER.DAT

			var getConfigFileAddress = pi.TargetModuleScanner.ScanText( "E8 ?? ?? ?? ?? 48 85 C0 74 14 83 7B 44 00" );
			if( getConfigFileAddress != IntPtr.Zero )
			{
				mfpGetConfigFile = Marshal.GetDelegateForFunctionPointer<GetConfigFileDelegate>( getConfigFileAddress );
			}
			var getConfigSectionAddress = pi.TargetModuleScanner.ScanText( "40 53 48 83 EC 20 48 8B 0D ?? ?? ?? ?? 0F B7 DA" );
			if( getConfigSectionAddress != IntPtr.Zero )
			{
				mfpGetConfigSection = Marshal.GetDelegateForFunctionPointer<GetConfigSectionDelegate>( getConfigSectionAddress );
			}

			// you might normally want to embed resources and load them from the manifest stream
			//var imagePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"goat.png");
			var goatImage = this.pi.UiBuilder.LoadImage("Z:\\goat.png");
			this.ui = new PluginUI( this.configuration, goatImage );

			SetupTextCommands();

			this.pi.UiBuilder.OnBuildUi += DrawUI;
			this.pi.UiBuilder.OnOpenConfigUi += ( sender, args ) => DrawConfigUI();
		}

		public void Dispose()
		{
			this.ui.Dispose();
			this.pi.CommandManager.RemoveHandler( mTextCommandName );
			this.pi.Dispose();
		}

		protected void SetupTextCommands()
		{
			this.pi.CommandManager.AddHandler( mTextCommandName, new CommandInfo( ProcessTextCommand )
			{
				HelpMessage = "Performs waymark preset commands.  Use \"/pwaymark help\" for detailed usage information."
			} );
		}

		protected void ProcessTextCommand( string command, string args )
		{
			//	Handle the help command.
			if( args.Trim().StartsWith( "help" ) )
			{
				pi.Framework.Gui.Chat.Print( "Command format: \"/pwaymark [help|import] <parameter(s)>\"" );
				pi.Framework.Gui.Chat.Print( "\"import\" valid parameters: \"1\"-\"5\" (corresponding to the game's build in preset slots), or a properly-formatted JSON string." );
				pi.Framework.Gui.Chat.Print( "\"/pwaymark\" without any commands or parameters opens the GUI." );
				//*****TODO: Add export and copy commands.*****
			}
			else if( args.Trim().StartsWith( "save" ) )
			{
				configuration.Save();
				pi.Framework.Gui.Chat.Print( "Saved Config" );
			}
			//	Handle the temp load command.
			else if( args.Trim().StartsWith( "loggamedata" ) )
			{
				mpWaymarkMemory = mfpGetConfigSection.Invoke( mfpGetConfigFile.Invoke( 0x9 ), 0x11 );
				mpWaymarkMemory = new IntPtr( mpWaymarkMemory.ToInt64() + 64L );
				pi.Framework.Gui.Chat.Print( "Found Address: 0x" + mpWaymarkMemory.ToString( "X" ) );
				StreamWriter log = File.AppendText( "Z:\\WaymarkPresetPlugin.log" );
				
				log.WriteLine( "Found Address 0x" + mpWaymarkMemory.ToString( "X" ) );
				byte[] data = new byte[536];
				if( mpWaymarkMemory != IntPtr.Zero )
				{
					Marshal.Copy( mpWaymarkMemory, data, 0, 536 );
					log.WriteLine( "Data: " );
					foreach( byte b in data )
					{
						log.Write( b.ToString( "X2" ) + " " );
					}
					log.WriteLine();
				}
				log.Close();
			}
			else if( args.Trim().StartsWith( "gamedataexport" ) )
			{
				byte[] gamePreset1 = new byte[104];
				mpWaymarkMemory = mfpGetConfigSection.Invoke( mfpGetConfigFile.Invoke( 0x9 ), 0x11 );
				mpWaymarkMemory = new IntPtr( mpWaymarkMemory.ToInt64() + 64L );
				Marshal.Copy( mpWaymarkMemory, gamePreset1, 0, 104 );
				WaymarkPreset tempPreset = WaymarkPreset.Parse( gamePreset1 );
				
				pi.Framework.Gui.Chat.Print( "Game Slot 1 Contents:\r\n" + tempPreset.GetPresetDataString() );
			}
			else if( args.Trim().StartsWith( "gamedataimport" ) )
			{
				byte[] gamePreset1 = configuration.PresetLibrary.Presets[0].ConstructGamePreset();
				if( gamePreset1.Length == 104 )
				{
					mpWaymarkMemory = mfpGetConfigSection.Invoke( mfpGetConfigFile.Invoke( 0x9 ), 0x11 );
					mpWaymarkMemory = new IntPtr( mpWaymarkMemory.ToInt64() + 64L );
					Marshal.Copy( gamePreset1, 0, mpWaymarkMemory, 104 );
					pi.Framework.Gui.Chat.Print( "Game Slot 1 Updated." );
				}
				else
				{
					pi.Framework.Gui.Chat.Print( "Improper game data size; not copied to game." );
				}
			}
			//	Handle the import command.
			else if( args.Trim().StartsWith( "import" ) )
			{
				string commandParameters = args.Trim().Substring( 6 ).Trim();
				if( commandParameters.Length < 1 )
				{
					pi.Framework.Gui.Chat.Print( "No parameters were specified for the import command.  Use \"/pwaymark help\" for proper command format." );
				}
				else if( commandParameters.Length == 1 )
				{
					int gameSlotToCopy;
					if( int.TryParse( commandParameters, out gameSlotToCopy ) && gameSlotToCopy >= 1 && gameSlotToCopy <= 5 )
					{
						//*****TODO: Actually copy game slot to library.*****
						//pi.Framework.Gui.Chat.Print( "Waymark preset imported." );
					}
					else
					{
						pi.Framework.Gui.Chat.Print( "Invalid waymark slot number passed to import command.  Only the numbers 1-5 are valid slots." );
					}
				}
				else
				{
					if( configuration.PresetLibrary.ImportPreset( commandParameters ) )
					{
						pi.Framework.Gui.Chat.Print( "Waymark preset imported." );
					}
					else
					{
						pi.Framework.Gui.Chat.Print( "Invalid JSON passed to import command, unable to import." );
					}
				}
			}
			//	Handle the export command.
			else if( args.Trim().StartsWith( "export" ) )
			{
				string commandParameters = args.Trim().Substring( 6 ).Trim();
				int slotIndex;
				if( commandParameters.Length > 0 && int.TryParse( commandParameters, out slotIndex ) && slotIndex >= 0 && slotIndex < configuration.PresetLibrary.Presets.Count )
				{
					pi.Framework.Gui.Chat.Print( configuration.PresetLibrary.ExportPreset( slotIndex ) );
				}
				else
				{
					pi.Framework.Gui.Chat.Print( "No or invalid parameters were specified for the export command.  Use \"/pwaymark help\" for proper command format." );
				}
			}
			//	Default is just show the GUI.
			else
			{
				pi.Framework.Gui.Chat.Print( "Unrecognized command.  Use \"/pwaymark help\" for a list of valid commands." );
				//this.ui.Visible = true;
			}

			//*****TODO: Process params.
			//  Process either game slot number or JSON if import.
			//  List detailed usage with "help" as a param.

			// in response to the slash command, just display our main ui
			//this.ui.Visible = true;
		}

		private void DrawUI()
		{
			this.ui.Draw();
		}

		private void DrawConfigUI()
		{
			this.ui.SettingsVisible = true;
		}
	}
}
