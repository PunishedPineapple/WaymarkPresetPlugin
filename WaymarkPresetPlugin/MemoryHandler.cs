using System;
using System.Runtime.InteropServices;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState;
using Dalamud.Game;
using Dalamud.Logging;

namespace WaymarkPresetPlugin
{
	public static class MemoryHandler
	{
		public static void Init( SigScanner sigScanner, ClientState clientState, Condition condition )
		{
			if( sigScanner == null )
			{
				throw new Exception( "Error in \"MemoryHandler.Init()\": A null SigScanner was passed!" );
			}

			if( clientState == null )
			{
				throw new Exception( "Error in \"MemoryHandler.Init()\": A null ClientState was passed!" );
			}

			if( condition == null )
			{
				throw new Exception( "Error in \"MemoryHandler.Init()\": A null Condition was passed!" );
			}

			//	Save off the client state and condition access.
			mClientState = clientState;
			mCondition = condition;

			//	Get Function Pointers, etc.
			try
			{
				IntPtr fpGetUISAVESectionAddress = sigScanner.ScanText( "40 53 48 83 EC 20 48 8B 0D ?? ?? ?? ?? 0F B7 DA" );
				if( fpGetUISAVESectionAddress != IntPtr.Zero )
				{
					mdGetUISAVESectionAddress = Marshal.GetDelegateForFunctionPointer<GetConfigSectionDelegate>( fpGetUISAVESectionAddress );
				}

				//	Write this address to log to help with digging around in memory if we need to.
				if( mdGetUISAVESectionAddress != null )
				{
					PluginLog.LogInformation( $"FMARKER.DAT address: 0x{mdGetUISAVESectionAddress.Invoke( IntPtr.Zero, mFMARKERDATIndex ):X}" );
				}

				IntPtr fpGetPresetAddressForSlot = sigScanner.ScanText( "4C 8B C9 85 D2 78 0A 83 FA 08 73 05" );
				if( fpGetPresetAddressForSlot != IntPtr.Zero )
				{
					mdGetPresetAddressForSlot = Marshal.GetDelegateForFunctionPointer<GetPresetAddressForSlotDelegate>( fpGetPresetAddressForSlot );
				}

				//*****TODO:	Ideally we would check the size of the FMARKER.DAT section against the expected number of presets and the struct size, and
				//				warn the user if it doesn't all line up (or maybe only if it's not divisible?), but it doesn't appear to store the size of
				//				the config section like it does in UISAVE.DAT, so this may not be feasible.  We could consider checking the difference between
				//				the pointer to this section and the next section, but that seems a bit unreliable.*****
			}
			catch( Exception e )
			{
				throw new Exception( $"Error in \"MemoryHandler.Init()\" while searching for required function signatures; this probably means that the plugin needs to be updated due to changes in Final Fantasy XIV.  Raw exception as follows:\r\n{e}" );
			}

			try
			{
				IntPtr fpGetCurrentContentFinderLinkType = sigScanner.ScanText( "48 83 ?? ?? 48 8B ?? ?? ?? ?? ?? 48 85 ?? 0F ?? ?? ?? ?? ?? ?? B8 ?? ?? ?? ?? ?? 0F ?? ?? ?? ?? ?? ?? 89" );
				if( fpGetCurrentContentFinderLinkType != IntPtr.Zero )
				{
					mdGetCurrentContentFinderLinkType = Marshal.GetDelegateForFunctionPointer<GetCurrentContentFinderLinkTypeDelegate>( fpGetCurrentContentFinderLinkType );
				}

				IntPtr fpDirectPlacePreset = sigScanner.ScanText( "E8 ?? ?? ?? ?? 84 C0 0F 94 C0 EB 19" );
				if( fpDirectPlacePreset != IntPtr.Zero )
				{
					mdDirectPlacePreset = Marshal.GetDelegateForFunctionPointer<DirectPlacePresetDelegate>( fpDirectPlacePreset );
				}

				IntPtr fpGetCurrentWaymarkData = sigScanner.ScanText( "48 89 ?? ?? ?? 57 48 83 ?? ?? 48 8B ?? 48 8B ?? 33 D2 48 8B" );
				if( fpGetCurrentWaymarkData != IntPtr.Zero )
				{
					mdGetCurrentWaymarkData = Marshal.GetDelegateForFunctionPointer<GetCurrentWaymarkDataDelegate>( fpGetCurrentWaymarkData );
				}

				mpWaymarksObj = sigScanner.GetStaticAddressFromSig( "41 80 F9 08 7C BB 48 8D ?? ?? ?? 48 8D ?? ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 0F 94 C0 EB 19", 11 );

				//	Write this address to log to help with digging around in memory if we need to.
				PluginLog.LogInformation( $"Waymarks object address: 0x{mpWaymarksObj:X}" );
			}
			catch( Exception e )
			{
				PluginLog.LogWarning( $"Error in \"MemoryHandler.Init()\" while searching for \"optional\" function signatures;  this probably means that the plugin needs to be updated due to changes in FFXIV.  Raw exception as follows:\r\n{e}" );
			}
		}

