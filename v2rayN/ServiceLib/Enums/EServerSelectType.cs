namespace ServiceLib.Enums;

/// <summary>
/// Which server of a group a workflow activates. The group uses the persisted sort
/// order (the same order the server list shows), so <see cref="First"/> is the topmost
/// row and lets a preceding sort decide the winner.
/// </summary>
public enum EServerSelectType
{
    /// <summary>Topmost server of the group, i.e. the first row as displayed.</summary>
    First = 1,

    /// <summary>Bottom server of the group, i.e. the last row as displayed.</summary>
    Last = 2,
}
