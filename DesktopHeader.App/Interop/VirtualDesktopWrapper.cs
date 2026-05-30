// Author: Markus Scholtes, 2025
// Version 1.21, 2025-08-11
// Version for Windows 11 & Windows 10 Compatibility with Headless Simulation Fallback

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using DesktopHeader.App;

namespace VirtualDesktop
{
	#region COM API
	internal static class Guids
	{
		public static readonly Guid CLSID_ImmersiveShell = new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
		public static readonly Guid CLSID_VirtualDesktopManagerInternal = new Guid("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
		public static readonly Guid CLSID_VirtualDesktopManager = new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");
		public static readonly Guid CLSID_VirtualDesktopPinnedApps = new Guid("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct Size
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct Rect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	internal enum APPLICATION_VIEW_CLOAK_TYPE : int
	{
		AVCT_NONE = 0,
		AVCT_DEFAULT = 1,
		AVCT_VIRTUAL_DESKTOP = 2
	}

	internal enum APPLICATION_VIEW_COMPATIBILITY_POLICY : int
	{
		AVCP_NONE = 0,
		AVCP_SMALL_SCREEN = 1,
		AVCP_TABLET_SMALL_SCREEN = 2,
		AVCP_VERY_SMALL_SCREEN = 3,
		AVCP_HIGH_SCALE_FACTOR = 4
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513")]
	internal interface IApplicationView
	{
		int GetIids(out uint iidCount, out IntPtr iids);
		int GetRuntimeClassName([MarshalAs(UnmanagedType.LPWStr)] out string className);
		int GetTrustLevel(out int trustLevel);

		int SetFocus();
		int SwitchTo();
		int TryInvokeBack(IntPtr callback);
		int GetThumbnailWindow(out IntPtr hwnd);
		int GetMonitor(out IntPtr immersiveMonitor);
		int GetVisibility(out int visibility);
		int SetCloak(APPLICATION_VIEW_CLOAK_TYPE cloakType, int unknown);
		int GetPosition(ref Guid guid, out IntPtr position);
		int SetPosition(ref IntPtr position);
		int InsertAfterWindow(IntPtr hwnd);
		int GetExtendedFramePosition(out Rect rect);
		int GetAppUserModelId([MarshalAs(UnmanagedType.LPWStr)] out string id);
		int SetAppUserModelId(string id);
		int IsEqualByAppUserModelId(string id, out int result);
		int GetViewState(out uint state);
		int SetViewState(uint state);
		int GetNeediness(out int neediness);
		int GetLastActivationTimestamp(out ulong timestamp);
		int SetLastActivationTimestamp(ulong timestamp);
		int GetVirtualDesktopId(out Guid guid);
		int SetVirtualDesktopId(ref Guid guid);
		int GetShowInSwitchers(out int flag);
		int SetShowInSwitchers(int flag);
		int GetScaleFactor(out int factor);
		int CanReceiveInput(out bool canReceiveInput);
		int GetCompatibilityPolicyType(out APPLICATION_VIEW_COMPATIBILITY_POLICY flags);
		int SetCompatibilityPolicyType(APPLICATION_VIEW_COMPATIBILITY_POLICY flags);
		int GetSizeConstraints(IntPtr monitor, out Size size1, out Size size2);
		int GetSizeConstraintsForDpi(uint uint1, out Size size1, out Size size2);
		int SetSizeConstraintsForDpi(ref uint uint1, ref Size size1, ref Size size2);
		int OnMinSizePreferencesUpdated(IntPtr hwnd);
		int ApplyOperation(IntPtr operation);
		int IsTray(out bool isTray);
		int IsInHighZOrderBand(out bool isInHighZOrderBand);
		int IsSplashScreenPresented(out bool isSplashScreenPresented);
		int Flash();
		int GetRootSwitchableOwner(out IApplicationView rootSwitchableOwner);
		int EnumerateOwnershipTree(out IObjectArray ownershipTree);
		int GetEnterpriseId([MarshalAs(UnmanagedType.LPWStr)] out string enterpriseId);
		int IsMirrored(out bool isMirrored);
		int Unknown1(out int unknown);
		int Unknown2(out int unknown);
		int Unknown3(out int unknown);
		int Unknown4(out int unknown);
		int Unknown5(out int unknown);
		int Unknown6(int unknown);
		int Unknown7();
		int Unknown8(out int unknown);
		int Unknown9(int unknown);
		int Unknown10(int unknownX, int unknownY);
		int Unknown11(int unknown);
		int Unknown12(out Size size1);
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
	internal interface IApplicationViewCollection
	{
		int GetViews(out IObjectArray array);
		int GetViewsByZOrder(out IObjectArray array);
		int GetViewsByAppUserModelId(string id, out IObjectArray array);
		int GetViewForHwnd(IntPtr hwnd, out IApplicationView view);
		int GetViewForApplication(object application, out IApplicationView view);
		int GetViewForAppUserModelId(string id, out IApplicationView view);
		int GetViewInFocus(out IntPtr view);
		int Unknown1(out IntPtr view);
		void RefreshCollection();
		int RegisterForApplicationViewChanges(object listener, out int cookie);
		int UnregisterForApplicationViewChanges(int cookie);
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
	internal interface IObjectArray
	{
		void GetCount(out int count);
		void GetAt(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)]out object obj);
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
	internal interface IServiceProvider10
	{
		[return: MarshalAs(UnmanagedType.IUnknown)]
		object QueryService(ref Guid service, ref Guid riid);
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("4CE81583-1E4C-4632-A621-07A53543148F")]
	internal interface IVirtualDesktopPinnedApps
	{
		bool IsAppIdPinned(string appId);
		void PinAppID(string appId);
		void UnpinAppID(string appId);
		bool IsViewPinned(IApplicationView applicationView);
		void PinView(IApplicationView applicationView);
		void UnpinView(IApplicationView applicationView);
	}

	// --- Windows 11 COM Interfaces ---
	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("3F07F4BE-B107-441A-AF0F-39D82529072C")]
	internal interface IVirtualDesktop
	{
		bool IsViewVisible(IApplicationView view);
		Guid GetId();
		[return: MarshalAs(UnmanagedType.HString)]
		string GetName();
		[return: MarshalAs(UnmanagedType.HString)]
		string GetWallpaperPath();
		bool IsRemote();
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("53F5CA0B-158F-4124-900C-057158060B27")]
	internal interface IVirtualDesktopManagerInternal
	{
		int GetCount();
		void MoveViewToDesktop(IApplicationView view, IVirtualDesktop desktop);
		bool CanViewMoveDesktops(IApplicationView view);
		IVirtualDesktop GetCurrentDesktop();
		void GetDesktops(out IObjectArray desktops);
		[PreserveSig]
		int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
		void SwitchDesktop(IVirtualDesktop desktop);
		void SwitchDesktopAndMoveForegroundView(IVirtualDesktop desktop);
		IVirtualDesktop CreateDesktop();
		void MoveDesktop(IVirtualDesktop desktop, int nIndex);
		void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
		IVirtualDesktop FindDesktop(ref Guid desktopid);
		void GetDesktopSwitchIncludeExcludeViews(IVirtualDesktop desktop, out IObjectArray unknown1, out IObjectArray unknown2);
		void SetDesktopName(IVirtualDesktop desktop, [MarshalAs(UnmanagedType.HString)] string name);
		void SetDesktopWallpaper(IVirtualDesktop desktop, [MarshalAs(UnmanagedType.HString)] string path);
		void UpdateWallpaperPathForAllDesktops([MarshalAs(UnmanagedType.HString)] string path);
		void CopyDesktopState(IApplicationView pView0, IApplicationView pView1);
		void CreateRemoteDesktop([MarshalAs(UnmanagedType.HString)] string path, out IVirtualDesktop desktop);
		void SwitchRemoteDesktop(IVirtualDesktop desktop, IntPtr switchtype);
		void SwitchDesktopWithAnimation(IVirtualDesktop desktop);
		void GetLastActiveDesktop(out IVirtualDesktop desktop);
		void WaitForAnimationToComplete();
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
	internal interface IVirtualDesktopManager
	{
		bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
		Guid GetWindowDesktopId(IntPtr topLevelWindow);
		void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
	}

	// --- Windows 10 COM Interfaces ---
	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4")]
	internal interface IVirtualDesktop10
	{
		bool IsViewVisible(IApplicationView view);
		Guid GetId();
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("F31574D6-B682-4CDC-BD56-1827860ABEC6")]
	internal interface IVirtualDesktopManagerInternal10
	{
		int GetCount();
		void MoveViewToDesktop(IApplicationView view, IVirtualDesktop10 desktop);
		bool CanViewMoveDesktops(IApplicationView view);
		IVirtualDesktop10 GetCurrentDesktop();
		void GetDesktops(out IObjectArray desktops);
		[PreserveSig]
		int GetAdjacentDesktop(IVirtualDesktop10 from, int direction, out IVirtualDesktop10 desktop);
		void SwitchDesktop(IVirtualDesktop10 desktop);
		IVirtualDesktop10 CreateDesktop();
		void RemoveDesktop(IVirtualDesktop10 desktop, IVirtualDesktop10 fallback);
		IVirtualDesktop10 FindDesktop(ref Guid desktopid);
	}
	#endregion

	#region Backend Abstraction
	internal interface IDesktopBackend
	{
		int GetCount();
		Guid GetCurrentDesktopId();
		Guid GetDesktopId(int index);
		string GetDesktopName(int index);
		void SwitchToDesktop(int index);
		Guid CreateDesktop();
		void RemoveDesktop(Guid id, Guid fallbackId);
		void SetDesktopName(Guid id, string name);
		int GetDesktopIndex(Guid id);
		bool IsWindowPinned(IntPtr hWnd);
		void PinWindow(IntPtr hWnd);
		void UnpinWindow(IntPtr hWnd);
		Guid GetWindowDesktopId(IntPtr hWnd);
	}

	internal static class DesktopManager
	{
		internal static readonly IDesktopBackend Backend;

		static DesktopManager()
		{
			try
			{
				// Attempt to initialize Windows 11 COM
				Backend = new Windows11DesktopBackend();
				Logger.LogInfo("Successfully initialized Windows 11 Virtual Desktop COM backend.");
			}
			catch (Exception ex11)
			{
				Logger.LogWarning($"Windows 11 Virtual Desktop COM initialization failed: {ex11.Message}. Attempting Windows 10 COM backend...");
				try
				{
					// Attempt to initialize Windows 10 COM
					Backend = new Windows10DesktopBackend();
					Logger.LogInfo("Successfully initialized Windows 10 Virtual Desktop COM backend.");
				}
				catch (Exception ex10)
				{
					Logger.LogWarning($"Windows 10 Virtual Desktop COM initialization failed: {ex10.Message}. Falling back to Simulated Desktop backend.");
					Backend = new SimulatedDesktopBackend();
				}
			}
		}
	}

	// --- Windows 11 Implementation ---
	internal class Windows11DesktopBackend : IDesktopBackend
	{
		private readonly IServiceProvider10 shell;
		private readonly IVirtualDesktopManagerInternal win11Internal;
		private readonly IVirtualDesktopManager win11Manager;
		private readonly IApplicationViewCollection win11Collection;
		private readonly IVirtualDesktopPinnedApps win11Pinned;

		public Windows11DesktopBackend()
		{
			shell = (IServiceProvider10)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_ImmersiveShell))!;
			
			Guid serviceInternal = Guids.CLSID_VirtualDesktopManagerInternal;
			Guid riidInternal = typeof(IVirtualDesktopManagerInternal).GUID;
			win11Internal = (IVirtualDesktopManagerInternal)shell.QueryService(ref serviceInternal, ref riidInternal);
			
			win11Manager = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_VirtualDesktopManager))!;
			
			Guid serviceCollection = typeof(IApplicationViewCollection).GUID;
			Guid riidCollection = typeof(IApplicationViewCollection).GUID;
			win11Collection = (IApplicationViewCollection)shell.QueryService(ref serviceCollection, ref riidCollection);
			
			Guid servicePinned = Guids.CLSID_VirtualDesktopPinnedApps;
			Guid riidPinned = typeof(IVirtualDesktopPinnedApps).GUID;
			win11Pinned = (IVirtualDesktopPinnedApps)shell.QueryService(ref servicePinned, ref riidPinned);
		}

		public int GetCount() => win11Internal.GetCount();
		
		public Guid GetCurrentDesktopId()
		{
			var desktop = win11Internal.GetCurrentDesktop();
			try { return desktop.GetId(); } finally { Marshal.ReleaseComObject(desktop); }
		}

		public Guid GetDesktopId(int index)
		{
			var desktop = GetDesktop(index);
			try { return desktop.GetId(); } finally { Marshal.ReleaseComObject(desktop); }
		}

		public string GetDesktopName(int index)
		{
			var desktop = GetDesktop(index);
			try { return desktop.GetName(); } catch { return ""; } finally { Marshal.ReleaseComObject(desktop); }
		}

		public void SwitchToDesktop(int index)
		{
			var desktop = GetDesktop(index);
			try { win11Internal.SwitchDesktop(desktop); } finally { Marshal.ReleaseComObject(desktop); }
		}

		public Guid CreateDesktop()
		{
			var desktop = win11Internal.CreateDesktop();
			try { return desktop.GetId(); } finally { Marshal.ReleaseComObject(desktop); }
		}

		public void RemoveDesktop(Guid id, Guid fallbackId)
		{
			var desktop = win11Internal.FindDesktop(ref id);
			var fallback = win11Internal.FindDesktop(ref fallbackId);
			try { win11Internal.RemoveDesktop(desktop, fallback); } finally { Marshal.ReleaseComObject(desktop); Marshal.ReleaseComObject(fallback); }
		}

		public void SetDesktopName(Guid id, string name)
		{
			var desktop = win11Internal.FindDesktop(ref id);
			try { win11Internal.SetDesktopName(desktop, name); } finally { Marshal.ReleaseComObject(desktop); }
		}

		public int GetDesktopIndex(Guid id)
		{
			IObjectArray desktops;
			win11Internal.GetDesktops(out desktops);
			try
			{
				int count = win11Internal.GetCount();
				for (int i = 0; i < count; i++)
				{
					Guid riid = typeof(IVirtualDesktop).GUID;
					object obj;
					desktops.GetAt(i, ref riid, out obj);
					var d = (IVirtualDesktop)obj;
					Guid curId = d.GetId();
					Marshal.ReleaseComObject(d);
					if (curId == id) return i;
				}
				return -1;
			}
			finally
			{
				Marshal.ReleaseComObject(desktops);
			}
		}

		public bool IsWindowPinned(IntPtr hWnd)
		{
			IApplicationView view;
			win11Collection.GetViewForHwnd(hWnd, out view);
			try { return win11Pinned.IsViewPinned(view); } finally { Marshal.ReleaseComObject(view); }
		}

		public void PinWindow(IntPtr hWnd)
		{
			IApplicationView view;
			win11Collection.GetViewForHwnd(hWnd, out view);
			try { if (!win11Pinned.IsViewPinned(view)) win11Pinned.PinView(view); } finally { Marshal.ReleaseComObject(view); }
		}

		public void UnpinWindow(IntPtr hWnd)
		{
			IApplicationView view;
			win11Collection.GetViewForHwnd(hWnd, out view);
			try { if (win11Pinned.IsViewPinned(view)) win11Pinned.UnpinView(view); } finally { Marshal.ReleaseComObject(view); }
		}

		public Guid GetWindowDesktopId(IntPtr hWnd)
		{
			try { return win11Manager.GetWindowDesktopId(hWnd); } catch { return Guid.Empty; }
		}

		private IVirtualDesktop GetDesktop(int index)
		{
			int count = win11Internal.GetCount();
			if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
			IObjectArray desktops;
			win11Internal.GetDesktops(out desktops);
			try
			{
				Guid riid = typeof(IVirtualDesktop).GUID;
				object obj;
				desktops.GetAt(index, ref riid, out obj);
				return (IVirtualDesktop)obj;
			}
			finally
			{
				Marshal.ReleaseComObject(desktops);
			}
		}
	}