		public static void Uninit()
		{
			mClientState						= null;
			mCondition							= null;
			mpWaymarksObj						= IntPtr.Zero;
			mdGetUISAVESectionAddress			= null;
			mdGetPresetAddressForSlot			= null;
			mdGetCurrentContentFinderLinkType	= null;
			mdDirectPlacePreset					= null;
			mdGetCurrentWaymarkData				= null;
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

		public static bool FoundClientPlaceSigs()
		{
			return	mdGetCurrentContentFinderLinkType != null &&
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
				PluginLog.LogDebug( $"Read game preset in slot {slotNum} with data:\r\n{preset}" );
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
				PluginLog.LogDebug( $"Attempting to write slot {slotNum} with data:\r\n{preset}" );

				//	Don't catch exceptions here; better to have the caller do it probably.
				lock( mPresetMemoryLockObject ) Marshal.StructureToPtr( preset, pWaymarkData, false );
				return true;
			}
			else
			{
				PluginLog.LogWarning( $"Error in MemoryHandler.WriteSlot: Unable to obtain pointer to slot {slotNum}!" );
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
				IntPtr pWaymarksLocation = mdGetUISAVESectionAddress.Invoke( IntPtr.Zero, mFMARKERDATIndex );

				if( pWaymarksLocation != IntPtr.Zero )
				{
					pWaymarksLocation = mdGetPresetAddressForSlot.Invoke( pWaymarksLocation, slotNum - 1 );
				}

				return pWaymarksLocation;
			}
		}

		private static bool IsSafeToDirectPlacePreset()
		{
			//	Basically impose all of the same conditions that the game does, but without checking the preset's zone ID.
			if( !FoundDirectPlacementSigs() ) return false;
			byte currentContentLinkType = mdGetCurrentContentFinderLinkType.Invoke();
			return	mCondition != null &&
					mClientState != null &&
					mClientState.LocalPlayer != null &&
					!mCondition[ConditionFlag.InCombat] &&
					currentContentLinkType > 0 && currentContentLinkType < 4;
		}

		public static void PlacePreset( GamePreset preset, bool allowClientSide = false )
		{
			if( allowClientSide && InOverworldZone() )
			{
				PlacePreset_ClientSide( preset );
			}
			else
			{
				DirectPlacePreset( preset );
			}
		}

		private static void DirectPlacePreset( GamePreset preset )
		{
			if( IsSafeToDirectPlacePreset() )
			{
				GamePreset_Placement placementStruct = new GamePreset_Placement( preset );
				PluginLog.LogDebug( $"Attempting to place waymark preset with data:\r\n{placementStruct}" );
				unsafe
				{
					mdDirectPlacePreset.Invoke( mpWaymarksObj, new IntPtr( &placementStruct ) );
				}
			}
		}

