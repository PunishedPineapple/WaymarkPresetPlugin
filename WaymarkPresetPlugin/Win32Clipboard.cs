﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WaymarkPresetPlugin
{
	//	This is just enough of an implementation to do what we need.  The C# options require loading in WinForms or WPF dependencies, and that seems to cause problems sometimes.
	public static class Win32Clipboard
	{
		public static void CopyTextToClipboard( string str )
		{
			if( OpenClipboard( Process.GetCurrentProcess().MainWindowHandle ) )
			{
				if( EmptyClipboard() )
				{
					//	Copy the string to a byte array.  We have to null-terminate this ourselves.
					byte[] bytes = GetNullTerminatedByteArrayFromString( str );

					//	Allocate the moveable memory required by the clipboard, and copy the data to the clipboard if everything went ok.
					bool copiedToClipboardSuccessfully = false;
					IntPtr hNewMem = GlobalAlloc( GMEM_MOVEABLE, new UIntPtr( (uint)bytes.Length ) );
					if( hNewMem != IntPtr.Zero )
					{
						IntPtr pNewMem = GlobalLock( hNewMem );
						if( pNewMem != IntPtr.Zero )
						{
							//	Ensure that we unlock no matter what happens.
							try
							{
								Marshal.Copy( bytes, 0, pNewMem, bytes.Length );
							}
							catch
							{
							}
							finally
							{
								GlobalUnlock( hNewMem );
							}
							copiedToClipboardSuccessfully = SetClipboardData( CF_UNICODETEXT, hNewMem ) != IntPtr.Zero;
						}
						//	If the clipboard data was accepted, we are no longer responsible for the allocated memory, so only free it if something went wrong.
						if( !copiedToClipboardSuccessfully )
						{
							GlobalFree( hNewMem );
						}
					}
				}

				CloseClipboard();
			}
		}

		private static byte[] GetNullTerminatedByteArrayFromString( string str )
		{
			char[] chars = str.ToCharArray();
			uint numBytes = sizeof( char ) * (uint)( chars.Length + 1 );
			byte[] bytes = new byte[numBytes];
			for( int i = 0; i < chars.Length; ++i )
			{
				Array.Copy( BitConverter.GetBytes( chars[i] ), 0, bytes, i * sizeof( char ), sizeof( char ) );
			}
			for( int i = bytes.Length - sizeof( char ); i < bytes.Length; ++i )
			{
				bytes[i] = 0;
			}

			return bytes;
		}

		//	Imported Functions
		[DllImport( "kernel32.dll", SetLastError = true )]
		private static extern IntPtr GlobalAlloc( UInt32 uFlags, UIntPtr dwBytes );

		[DllImport( "kernel32.dll", SetLastError = true )]
		private static extern IntPtr GlobalLock( IntPtr hMem );

		[DllImport( "kernel32.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		private static extern bool GlobalUnlock( IntPtr hMem );

		[DllImport( "kernel32.dll", SetLastError = true )]
		private static extern IntPtr GlobalFree( IntPtr hMem );

		[DllImport( "user32.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		private static extern bool OpenClipboard( IntPtr hWndNewOwner );

		[DllImport( "user32.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		private static extern bool EmptyClipboard();

		[DllImport( "user32.dll", SetLastError = true )]
		private static extern IntPtr SetClipboardData( UInt32 uFormat, IntPtr hMem );

		[DllImport( "user32.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		private static extern bool CloseClipboard();

		//	Standard Clipboard Formats
		private const UInt32 CF_TEXT = 1u;
		private const UInt32 CF_UNICODETEXT = 13u;

		//	Memory Allocation Flags
		private const UInt32 GHND = 0x42u;
		private const UInt32 GMEM_FIXED = 0x0u;
		private const UInt32 GMEM_MOVEABLE = 0x2u;
		private const UInt32 GMEM_ZEROINIT = 0x40u;
		private const UInt32 GPTR = 0x40u;
	}
}
