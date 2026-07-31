using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace v2rayN.Views;

/// <summary>
/// Lists the signed-in CubeVPN account's purchased services (see
/// accountme.php) and lets the user add any of them to their server list —
/// mirrors the Android app's "My services" screen: shown with just the
/// subscription link, only imported on explicit user action.
/// </summary>
public partial class CubeServicesView : UserControl
{
    private readonly Config _config;

    public CubeServicesView()
    {
        InitializeComponent();
        _config = AppManager.Instance.Config;
        btnRefresh.Click += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var token = _config.CubeAuthItem?.Token;
        if (token.IsNullOrEmpty())
        {
            return;
        }

        SetBusy(true);
        var result = await CubeAuthApi.FetchAccountAsync(token);
        SetBusy(false);

        if (result is not CubeAuthResult.AccountOk ok)
        {
            return;
        }

        panelServices.Children.Clear();
        txtEmpty.Visibility = ok.Services.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var svc in ok.Services)
        {
            panelServices.Children.Add(BuildServiceRow(svc));
        }
    }

    private UIElement BuildServiceRow(CubeAccountService svc)
    {
        var card = new Card
        {
            Margin = new Thickness(0, 0, 0, 8),
        };
        var stack = new StackPanel { Margin = new Thickness(12) };

        stack.Children.Add(new TextBlock
        {
            Text = svc.Name.IsNullOrEmpty() ? "Service" : svc.Name,
            FontSize = 15,
        });

        var usage = BuildUsageText(svc);
        if (usage.IsNotEmpty())
        {
            stack.Children.Add(new TextBlock
            {
                Text = usage,
                FontSize = 12,
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 0),
            });
        }

        var urlRow = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var addBtn = new Button
        {
            Content = ResUI.CubeServicesAdd,
            Padding = new Thickness(8, 4, 8, 4),
        };
        DockPanel.SetDock(addBtn, Dock.Right);

        var copyBtn = new Button
        {
            Width = 24,
            Height = 24,
            Content = new PackIcon { Kind = PackIconKind.ContentCopy },
            ToolTip = ResUI.CubeServicesCopyLink,
        };
        DockPanel.SetDock(copyBtn, Dock.Right);
        copyBtn.Click += (_, _) => Clipboard.SetText(svc.SubscriptionUrl);

        var urlText = new TextBlock
        {
            Text = svc.SubscriptionUrl,
            FontSize = 11,
            Opacity = 0.6,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        urlRow.Children.Add(addBtn);
        urlRow.Children.Add(copyBtn);
        urlRow.Children.Add(urlText);
        stack.Children.Add(urlRow);

        addBtn.Click += async (_, _) =>
        {
            if (svc.SubscriptionUrl.IsNullOrEmpty())
            {
                return;
            }
            addBtn.IsEnabled = false;
            var subId = Utils.GetGuid(false);
            var subItem = new SubItem
            {
                Id = subId,
                Url = svc.SubscriptionUrl,
                Remarks = svc.Name.IsNullOrEmpty() ? "CubeVPN" : svc.Name,
            };
            var ret = await ConfigHandler.AddSubItem(_config, subItem);
            if (ret == 0)
            {
                await Task.Run(async () => await SubscriptionHandler.UpdateProcess(_config, subId, false, (_, _) => Task.CompletedTask));
                addBtn.Content = ResUI.CubeServicesAdded;
            }
            else
            {
                addBtn.IsEnabled = true;
            }
        };

        card.Content = stack;
        return card;
    }

    private static string BuildUsageText(CubeAccountService svc)
    {
        if (svc.TotalBytes <= 0 && svc.Expire <= 0)
        {
            return "";
        }
        var parts = new List<string>();
        if (svc.TotalBytes > 0)
        {
            var remaining = Math.Max(0, svc.TotalBytes - svc.UsedBytes);
            parts.Add($"{Utils.HumanFy(remaining)} / {Utils.HumanFy(svc.TotalBytes)}");
        }
        if (svc.Expire > 0)
        {
            var daysLeft = (svc.Expire * 1000 - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 86_400_000L;
            if (daysLeft >= 0)
            {
                parts.Add($"{daysLeft}d left");
            }
        }
        return string.Join("   •   ", parts);
    }

    private void SetBusy(bool busy)
    {
        progressBusy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
}
