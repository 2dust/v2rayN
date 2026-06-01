using System.Windows.Controls;

namespace v2rayN.Base;

/// <summary>
/// Exposes the persisted column key (<see cref="ExName"/>) so column width and order can be saved
/// and restored uniformly for both text and template columns in the profiles grid.
/// </summary>
internal interface IMyDGColumn
{
    string ExName { get; set; }
}

/// <summary>
/// A text column that carries a stable <see cref="ExName"/> key for layout persistence and sorting.
/// </summary>
internal class MyDGTextColumn : DataGridTextColumn, IMyDGColumn
{
    public string ExName { get; set; }
}

/// <summary>
/// A template column variant of <see cref="MyDGTextColumn"/>; used for cells that host custom
/// content (such as the SVG flag next to the IP info) while keeping the same persistence key.
/// </summary>
internal class MyDGTemplateColumn : DataGridTemplateColumn, IMyDGColumn
{
    public string ExName { get; set; }
}
