using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Dalamud.Game.Command;
using Dalamud.Plugin;

namespace WaymarkPresetPlugin
{
	public static class MemoryHandler
	{
		public static void Init( DalamudPluginInterface pluginInterface )
		{
			if( pluginInterface == null )
			{
				throw new Exception( "Error in MemoryHandler.Init: A null plugin interface was passed!" );
			}

			//	Save this off.
			mPluginInterface = pluginInterface;

			try
			{
				//	Get Function Pointers
				IntPtr fpGetUISAVESectionAddress = mPluginInterface.TargetModuleScanner.ScanText( "40 53 48 83 EC 20 48 8B 0D ?? ?? ?? ?? 0F B7 DA" );
				if( fpGetUISAVESectionAddress != IntPtr.Zero )
				{
					mdGetUISAVESectionAddress = Marshal.GetDelegateForFunctionPointer<GetConfigSectionDelegate>( fpGetUISAVESectionAddress );
				}

				IntPtr fpGetPresetAddressForSlot = mPluginInterface.TargetModuleScanner.ScanText( "4C 8B C9 85 D2 78 0A 83 FA 08 73 05" );
				if( fpGetPresetAddressForSlot != IntPtr.Zero )
				{
					mdGetPresetAddressForSlot = Marshal.GetDelegateForFunctionPointer<GetPresetAddressForSlotDelegate>( fpGetPresetAddressForSlot );
				}

				//*****TODO: Determine actual proper sig.*****
				IntPtr fpGetCurrentContentFinderLinkType = mPluginInterface.TargetModuleScanner.ScanText( "48 83 EC 28 48 8B 05 ?? ?? ?? ?? 48 85 C0 0F 84 A8 00 00 00 83 B8 ?? 2F 00 00 06 0F 85 9B 00 00 00" );
				if( fpGetCurrentContentFinderLinkType != IntPtr.Zero )
				{
					mdGetCurrentContentFinderLinkType = Marshal.GetDelegateForFunctionPointer<GetCurrentContentFinderLinkTypeDelegate>( fpGetCurrentContentFinderLinkType );
				}

				IntPtr fpDirectPlacePreset = mPluginInterface.TargetModuleScanner.ScanText( "E8 ?? ?? ?? ?? 84 C0 0F 94 C0 EB 19" );
				if( fpDirectPlacePreset != IntPtr.Zero )
				{
					mdDirectPlacePreset = Marshal.GetDelegateForFunctionPointer<DirectPlacePresetDelegate>( fpDirectPlacePreset );
				}

				IntPtr fpGetCurrentWaymarkData = mPluginInterface.TargetModuleScanner.ScanText( "48 89 ?? ?? ?? 57 48 83 ?? ?? 48 8B ?? 48 8B ?? 33 D2 48 8B" );
				if( fpGetCurrentWaymarkData != IntPtr.Zero )
				{
					mdGetCurrentWaymarkData = Marshal.GetDelegateForFunctionPointer<GetCurrentWaymarkDataDelegate>( fpGetCurrentWaymarkData );
				}

				mpWaymarksObj = mPluginInterface.TargetModuleScanner.GetStaticAddressFromSig( "41 80 F9 08 7C BB 48 8D ?? ?? ?? 48 8D ?? ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 0F 94 C0 EB 19", 11 );

				//	Write this address to log to help with digging around in memory if we need to.
				PluginLog.LogInformation( $"Waymarks object address: 0x{mpWaymarksObj.ToString( "X" )}" );
			}
			catch( Exception e )
			{
				throw new Exception( $"Error in MemoryHandler.Init: Unable to find all required function signatures; this probably means that the plugin needs to be updated due to changes in Final Fantasy XIV.  Raw exception as follows:\r\n{e}" );
			}
		}

		public static void Uninit()
		{
			mPluginInterface = null;
		}

		public static bool FoundSavedPresetSigs()
		{
			return	mdGetUISAVESectionAddress != null &&
					mdGetPresetAddressForSlot != null;
		}

		public static bool FoundDirectPlacementSigs()
		{
			return	mdGetCurrentContentFinderLinkType != null &&
					mdDirectPlacePreset != null &&
					mpWaymarksObj != IntPtr.Zero;
		}

		public static bool FoundDirectSaveSigs()
		{
			return	mdGetCurrentWaymarkData != null &&
					mpWaymarksObj != IntPtr.Zero;
		}

		public static GamePreset ReadSlot( uint slotNum )
		{
			IntPtr pWaymarkData = GetGameWaymarkDataPointerForSlot( slotNum );
			GamePreset preset = new GamePreset();
			if( pWaymarkData != IntPtr.Zero )
			{
				//	Don't catch exceptions here; better to have the caller do it probably.
				lock( mPresetMemoryLockObject ) preset = (GamePreset)Marshal.PtrToStructure( pWaymarkData, typeof( GamePreset ) );
			}
			else
			{
				throw new ArgumentOutOfRangeException( $"Error in \"WaymarkPresetPlugin.MemoryHandler.ReadSlot()\": Slot number ({slotNum}) was either invalid, or pointer for valid slot number could not be located." );
			}

			return preset;
		}

		public static bool WriteSlot( uint slotNum, GamePreset preset )
		{
			IntPtr pWaymarkData = GetGameWaymarkDataPointerForSlot( slotNum );
			if( pWaymarkData != IntPtr.Zero )
			{
				//	Don't catch exceptions here; better to have the caller do it probably.
				lock( mPresetMemoryLockObject ) Marshal.StructureToPtr( preset, pWaymarkData, false );
				return true;
			}
			else
			{
				return false;
			}
		}

