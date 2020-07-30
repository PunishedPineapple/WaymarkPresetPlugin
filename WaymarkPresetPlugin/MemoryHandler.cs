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
				IntPtr fpGetCurrentContentFinderLinkType = mPluginInterface.TargetModuleScanner.ScanText( "48 83 EC 28 48 8B 05 B5 4B 4F 01 48 85 C0 0F 84 A8 00 00 00 83 B8 30 2F 00 00 06 0F 85 9B 00 00 00" );
				if( fpGetCurrentContentFinderLinkType != IntPtr.Zero )
				{
					mdGetCurrentContentFinderLinkType = Marshal.GetDelegateForFunctionPointer<GetCurrentContentFinderLinkTypeDelegate>( fpGetCurrentContentFinderLinkType );
				}

				IntPtr fpDirectPlacePreset = mPluginInterface.TargetModuleScanner.ScanText( "E8 ?? ?? ?? ?? 84 C0 0F 94 C0 EB 19" );
				if( fpDirectPlacePreset != IntPtr.Zero )
				{
					mdDirectPlacePreset = Marshal.GetDelegateForFunctionPointer<DirectPlacePresetDelegate>( fpDirectPlacePreset );
				}

				mpPlaceRawPresetParam1Obj = mPluginInterface.TargetModuleScanner.GetStaticAddressFromSig( "41 80 F9 08 7C BB 48 8D ?? ?? ?? 48 8D ?? ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 0F 94 C0 EB 19", 11 );
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
					mpPlaceRawPresetParam1Obj != IntPtr.Zero;
		}

		public static byte[] ReadSlot( uint slotNum )
		{
			IntPtr pWaymarkData = GetGameWaymarkDataPointerForSlot( slotNum );
			byte[] data = new byte[104];
			if( pWaymarkData != IntPtr.Zero )
			{
				//	Don't catch exceptions here; better to have the caller do it probably.
				Marshal.Copy( pWaymarkData, data, 0, 104 );
			}

			return data;
		}

		public static bool WriteSlot( uint slotNum, byte[] data )
		{
			IntPtr pWaymarkData = GetGameWaymarkDataPointerForSlot( slotNum );
			if( data.Length >= 104 && pWaymarkData != IntPtr.Zero )
			{
				//	Don't catch exceptions here; better to have the caller do it probably.
				Marshal.Copy( data, 0, pWaymarkData, 104 );
				return true;
			}
			else
			{
				return false;
			}
		}

		public static IntPtr GetGameWaymarkDataPointerForSlot( uint slotNum )
		{
			if( !FoundSavedPresetSigs() || slotNum < 1 || slotNum > 5 )
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
			if( !FoundDirectPlacementSigs() ) return false;
			byte currentContentLinkType = mdGetCurrentContentFinderLinkType.Invoke();
			return	mPluginInterface != null &&
					mPluginInterface.ClientState.LocalPlayer != null &&
					mPluginInterface.ClientState.LocalPlayer.Address != IntPtr.Zero &&
					!IsCharacterInCombat() &&
					currentContentLinkType > 0 && currentContentLinkType < 4;
		}

		public static void DirectPlacePreset( byte[] presetData )
		{
			if( IsSafeToDirectPlacePreset() )
			{
				byte[] formattedData = GetPresetDataForDirectPlace( presetData );

				unsafe
				{
					fixed( byte* pFormattedData = formattedData )
					{
						mdDirectPlacePreset.Invoke( mpPlaceRawPresetParam1Obj, new IntPtr( pFormattedData ) );
					}
				}
			}
		}

		private static byte[] GetPresetDataForDirectPlace( byte[] presetData )
		{
			if( presetData.Length != 104 )
			{
				throw new Exception( "Error in GetPresetDataForDirectPlace(): Invalid length of passed preset data!" );
			}

			//	This is probably actually a struct in the game, but for various reasons (i.e., variable sizes of bools in C#), we'll just construct a byte array instead.
			byte[] newData = new byte[0xB0];	//	Go with the size of the entire local stack in the disassembled program just so we don't risk an access violation or risk accessing junk memory.

			for( int i = 0; i < 8; ++i )
			{
				newData[i] = (byte)( presetData[96] >> i & 0x1 );
				Array.Copy( presetData, i * 12 + 0, newData, i * 4 +  8, 4 );
				Array.Copy( presetData, i * 12 + 4, newData, i * 4 + 40, 4 );
				Array.Copy( presetData, i * 12 + 8, newData, i * 4 + 72, 4 );
			}

			return newData;
		}

		private static bool IsCharacterInCombat()
		{
			byte flags = 0;

			if( mPluginInterface.ClientState.LocalPlayer != null &&
				mPluginInterface.ClientState.LocalPlayer.Address != IntPtr.Zero )
			{
				try
				{
					flags = Marshal.ReadByte( new IntPtr( mPluginInterface.ClientState.LocalPlayer.Address.ToInt64() + 0x1901 ) );
				}
				catch
				{
				}
			}

			return ( flags & 2 ) > 0;
		}

		private static DalamudPluginInterface mPluginInterface;
		private static IntPtr mpPlaceRawPresetParam1Obj;

		private delegate IntPtr GetConfigSectionDelegate( IntPtr pConfigFile, byte sectionIndex );
		private delegate IntPtr GetPresetAddressForSlotDelegate( IntPtr pMarkerDataStart, uint slotNum );
		private delegate byte GetCurrentContentFinderLinkTypeDelegate();
		private delegate void DirectPlacePresetDelegate( IntPtr thisPtr, IntPtr pData );

		private static GetConfigSectionDelegate mdGetUISAVESectionAddress;
		private static GetPresetAddressForSlotDelegate mdGetPresetAddressForSlot;
		private static GetCurrentContentFinderLinkTypeDelegate mdGetCurrentContentFinderLinkType;
		private static DirectPlacePresetDelegate mdDirectPlacePreset;
	}
}
