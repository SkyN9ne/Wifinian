﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ManagedNativeWifi;

namespace WlanProfileViewer.Models.Wlan
{
	internal class NetshWorker : IWlanWorker
	{
		#region Dispose

		private bool _disposed = false;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			_disposed = true;
		}

		#endregion

		public event EventHandler NetworkRefreshed;
		public event EventHandler InterfaceChanged;
		public event EventHandler ConnectionChanged;
		public event EventHandler ProfileChanged;

		#region Scan networks

		public async Task ScanNetworkAsync(TimeSpan timeout)
		{
			await Task.Delay(TimeSpan.FromSeconds(1)); // Dummy

			deferTask = DeferAsync(() =>
			{
				NetworkRefreshed?.Invoke(this, EventArgs.Empty);
				InterfaceChanged?.Invoke(this, EventArgs.Empty);
			});
		}

		#endregion

		#region Get profiles

		public async Task<IEnumerable<ProfileItem>> GetProfilesAsync()
		{
			var interfacePacks = (await Netsh.GetInterfacesAsync().ConfigureAwait(false))
				.ToArray(); // ToArray method is necessary.

			var networkPacks = (await Netsh.GetNetworksAsync().ConfigureAwait(false))
				.ToArray(); // ToArray method is necessary.

			var profilePacks = await Netsh.GetProfilesAsync().ConfigureAwait(false);

			return from profilePack in profilePacks
				   let networkPack = networkPacks.FirstOrDefault(x =>
					   x.InterfaceName.Equals(profilePack.InterfaceName, StringComparison.Ordinal) &&
					   x.Ssid.Equals(profilePack.Ssid, StringComparison.Ordinal))
				   from interfacePack in interfacePacks
				   where profilePack.InterfaceName.Equals(interfacePack.Name, StringComparison.Ordinal)
				   select new ProfileItem(
					   name: profilePack.Name,
					   interfaceId: interfacePack.Id,
					   interfaceName: profilePack.InterfaceName,
					   interfaceDescription: interfacePack.Description,
					   authentication: ConvertToAuthenticationMethod(profilePack.Authentication),
					   encryption: ConvertToEncryptionType(profilePack.Encryption),
					   isAutoConnectionEnabled: profilePack.IsAutoConnectionEnabled,
					   isAutoSwitchEnabled: profilePack.IsAutoSwitchEnabled,
					   position: profilePack.Position,
					   signal: (networkPack?.Signal ?? 0),
					   isConnected: (interfacePack.IsConnected && profilePack.Name.Equals(interfacePack.ProfileName, StringComparison.Ordinal)));
		}

		private static AuthenticationMethod ConvertToAuthenticationMethod(string authenticationString)
		{
			switch (authenticationString)
			{
				case "Open":
					return AuthenticationMethod.Open;
				case "Shared":
					return AuthenticationMethod.Shared;
				case "WPA-Enterprise":
					return AuthenticationMethod.WPA_Enterprise;
				case "WPA-Personal":
					return AuthenticationMethod.WPA_Personal;
				case "WPA2-Enterprise":
					return AuthenticationMethod.WPA2_Enterprise;
				case "WPA2-Personal":
					return AuthenticationMethod.WPA2_Personal;
				default:
					return AuthenticationMethod.None;
			}
		}

		private static EncryptionType ConvertToEncryptionType(string encryptionString)
		{
			switch (encryptionString)
			{
				case "WEP":
					return EncryptionType.WEP;
				case "TKIP":
					return EncryptionType.TKIP;
				case "CCMP":
					return EncryptionType.AES;
				default: // none
					return EncryptionType.None;
			}
		}

		#endregion

		#region Set profile

		public async Task<bool> SetProfileParameterAsync(ProfileItem profileItem)
		{
			var item = profileItem ?? throw new ArgumentNullException(nameof(profileItem));

			if (!await Netsh.SetProfileParameterAsync(item.InterfaceName, item.Name, item.IsAutoConnectionEnabled, item.IsAutoSwitchEnabled))
				return false;

			deferTask = DeferAsync(() => ProfileChanged?.Invoke(this, EventArgs.Empty));
			return true;
		}

		public async Task<bool> SetProfilePositionAsync(ProfileItem profileItem, int position)
		{
			var item = profileItem ?? throw new ArgumentNullException(nameof(profileItem));

			if (position < 0)
				throw new ArgumentOutOfRangeException(nameof(position));

			if (!await Netsh.SetProfilePositionAsync(item.InterfaceName, item.Name, position))
				return false;

			deferTask = DeferAsync(() => ProfileChanged?.Invoke(this, EventArgs.Empty));
			return true;
		}

		#endregion

		#region Delete profile

		public async Task<bool> DeleteProfileAsync(ProfileItem profileItem)
		{
			var item = profileItem ?? throw new ArgumentNullException(nameof(profileItem));

			if (!await Netsh.DeleteProfileAsync(item.InterfaceName, item.Name))
				return false;

			deferTask = DeferAsync(() => ProfileChanged?.Invoke(this, EventArgs.Empty));
			return true;
		}

		#endregion

		#region Connect/Disconnect

		public async Task<bool> ConnectNetworkAsync(ProfileItem profileItem)
		{
			var item = profileItem ?? throw new ArgumentNullException(nameof(profileItem));

			if (!await Netsh.ConnectNetworkAsync(item.InterfaceName, item.Name))
				return false;

			deferTask = DeferAsync(() => ConnectionChanged?.Invoke(this, EventArgs.Empty));
			return true;
		}

		public async Task<bool> DisconnectNetworkAsync(ProfileItem profileItem)
		{
			var item = profileItem ?? throw new ArgumentNullException(nameof(profileItem));

			if (!await Netsh.DisconnectNetworkAsync(item.InterfaceName))
				return false;

			deferTask = DeferAsync(() => ConnectionChanged?.Invoke(this, EventArgs.Empty));
			return true;
		}

		#endregion

		#region Base

		private Task deferTask;

		private async Task DeferAsync(Action action)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
			action?.Invoke();
		}

		#endregion
	}
}