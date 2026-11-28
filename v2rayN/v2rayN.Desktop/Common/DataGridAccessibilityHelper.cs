using System.Collections;
using System.Runtime.CompilerServices;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.VisualTree;

namespace v2rayN.Desktop.Common;

/// <summary>
/// Enhances ProDataGrid (<see cref="DataGrid"/>) keyboard navigation for Windows UIA screen readers
/// by populating row automation metadata and raising assertive live-region name updates on row/column changes.
/// </summary>
public static class DataGridAccessibilityHelper
{
    private static readonly ConditionalWeakTable<DataGrid, Attachment> _attachments = new();

    /// <summary>
    /// Attaches screen-reader accessibility handlers to <paramref name="grid"/>.
    /// Safe to call multiple times on the same instance.
    /// </summary>
    public static void Attach(DataGrid grid, Func<object, string>? formatRow = null)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (_attachments.TryGetValue(grid, out _))
        {
            return;
        }

        var attachment = new Attachment(grid, formatRow);
        _attachments.Add(grid, attachment);
    }

    /// <summary>
    /// Formats a DataGrid row data item into a concise, screen-reader-friendly summary.
    /// </summary>
    public static string FormatItem(object? item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        switch (item)
        {
            case RoutingItemModel routing:
                {
                    var parts = new[]
                    {
                        routing.IsActive ? ResUI.TipActiveServer : null,
                        routing.Remarks,
                        $"{ResUI.LvCount} {routing.RuleNum}",
                        routing.Url
                    }.Where(s => !string.IsNullOrWhiteSpace(s));
                    return string.Join(", ", parts);
                }

            case RulesItemModel rule:
                {
                    var summary = rule.ToString();
                    var parts = new[]
                    {
                        rule.Enabled ? ResUI.LvEnabled : null,
                        summary
                    }.Where(s => !string.IsNullOrWhiteSpace(s));
                    return string.Join(", ", parts);
                }

            case SubItem sub:
                {
                    var parts = new[]
                    {
                        sub.Remarks,
                        sub.Enabled ? ResUI.LvEnabled : null,
                        sub.Url
                    }.Where(s => !string.IsNullOrWhiteSpace(s));
                    return string.Join(", ", parts);
                }

            default:
                return item.ToString() ?? string.Empty;
        }
    }

    private sealed class Attachment
    {
        private readonly DataGrid _grid;
        private readonly string _baseName;
        private readonly Func<object, string>? _formatRow;
        private readonly Dictionary<object, DataGridRow> _realizedRows = new(ReferenceEqualityComparer.Instance);

        private object? _lastItem;
        private DataGridColumn? _lastColumn;
        private bool _isFocused;
        private bool _announcePending;
        private bool _toggleSuffix;

        public Attachment(DataGrid grid, Func<object, string>? formatRow)
        {
            _grid = grid;
            _baseName = AutomationProperties.GetName(grid) ?? string.Empty;
            _formatRow = formatRow;

            _grid.LoadingRow += OnLoadingRow;
            _grid.UnloadingRow += OnUnloadingRow;
            _grid.GotFocus += OnGotFocus;
            _grid.LostFocus += OnLostFocus;
            _grid.SelectionChanged += OnNavigationChanged;
            _grid.CurrentCellChanged += OnNavigationChanged;
        }

        private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
        {
            RemoveRowReference(e.Row);

            var item = e.Row.DataContext;
            if (item != null)
            {
                _realizedRows[item] = e.Row;
            }

            ApplyRowAutomationMetadata(e.Row, item);
        }

        private void OnUnloadingRow(object? sender, DataGridRowEventArgs e)
        {
            var item = e.Row.DataContext;
            if (item != null
                && _realizedRows.TryGetValue(item, out var existing)
                && ReferenceEquals(existing, e.Row))
            {
                _realizedRows.Remove(item);
            }
            else
            {
                RemoveRowReference(e.Row);
            }
        }

        private void RemoveRowReference(DataGridRow row)
        {
            foreach (var kvp in _realizedRows)
            {
                if (ReferenceEquals(kvp.Value, row))
                {
                    _realizedRows.Remove(kvp.Key);
                    break;
                }
            }
        }

        private void ApplyRowAutomationMetadata(DataGridRow row, object? item)
        {
            AutomationProperties.SetName(row, FormatRowText(item));
            AutomationProperties.SetItemStatus(row, IsItemActive(item) ? ResUI.TipActiveServer : string.Empty);
        }

        private void OnGotFocus(object? sender, FocusChangedEventArgs e)
        {
            _isFocused = true;
            _lastItem = _grid.SelectedItem;
            _lastColumn = _grid.CurrentColumn;

            Dispatcher.UIThread.Post(() =>
            {
                if (!_isFocused || !_grid.IsKeyboardFocusWithin)
                {
                    return;
                }

                AutomationProperties.SetLiveSetting(_grid, AutomationLiveSetting.Assertive);
                if (_grid.SelectedItem != null)
                {
                    Announce(FormatRowWithPosition(_grid.SelectedItem));
                }
            }, DispatcherPriority.Background);
        }

        private void OnLostFocus(object? sender, FocusChangedEventArgs e)
        {
            if (_grid.IsKeyboardFocusWithin)
            {
                return;
            }

            _isFocused = false;
            _lastItem = null;
            _lastColumn = null;

            // Disable live announcements before restoring base name so screen readers stay silent on blur.
            AutomationProperties.SetLiveSetting(_grid, AutomationLiveSetting.Off);
            AutomationProperties.SetName(_grid, _baseName);
        }

        private void OnNavigationChanged(object? sender, EventArgs e)
        {
            if ((!_isFocused && !_grid.IsKeyboardFocusWithin) || _announcePending)
            {
                return;
            }

            _announcePending = true;
            Dispatcher.UIThread.Post(FlushAnnouncement, DispatcherPriority.Input);
        }

        private void FlushAnnouncement()
        {
            _announcePending = false;
            if (!_grid.IsKeyboardFocusWithin)
            {
                return;
            }

            var item = _grid.SelectedItem;
            var column = _grid.CurrentColumn;
            if (item == null)
            {
                _lastItem = null;
                _lastColumn = column;
                return;
            }

            if (!ReferenceEquals(item, _lastItem))
            {
                _lastItem = item;
                _lastColumn = column;

                if (_realizedRows.TryGetValue(item, out var row))
                {
                    ApplyRowAutomationMetadata(row, item);
                }

                Announce(FormatRowWithPosition(item));
            }
            else if (column != null && !ReferenceEquals(column, _lastColumn))
            {
                _lastColumn = column;
                Announce(FormatCell(item, column));
            }
        }

        private void Announce(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (AutomationProperties.GetLiveSetting(_grid) != AutomationLiveSetting.Assertive)
            {
                AutomationProperties.SetLiveSetting(_grid, AutomationLiveSetting.Assertive);
            }

            var oldName = AutomationProperties.GetName(_grid) ?? _baseName;
            var announcement = text;
            if (string.Equals(oldName, announcement, StringComparison.Ordinal))
            {
                _toggleSuffix = !_toggleSuffix;
                announcement += _toggleSuffix ? "\u200B" : "\u200C";
            }

            AutomationProperties.SetName(_grid, announcement);
            ControlAutomationPeer.CreatePeerForElement(_grid)
                ?.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, oldName, announcement);
        }

        private string FormatRowText(object? item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return _formatRow != null ? _formatRow(item) : FormatItem(item);
        }

        private string FormatRowWithPosition(object item)
        {
            var rowText = FormatRowText(item);
            var index = _grid.SelectedIndex;
            var count = GetItemCount(_grid.ItemsSource);
            if (index >= 0 && count > 0)
            {
                return $"{rowText}, {index + 1}/{count}";
            }

            return rowText;
        }

        private string FormatCell(object item, DataGridColumn column)
        {
            var header = GetColumnHeader(column);
            var value = ExtractCellText(item, column);
            if (string.IsNullOrWhiteSpace(header))
            {
                return value;
            }

            return $"{header}: {(string.IsNullOrWhiteSpace(value) ? "-" : value)}";
        }

        private string ExtractCellText(object item, DataGridColumn column)
        {
            if (_realizedRows.TryGetValue(item, out var row))
            {
                var content = column.GetCellContent(row);
                var text = ExtractTextFromControl(content);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            var propName = column.Tag as string ?? column.SortMemberPath;
            if (!string.IsNullOrWhiteSpace(propName))
            {
                var prop = item.GetType().GetProperty(propName);
                if (prop != null)
                {
                    var val = prop.GetValue(item);
                    return val switch
                    {
                        null => string.Empty,
                        bool b => b ? ResUI.LvEnabled : "-",
                        _ => val.ToString() ?? string.Empty
                    };
                }
            }

            return string.Empty;
        }

        private static string ExtractTextFromControl(Control? control)
        {
            if (control == null)
            {
                return string.Empty;
            }

            switch (control)
            {
                case TextBlock tb:
                    return tb.Text ?? string.Empty;

                case CheckBox cb:
                    return cb.IsChecked == true ? ResUI.LvEnabled : "-";

                case Label lbl when lbl.IsVisible:
                    return lbl.Content?.ToString() ?? string.Empty;

                default:
                    {
                        var parts = new List<string>();
                        foreach (var visual in control.GetVisualDescendants())
                        {
                            if (visual is Control c && !c.IsVisible)
                            {
                                continue;
                            }

                            switch (visual)
                            {
                                case TextBlock descTb when !string.IsNullOrWhiteSpace(descTb.Text):
                                    parts.Add(descTb.Text);
                                    break;

                                case CheckBox descCb:
                                    parts.Add(descCb.IsChecked == true ? ResUI.LvEnabled : "-");
                                    break;

                                case Label descLbl when descLbl.Content != null:
                                    {
                                        var contentText = descLbl.Content.ToString();
                                        if (!string.IsNullOrWhiteSpace(contentText))
                                        {
                                            parts.Add(contentText);
                                        }
                                        break;
                                    }
                            }
                        }

                        return string.Join(" ", parts.Distinct(StringComparer.Ordinal));
                    }
            }
        }

        private static string GetColumnHeader(DataGridColumn column)
        {
            return column.Header switch
            {
                string s => s,
                TextBlock tb => tb.Text ?? string.Empty,
                _ => column.Header?.ToString() ?? string.Empty
            };
        }

        private static int GetItemCount(IEnumerable? itemsSource)
        {
            return itemsSource switch
            {
                null => 0,
                ICollection col => col.Count,
                _ => itemsSource.Cast<object>().Count()
            };
        }

        private static bool IsItemActive(object? item)
        {
            return item switch
            {
                ProfileItemModel p => p.IsActive,
                RoutingItem r => r.IsActive,
                _ => false
            };
        }
    }
}
