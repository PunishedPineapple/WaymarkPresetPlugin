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
			//	Get Function Pointers
			IntPtr fpGetUISAVESectionAddress = pluginInterface.TargetModuleScanner.ScanText( "40 53 48 83 EC 20 48 8B 0D ?? ?? ?? ?? 0F B7 DA" );
			if( fpGetUISAVESectionAddress != IntPtr.Zero )
			{
				mdGetUISAVESectionAddress = Marshal.GetDelegateForFunctionPointer<GetConfigSectionDelegate>( fpGetUISAVESectionAddress );
			}

			IntPtr fpGetPresetAddressForSlot = pluginInterface.TargetModuleScanner.ScanText( "4C 8B C9 85 D2 78 0A 83 FA 08 73 05" );
			if( fpGetPresetAddressForSlot != IntPtr.Zero )
			{
				mdGetPresetAddressForSlot = Marshal.GetDelegateForFunctionPointer<GetPresetAddressForSlotDelegate>( fpGetPresetAddressForSlot );
			}
		}

		public static bool FoundSavedPresetSigs()
		{
			return	mdGetUISAVESectionAddress != null &&
					mdGetPresetAddressForSlot != null;
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

		private delegate IntPtr GetConfigSectionDelegate( IntPtr pConfigFile, byte sectionIndex );
		private delegate IntPtr GetPresetAddressForSlotDelegate( IntPtr pMarkerDataStart, uint slotNum );
		private delegate byte GetCurrentContentLinkTypeDelegate();
		private delegate uint GetCurrentZoneDelegate();

		private static GetConfigSectionDelegate mdGetUISAVESectionAddress;
		private static GetPresetAddressForSlotDelegate mdGetPresetAddressForSlot;
	}
}
