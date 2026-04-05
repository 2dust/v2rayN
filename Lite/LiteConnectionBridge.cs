namespace v2rayN.Lite;

/// <summary>
/// Bridge to v2rayN backend infrastructure for Lite UI
/// Handles VLESS profile import, config generation, and connection management
/// while reusing existing AppManager, ConfigHandler, CoreManager infrastructure
/// </summary>
public class LiteConnectionBridge
{
    private const string LiteProfileName = "VPN RUS Client Lite";
    private string? _currentProfileId;

    public async Task<bool> ConnectAsync(string vlessLink, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate VLESS link
            if (!vlessLink.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Import/update VLESS profile using existing v2rayN infrastructure
            _currentProfileId = await ImportVlessProfileAsync(vlessLink, cancellationToken);

            if (string.IsNullOrEmpty(_currentProfileId))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Set profile as active
            var profile = AppManager.Instance?.GetProfileById(_currentProfileId);
            if (profile == null)
            {
                return false;
            }

            AppManager.Instance?.SetActiveProfile(_currentProfileId);

            // Enable TUN in the profile configuration
            profile.EnableTun = true;
            ConfigHandler.SaveProfile(profile);

            cancellationToken.ThrowIfCancellationRequested();

            // Start the core using existing v2rayN pipeline
            // This reuses CoreConfigContextBuilder → CoreConfigHandler → CoreManager
            await Task.Delay(500, cancellationToken);
            var result = await CoreManager.Instance.StartAsync();

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(LiteConnectionBridge), ex);
            return false;
        }
    }

    public async Task<bool> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Stop core using existing CoreManager
            var result = await CoreManager.Instance.StopAsync();
            
            await Task.Delay(300, cancellationToken);
            
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(LiteConnectionBridge), ex);
            return false;
        }
    }

    public bool IsConnected()
    {
        try
        {
            return CoreManager.Instance.IsRunning();
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> ImportVlessProfileAsync(string vlessLink, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if Lite profile already exists
            var existingProfile = AppManager.Instance?.GetProfiles()
                .FirstOrDefault(p => p.Name == LiteProfileName);

            ProfileItem profile;

            if (existingProfile != null)
            {
                // Update existing profile
                profile = existingProfile;
            }
            else
            {
                // Create new profile
                var groupId = await GetOrCreateLiteGroupAsync(cancellationToken);
                profile = new ProfileItem
                {
                    Name = LiteProfileName,
                    GroupId = groupId
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Parse VLESS link and update profile
            if (!ParseVlessLink(vlessLink, profile))
            {
                return null;
            }

            // Save profile
            ConfigHandler.SaveProfile(profile);

            return profile.Id;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(LiteConnectionBridge), ex);
            return null;
        }
    }

    private bool ParseVlessLink(string vlessLink, ProfileItem profile)
    {
        try
        {
            // Basic VLESS link parsing
            // Format: vless://uuid@host:port?encryption=none&security=reality&sni=domain&fp=...
            
            if (!Uri.TryCreate(vlessLink, UriKind.Absolute, out var uri))
            {
                return false;
            }

            // Extract UUID (user info)
            var userInfo = uri.UserInfo;
            if (string.IsNullOrEmpty(userInfo))
            {
                return false;
            }

            // Extract host and port
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 443;

            if (string.IsNullOrEmpty(host))
            {
                return false;
            }

            // Parse query parameters
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var security = query["security"] ?? "reality";
            var sni = query["sni"] ?? host;
            var fp = query["fp"] ?? "chrome";

            // Update profile with parsed VLESS data
            profile.Address = host;
            profile.Port = port;
            profile.Security = security;
            profile.Sni = sni;
            profile.Fingerprint = fp;
            profile.Protocol = EProtocol.VLESS;

            // Enable TUN
            profile.EnableTun = true;

            return true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(LiteConnectionBridge), ex);
            return false;
        }
    }

    private async Task<string> GetOrCreateLiteGroupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var liteGroup = AppManager.Instance?.GetProfileGroups()
                .FirstOrDefault(g => g.GroupName == LiteProfileName);

            if (liteGroup != null)
            {
                return liteGroup.Id;
            }

            // Create new group
            var newGroup = new ProfileGroup
            {
                GroupName = LiteProfileName
            };

            ConfigHandler.SaveProfileGroup(newGroup);

            return newGroup.Id;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(LiteConnectionBridge), ex);
            return string.Empty;
        }
    }
}