		public static bool GetCurrentWaymarksAsPresetData( ref GamePreset rPresetData )
		{
			if( mClientState != null && FoundDirectSaveSigs() )
			{
				byte currentContentLinkType = mdGetCurrentContentFinderLinkType.Invoke();
				if( currentContentLinkType >= 0 && currentContentLinkType < 4 )	//	Same as the game check, but let it do overworld maps too.
				{
					GamePreset_Placement rawWaymarkData = new GamePreset_Placement();
					unsafe
					{
						mdGetCurrentWaymarkData.Invoke( mpWaymarksObj, new IntPtr( &rawWaymarkData ) );
					}

					rPresetData = new GamePreset( rawWaymarkData );
					rPresetData.ContentFinderConditionID = ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( mClientState.TerritoryType );	//*****TODO: How do we get this as a territory type for non-instanced zones? The return type might need to be changed, or pass in another ref paramter or something. *****
					rPresetData.UnixTime = (Int32)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

					PluginLog.LogDebug( $"Obtained current waymarks with the following data:\r\n" +
										$"Territory: {mClientState.TerritoryType}\r\n" +
										$"ContentFinderCondition: {rPresetData.ContentFinderConditionID}\r\n" +
										$"Waymark Struct:\r\n{rawWaymarkData}" );
					return true;
				}
				else
				{
					PluginLog.LogWarning( $"Error in MemoryHandler.GetCurrentWaymarksAsPresetData: Disallowed ContentLinkType: {currentContentLinkType}" );
				}
			}
			else
			{
				PluginLog.LogWarning( $"Error in MemoryHandler.GetCurrentWaymarksAsPresetData: Missing sigs or null ClientState." );
			}

			return false;
		}

		private static bool InOverworldZone()
		{
			return	mdGetCurrentContentFinderLinkType != null &&
					mdGetCurrentContentFinderLinkType.Invoke() == 0;
		}

		//*****TODO: Only need this if we want to be able to set the offset via config instead of rebuilding for each new game version, although the offset within the object is unlikely to frequently change.*****
		public static void SetClientSideWaymarksOffset( IntPtr offset )
		{
			mClientSideWaymarksOffset = offset;
		}

		//	Only allow client-side placement if we're in a valid content type (i.e., only allow in overworld zones).  SE has made it pretty clear that they don't
		//	want waymarks changing in battle instances, so let's try to not poke the bear too much, assuming that they can even understand nuance or would see this...
		private static bool IsSafeToClientPlace()
		{
			return	FoundClientPlaceSigs() &&
					mdGetCurrentContentFinderLinkType.Invoke() == 0;
		}

		private static void PlacePreset_ClientSide( GamePreset preset )
		{
			//	Check whether we shouldn't be doing this.
			if( !IsSafeToClientPlace() ) return;

			//	Find where we will be overwriting the waymarks.
			IntPtr pClientSideWaymarks = new IntPtr( mpWaymarksObj.ToInt64() + mClientSideWaymarksOffset.ToInt64() );

			//*****TODO: Should we instead read in the extant data and only overwrite the floats?
			//GameWaymarks waymarkData = (GameWaymarks)Marshal.PtrToStructure( pClientSideWaymarks, typeof( GameWaymarks ) );
			//Write float coords and flags and send back out.
			//Marshal.StructureToPtr( waymarkData, pClientSideWaymarks, false );

			//	Do the actual writing.
			Marshal.StructureToPtr( new GameWaymarks( preset ), pClientSideWaymarks, false );
		}

		//	Magic Numbers
		public static readonly int MaxPresetSlotNum = 5;
		private static readonly byte mFMARKERDATIndex = 0x11;
		private static IntPtr mClientSideWaymarksOffset = new IntPtr( 0x1B0 );  //*****TODO: Feels bad initializing this with a magic number.  Not sure best thing to do.*****

		//	Misc.
		private static ClientState mClientState;
		private static Condition mCondition;
		private static IntPtr mpWaymarksObj;

		//	Delgates
		private delegate IntPtr GetConfigSectionDelegate( IntPtr pConfigFile, byte sectionIndex );
		private delegate IntPtr GetPresetAddressForSlotDelegate( IntPtr pMarkerDataStart, uint slotNum );
		private delegate byte GetCurrentContentFinderLinkTypeDelegate();
		private delegate void DirectPlacePresetDelegate( IntPtr pObj, IntPtr pData );
		private delegate void GetCurrentWaymarkDataDelegate( IntPtr pObj, IntPtr pData );

		private static GetConfigSectionDelegate mdGetUISAVESectionAddress;
		private static GetPresetAddressForSlotDelegate mdGetPresetAddressForSlot;
		private static GetCurrentContentFinderLinkTypeDelegate mdGetCurrentContentFinderLinkType;
		private static DirectPlacePresetDelegate mdDirectPlacePreset;
		private static GetCurrentWaymarkDataDelegate mdGetCurrentWaymarkData;

		private static readonly object mPresetMemoryLockObject = new object();
	}
}