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

			//	Get the offset from the function's returned pointer to the actual marker data.
			MarkerDataAdditionalOffset = 64L;
		}

		public byte[] ReadSlot( int slotNum )
		{
			IntPtr pWaymarkData = GetGameWaymarkDataPointer();
			byte[] data = new byte[104];
			if( slotNum >= 1 &&
				slotNum <= 5 &&
				pWaymarkData != IntPtr.Zero )
			{
				//	Don't catch exceptions here; better to have the caller do it probably.
				Marshal.Copy( new IntPtr( pWaymarkData.ToInt64() + ( slotNum - 1 ) * 104 ), data, 0, 104 );
			}

			return data;
		}

		public bool WriteSlot( int slotNum, byte[] data )
		{
			IntPtr pWaymarkData = GetGameWaymarkDataPointer();
			if( slotNum >= 1 &&
				slotNum <= 5 &&
				data.Length >= 104 &&
				pWaymarkData != IntPtr.Zero )
			{
				//	Don't catch exceptions here; better to have the caller do it probably.
				Marshal.Copy( data, 0, new IntPtr( pWaymarkData.ToInt64() + ( slotNum - 1 ) * 104 ), 104 );
				return true;
			}
			else
			{
				return false;
			}
		}

		public IntPtr GetGameWaymarkDataPointer()
		{
			if( mfpGetConfigFile == null || mfpGetConfigSection == null )
			{
				return IntPtr.Zero;
			}
			else
			{
				IntPtr pWaymarksLocation = mfpGetConfigSection.Invoke( mfpGetConfigFile.Invoke( 0x9 ), 0x11 );

				if( pWaymarksLocation != IntPtr.Zero )
				{
					pWaymarksLocation = new IntPtr( pWaymarksLocation.ToInt64() + MarkerDataAdditionalOffset );
				}

				return pWaymarksLocation;
			}
		}

		protected delegate IntPtr GetConfigFileDelegate( byte fileIndex );
		protected delegate IntPtr GetConfigSectionDelegate( IntPtr pConfigFile, byte sectionIndex );

		protected static GetConfigFileDelegate mfpGetConfigFile;
		protected static GetConfigSectionDelegate mfpGetConfigSection;

		protected Int64 MarkerDataAdditionalOffset { get; set; }
	}
}