	// --- Windows 10 Implementation ---
	internal class Windows10DesktopBackend : IDesktopBackend
	{
		private readonly IServiceProvider10 shell;
		private readonly IVirtualDesktopManagerInternal10 win10Internal;
		private readonly IVirtualDesktopManager win10Manager;
		private readonly IApplicationViewCollection win10Collection;
		private readonly IVirtualDesktopPinnedApps win10Pinned;

		public Windows10DesktopBackend()
		{
			shell = (IServiceProvider10)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_ImmersiveShell))!;
			
			Guid serviceInternal = Guids.CLSID_VirtualDesktopManagerInternal;
			Guid riidInternal = typeof(IVirtualDesktopManagerInternal10).GUID;
			win10Internal = (IVirtualDesktopManagerInternal10)shell.QueryService(ref serviceInternal, ref riidInternal);
			
			win10Manager = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_VirtualDesktopManager))!;
			
			Guid serviceCollection = typeof(IApplicationViewCollection).GUID;
			Guid riidCollection = typeof(IApplicationViewCollection).GUID;
			win10Collection = (IApplicationViewCollection)shell.QueryService(ref serviceCollection, ref riidCollection);
			
			Guid servicePinned = Guids.CLSID_VirtualDesktopPinnedApps;
			Guid riidPinned = typeof(IVirtualDesktopPinnedApps).GUID;
			win10Pinned = (IVirtualDesktopPinnedApps)shell.QueryService(ref servicePinned, ref riidPinned);
		}

		public int GetCount() => win10Internal.GetCount();
		
		public Guid GetCurrentDesktopId()
		{
			var desktop = win10Internal.GetCurrentDesktop();
			try { return desktop.GetId(); } finally { Marshal.ReleaseComObject(desktop); }
		}

		public Guid GetDesktopId(int index)
		{
			var desktop = GetDesktop(index);
			try { return desktop.GetId(); } finally { Marshal.ReleaseComObject(desktop); }
		}

		public string GetDesktopName(int index) => ""; // Windows 10 does not support name query via COM, read from registry instead

		public void SwitchToDesktop(int index)
		{
			var desktop = GetDesktop(index);
			try { win10Internal.SwitchDesktop(desktop); } finally { Marshal.ReleaseComObject(desktop); }
		}

		public Guid CreateDesktop()
		{
			var desktop = win10Internal.CreateDesktop();
			try { return desktop.GetId(); } finally { Marshal.ReleaseComObject(desktop); }
		}

		public void RemoveDesktop(Guid id, Guid fallbackId)
		{
			var desktop = win10Internal.FindDesktop(ref id);
			var fallback = win10Internal.FindDesktop(ref fallbackId);
			try { win10Internal.RemoveDesktop(desktop, fallback); } finally { Marshal.ReleaseComObject(desktop); Marshal.ReleaseComObject(fallback); }
		}

		public void SetDesktopName(Guid id, string name)
		{
			try
			{
				string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops\Desktops\" + id.ToString("B");
				using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath))
				{
					if (key != null)
					{
						if (string.IsNullOrEmpty(name))
							key.DeleteValue("Name", false);
						else
							key.SetValue("Name", name, Microsoft.Win32.RegistryValueKind.String);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to write Windows 10 desktop name to registry", ex);
			}
		}

		public int GetDesktopIndex(Guid id)
		{
			IObjectArray desktops;
			win10Internal.GetDesktops(out desktops);
			try
			{
				int count = win10Internal.GetCount();
				for (int i = 0; i < count; i++)
				{
					Guid riid = typeof(IVirtualDesktop10).GUID;
					object obj;
					desktops.GetAt(i, ref riid, out obj);
					var d = (IVirtualDesktop10)obj;
					Guid curId = d.GetId();
					Marshal.ReleaseComObject(d);
					if (curId == id) return i;
				}
				return -1;
			}
			finally
			{
				Marshal.ReleaseComObject(desktops);
			}
		}

		public bool IsWindowPinned(IntPtr hWnd)
		{
			IApplicationView view;
			win10Collection.GetViewForHwnd(hWnd, out view);
			try { return win10Pinned.IsViewPinned(view); } finally { Marshal.ReleaseComObject(view); }
		}

		public void PinWindow(IntPtr hWnd)
		{
			IApplicationView view;
			win10Collection.GetViewForHwnd(hWnd, out view);
			try { if (!win10Pinned.IsViewPinned(view)) win10Pinned.PinView(view); } finally { Marshal.ReleaseComObject(view); }
		}

		public void UnpinWindow(IntPtr hWnd)
		{
			IApplicationView view;
			win10Collection.GetViewForHwnd(hWnd, out view);
			try { if (win10Pinned.IsViewPinned(view)) win10Pinned.UnpinView(view); } finally { Marshal.ReleaseComObject(view); }
		}

		public Guid GetWindowDesktopId(IntPtr hWnd)
		{
			try { return win10Manager.GetWindowDesktopId(hWnd); } catch { return Guid.Empty; }
		}

		private IVirtualDesktop10 GetDesktop(int index)
		{
			int count = win10Internal.GetCount();
			if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
			IObjectArray desktops;
			win10Internal.GetDesktops(out desktops);
			try
			{
				Guid riid = typeof(IVirtualDesktop10).GUID;
				object obj;
				desktops.GetAt(index, ref riid, out obj);
				return (IVirtualDesktop10)obj;
			}
			finally
			{
				Marshal.ReleaseComObject(desktops);
			}
		}
	}

	// --- Simulated Fallback Implementation ---
	internal class SimulatedDesktopBackend : IDesktopBackend
	{
		private class SimDesktop
		{
			public Guid Id { get; }
			public string Name { get; set; }
			public SimDesktop(Guid id, string name)
			{
				Id = id;
				Name = name;
			}
		}

		private readonly List<SimDesktop> desktops = new();
		private int currentIndex = 0;
		private readonly HashSet<IntPtr> pinnedWindows = new();

		public SimulatedDesktopBackend()
		{
			desktops.Add(new SimDesktop(Guid.NewGuid(), "Desktop 1"));
			desktops.Add(new SimDesktop(Guid.NewGuid(), "Desktop 2"));
			desktops.Add(new SimDesktop(Guid.NewGuid(), "Desktop 3"));
		}

		public int GetCount() => desktops.Count;
		public Guid GetCurrentDesktopId() => desktops[currentIndex].Id;
		public Guid GetDesktopId(int index) => desktops[index].Id;
		public string GetDesktopName(int index) => desktops[index].Name;
		public void SwitchToDesktop(int index)
		{
			if (index >= 0 && index < desktops.Count)
			{
				currentIndex = index;
			}
		}
		public Guid CreateDesktop()
		{
			Guid id = Guid.NewGuid();
			desktops.Add(new SimDesktop(id, $"Desktop {desktops.Count + 1}"));
			return id;
		}
		public void RemoveDesktop(Guid id, Guid fallbackId)
		{
			int index = GetDesktopIndex(id);
			if (index >= 0 && desktops.Count > 1)
			{
				desktops.RemoveAt(index);
				if (currentIndex >= desktops.Count) currentIndex = desktops.Count - 1;
			}
		}
		public void SetDesktopName(Guid id, string name)
		{
			int index = GetDesktopIndex(id);
			if (index >= 0)
			{
				desktops[index].Name = name;
			}
		}
		public int GetDesktopIndex(Guid id)
		{
			for (int i = 0; i < desktops.Count; i++)
			{
				if (desktops[i].Id == id) return i;
			}
			return -1;
		}
		public bool IsWindowPinned(IntPtr hWnd) => pinnedWindows.Contains(hWnd);
		public void PinWindow(IntPtr hWnd) => pinnedWindows.Add(hWnd);
		public void UnpinWindow(IntPtr hWnd) => pinnedWindows.Remove(hWnd);
		public Guid GetWindowDesktopId(IntPtr hWnd) => desktops[currentIndex].Id;
	}
	#endregion

	#region Public API
	public class Desktop
	{
		[DllImport("User32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[DllImport("user32.dll")]
		private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

		[DllImport("kernel32.dll")]
		private static extern uint GetCurrentThreadId();

		[DllImport("user32.dll")]
		private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		private const int SW_MINIMIZE = 6;

		private static readonly Guid AppOnAllDesktops = new Guid("BB64D5B7-4DE3-4AB2-A87C-DB7601AEA7DC");
		private static readonly Guid WindowOnAllDesktops = new Guid("C2DDEA68-66F2-4CF9-8264-1BFD00FBBBAC");

		private readonly Guid id;
		private Desktop(Guid id) { this.id = id; }

		public Guid Id => id;

		public override int GetHashCode()
		{
			return id.GetHashCode();
		}

		public override bool Equals(object? obj)
		{
			var desk = obj as Desktop;
			return desk != null && this.id == desk.id;
		}

		public static int Count
		{
			get { return DesktopManager.Backend.GetCount(); }
		}

		public static Desktop Current
		{
			get { return new Desktop(DesktopManager.Backend.GetCurrentDesktopId()); }
		}

		public static Desktop FromIndex(int index)
		{
			return new Desktop(DesktopManager.Backend.GetDesktopId(index));
		}

		public static Desktop FromWindow(IntPtr hWnd)
		{
			if (hWnd == IntPtr.Zero) throw new ArgumentNullException(nameof(hWnd));
			Guid id = DesktopManager.Backend.GetWindowDesktopId(hWnd);
			if (id == Guid.Empty || id == AppOnAllDesktops || id == WindowOnAllDesktops)
				return Desktop.Current;
			else
				return new Desktop(id);
		}

		public static int FromDesktop(Desktop desktop)
		{
			return DesktopManager.Backend.GetDesktopIndex(desktop.Id);
		}

		private static string? GetRegistryDesktopName(Guid guid)
		{
			try
			{
				string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops\Desktops\" + guid.ToString("B");
				using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath))
				{
					if (key != null)
					{
						object? val = key.GetValue("Name");
						if (val != null && !string.IsNullOrEmpty(val.ToString()))
						{
							return val.ToString();
						}
					}
				}
			}
			catch { }
			return null;
		}

		public static string DesktopNameFromDesktop(Desktop desktop)
		{
			int index = FromDesktop(desktop);
			if (index >= 0) return DesktopNameFromIndex(index);
			return "Desktop";
		}

		public static string DesktopNameFromIndex(int index)
		{
			string desktopName = DesktopManager.Backend.GetDesktopName(index);

			if (string.IsNullOrEmpty(desktopName))
			{
				try
				{
					Guid id = DesktopManager.Backend.GetDesktopId(index);
					desktopName = GetRegistryDesktopName(id) ?? "";
				}
				catch { }
			}

			if (string.IsNullOrEmpty(desktopName))
			{
				desktopName = "Desktop " + (index + 1).ToString();
			}
			return desktopName;
		}

		public static bool HasDesktopNameFromIndex(int index)
		{
			string name = DesktopManager.Backend.GetDesktopName(index);
			if (string.IsNullOrEmpty(name))
			{
				try
				{
					Guid id = DesktopManager.Backend.GetDesktopId(index);
					name = GetRegistryDesktopName(id) ?? "";
				}
				catch { }
			}
			return !string.IsNullOrEmpty(name);
		}

		public static string DesktopWallpaperFromIndex(int index) => "";

		public static int SearchDesktop(string partialName)
		{
			int count = Desktop.Count;
			for (int i = 0; i < count; i++)
			{
				if (DesktopNameFromIndex(i).ToUpper().Contains(partialName.ToUpper()))
				{
					return i;
				}
			}
			return -1;
		}

		public static Desktop Create()
		{
			return new Desktop(DesktopManager.Backend.CreateDesktop());
		}

		public void Remove(Desktop? fallback = null)
		{
			Guid fallbackId = fallback?.Id ?? Guid.Empty;
			if (fallbackId == Guid.Empty)
			{
				int currentIdx = FromDesktop(this);
				if (currentIdx == 0)
				{
					if (Count > 1) fallbackId = FromIndex(1).Id;
				}
				else
				{
					fallbackId = FromIndex(currentIdx - 1).Id;
				}
			}
			
			if (fallbackId != Guid.Empty)
			{
				DesktopManager.Backend.RemoveDesktop(this.Id, fallbackId);
			}
		}

		public static void RemoveAll()
		{
			int count = Desktop.Count;
			var current = Desktop.Current;
			for (int i = count - 1; i >= 0; i--)
			{
				var d = FromIndex(i);
				if (!d.Equals(current))
				{
					d.Remove(current);
				}
			}
		}

		public void Move(int index) { }

		public void SetName(string Name)
		{
			DesktopManager.Backend.SetDesktopName(this.Id, Name);
		}

		public void SetWallpaperPath(string Path) { }

		public static void SetAllWallpaperPaths(string Path) { }

		public bool IsVisible => this.Id == Desktop.Current.Id;

		private static bool AnimateDesktopSwitch = true;

		public static void SetAnimation(bool OnOff)
		{
			AnimateDesktopSwitch = OnOff;
		}

		public void MakeVisible()
		{
			IntPtr hWnd = AnimateDesktopSwitch ? FindWindow("Shell_TrayWnd", "") : FindWindow("XamlExplorerHostIslandWindow", null);

			if (hWnd != IntPtr.Zero)
			{
				int dummy;
				uint DesktopThreadId = GetWindowThreadProcessId(hWnd, out dummy);
				uint ForegroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), out dummy);
				uint CurrentThreadId = GetCurrentThreadId();

				if ((DesktopThreadId != 0) && (ForegroundThreadId != 0) && (ForegroundThreadId != CurrentThreadId))
				{
					AttachThreadInput(DesktopThreadId, CurrentThreadId, true);
					AttachThreadInput(ForegroundThreadId, CurrentThreadId, true);
					SetForegroundWindow(hWnd);
					AttachThreadInput(ForegroundThreadId, CurrentThreadId, false);
					AttachThreadInput(DesktopThreadId, CurrentThreadId, false);
				}
			}

			int index = FromDesktop(this);
			if (index >= 0)
			{
				DesktopManager.Backend.SwitchToDesktop(index);
			}

			if (hWnd != IntPtr.Zero)
			{
				ShowWindow(hWnd, SW_MINIMIZE);
			}
		}

		public Desktop? Left
		{
			get
			{
				int index = FromDesktop(this);
				if (index > 0)
					return FromIndex(index - 1);
				return null;
			}
		}

		public Desktop? Right
		{
			get
			{
				int index = FromDesktop(this);
				if (index >= 0 && index < Count - 1)
					return FromIndex(index + 1);
				return null;
			}
		}

		public void MoveWindow(IntPtr hWnd) { }

		public void MoveActiveWindow() { }

		public bool HasWindow(IntPtr hWnd)
		{
			return DesktopManager.Backend.GetWindowDesktopId(hWnd) == this.Id;
		}

		public static bool IsWindowPinned(IntPtr hWnd)
		{
			return DesktopManager.Backend.IsWindowPinned(hWnd);
		}

		public static void PinWindow(IntPtr hWnd)
		{
			DesktopManager.Backend.PinWindow(hWnd);
		}

		public static void UnpinWindow(IntPtr hWnd)
		{
			DesktopManager.Backend.UnpinWindow(hWnd);
		}

		public static bool IsApplicationPinned(IntPtr hWnd) => false;

		public static void PinApplication(IntPtr hWnd) { }

		public static void UnpinApplication(IntPtr hWnd) { }
	}
	#endregion
}
