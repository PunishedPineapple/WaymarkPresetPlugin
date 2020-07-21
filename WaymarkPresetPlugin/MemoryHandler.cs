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
	public class MemoryHandler
	{
		public MemoryHandler( DalamudPluginInterface pluginInterface )
		{
			//	Get Function Pointers
			var getConfigFileAddress = pluginInterface.TargetModuleScanner.ScanText( "E8 ?? ?? ?? ?? 48 85 C0 74 14 83 7B 44 00" );
			if( getConfigFileAddress != IntPtr.Zero )
			{
				mfpGetConfigFile = Marshal.GetDelegateForFunctionPointer<GetConfigFileDelegate>( getConfigFileAddress );
			}
			var getConfigSectionAddress = pluginInterface.TargetModuleScanner.ScanText( "40 53 48 83 EC 20 48 8B 0D ?? ?? ?? ?? 0F B7 DA" );
			if( getConfigSectionAddress != IntPtr.Zero )
			{
				mfpGetConfigSection = Marshal.GetDelegateForFunctionPointer<GetConfigSectionDelegate>( getConfigSectionAddress );
			}
			var getPresetAddressForSlot = pluginInterface.TargetModuleScanner.ScanText( "4C 8B C9 85 D2 78 0A 83 FA 08 73 05" );
			if( getPresetAddressForSlot != IntPtr.Zero )
			{
				mfpGetPresetAddressForSlot = Marshal.GetDelegateForFunctionPointer<GetPresetAddressForSlotDelegate>( getPresetAddressForSlot );
			}
		}

		public bool FoundAllSigs()
		{
			return	mfpGetConfigFile != null &&
					mfpGetConfigSection != null &&
					mfpGetPresetAddressForSlot != null;
		}

		public byte[] ReadSlot( uint slotNum )
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

		public bool WriteSlot( uint slotNum, byte[] data )
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

		public IntPtr GetGameWaymarkDataPointerForSlot( uint slotNum )
		{
			if( !FoundAllSigs() || slotNum < 1 || slotNum > 5 )
			{
				return IntPtr.Zero;
			}
			else
			{
				IntPtr pWaymarksLocation = mfpGetConfigSection.Invoke( mfpGetConfigFile.Invoke( 0x9 ), 0x11 );

				if( pWaymarksLocation != IntPtr.Zero )
				{
					pWaymarksLocation = mfpGetPresetAddressForSlot( pWaymarksLocation, slotNum - 1 );
				}

				return pWaymarksLocation;
			}
		}

		public IntPtr GetAlternateWaymarkDataPointer( uint slotNum )
		{
			return mfpGetPresetAddressForSlot != null ? mfpGetPresetAddressForSlot.Invoke( mfpGetConfigSection.Invoke( mfpGetConfigFile.Invoke( 0x9 ), 0x11 ), slotNum ) : IntPtr.Zero;
		}

		protected delegate IntPtr GetConfigFileDelegate( byte fileIndex );
		protected delegate IntPtr GetConfigSectionDelegate( IntPtr pConfigFile, byte sectionIndex );
		protected delegate IntPtr GetPresetAddressForSlotDelegate( IntPtr pMarkerDataStart, uint slotNum );

		protected static GetConfigFileDelegate mfpGetConfigFile;
		protected static GetConfigSectionDelegate mfpGetConfigSection;
		protected static GetPresetAddressForSlotDelegate mfpGetPresetAddressForSlot;
	}
}