		public static IntPtr GetGameWaymarkDataPointerForSlot( uint slotNum )
		{
			if( !FoundSavedPresetSigs() || slotNum < 1 || slotNum > MaxPresetSlotNum )
			{
				return IntPtr.Zero;
			}
			else
			{
				IntPtr pWaymarksLocation = mdGetUISAVESectionAddress.Invoke( IntPtr.Zero, 0x11 );

				if( pWaymarksLocation != IntPtr.Zero )
				{
					pWaymarksLocation = mdGetPresetAddressForSlot.Invoke( pWaymarksLocation, slotNum - 1 );
				}

				return pWaymarksLocation;
			}
		}

		public static bool IsSafeToDirectPlacePreset()
		{
			//	Basically impose all of the same conditions that the game does, but without checking the preset's zone ID.
			/*if( !FoundDirectPlacementSigs() ) return false;
			byte currentContentLinkType = mdGetCurrentContentFinderLinkType.Invoke();
			return	mPluginInterface != null &&
					mPluginInterface.ClientState.LocalPlayer != null &&
					mPluginInterface.ClientState.LocalPlayer.Address != IntPtr.Zero &&
					!IsCharacterInCombat() &&
					currentContentLinkType > 0 && currentContentLinkType < 4;*/
			return true;    //***** TODO: Revert. *****
		}

		public static void DirectPlacePreset( GamePreset preset )
		{
			if( IsSafeToDirectPlacePreset() )
			{
				GamePreset_Placement placementStruct = new GamePreset_Placement( preset );
				unsafe
				{
					mdDirectPlacePreset.Invoke( mpWaymarksObj, new IntPtr( &placementStruct ) );
				}
			}
		}

		public static bool GetCurrentWaymarksAsPresetData( ref GamePreset rPresetData )
		{
			if( mPluginInterface != null && FoundDirectSaveSigs() )
			{
				byte currentContentLinkType = mdGetCurrentContentFinderLinkType.Invoke();
				if( true /*currentContentLinkType > 0 && currentContentLinkType < 4*/ ) //***** TODO: Revert. *****
				{
					GamePreset_Placement rawWaymarkData = new GamePreset_Placement();
					unsafe
					{
						mdGetCurrentWaymarkData.Invoke( mpWaymarksObj, new IntPtr( &rawWaymarkData ) );
					}

					rPresetData = new GamePreset( rawWaymarkData );
					rPresetData.ContentFinderConditionID = ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( mPluginInterface.ClientState.TerritoryType );
					rPresetData.UnixTime = (Int32)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
					return true;
				}
			}

			return false;
		}

		private static bool IsCharacterInCombat()
		{
			if( mPluginInterface.ClientState.LocalPlayer != null &&
				mPluginInterface.ClientState.LocalPlayer.Address != IntPtr.Zero )
			{
				try
				{
					byte flags = Marshal.ReadByte( new IntPtr( mPluginInterface.ClientState.LocalPlayer.Address.ToInt64() + CharacterStructCombatFlagsOffset.ToInt64() ) );
					return ( flags & 2 ) > 0;
				}
				catch
				{
					//	Default to assuming in-combat for safety.
					return true;
				}
			}

			//	Default to assuming in-combat for safety.
			return true;
		}

		//*****TODO: Only need this if we want to be able to set the offset via config instead of rebuilding for each new game version, although the offset within the object is unlikely to frequently change.*****
		public static void SetClientSideWaymarksOffset( IntPtr offset )
		{
			mClientSideWaymarksOffset = offset;
		}

		/*public static void PlacePreset_ClientSide( GamePreset presetData )
		{
			//	Get the data into the format used by the game's waymark struct.
			IntPtr pClientSideWaymarks = new IntPtr( mpWaymarksObj.ToInt64() + mClientSideWaymarksOffset.ToInt64() );
			byte[] formattedData = GetWaymarkDataForClientSidePlace( presetData );

			//	Copy it in.
			Marshal.Copy( formattedData, 0, pClientSideWaymarks, formattedData.Length );
		}

		public static bool GetCurrentWaymarksAsPresetData_ClientSide( ref GamePreset presetData )
		{

		}*/

		public static readonly int MaxPresetSlotNum = 5;
		private static readonly IntPtr CharacterStructCombatFlagsOffset = new IntPtr( 0x1980 );

		private static DalamudPluginInterface mPluginInterface;
		private static IntPtr mpWaymarksObj;
		private static IntPtr mClientSideWaymarksOffset;

		private delegate IntPtr GetConfigSectionDelegate( IntPtr pConfigFile, byte sectionIndex );
		private delegate IntPtr GetPresetAddressForSlotDelegate( IntPtr pMarkerDataStart, uint slotNum );
		private delegate byte GetCurrentContentFinderLinkTypeDelegate();
		private delegate void DirectPlacePresetDelegate( IntPtr pObj, IntPtr pData );
		private delegate void GetCurrentWaymarkDataDelegate( IntPtr pObj, IntPtr pData );

		//	***** TODO: Testing *****
		//private delegate IntPtr GetIsInCombatDelegate();
		//private static GetIsInCombatDelegate mdGetIsInCombat;

		private static GetConfigSectionDelegate mdGetUISAVESectionAddress;
		private static GetPresetAddressForSlotDelegate mdGetPresetAddressForSlot;
		private static GetCurrentContentFinderLinkTypeDelegate mdGetCurrentContentFinderLinkType;
		private static DirectPlacePresetDelegate mdDirectPlacePreset;
		private static GetCurrentWaymarkDataDelegate mdGetCurrentWaymarkData;

		private static readonly object mPresetMemoryLockObject = new object();
	}
}