using SQLite;

namespace ServiceLib.Models;

[Serializable]
public class ProfileGroupItem
{
    [PrimaryKey]
    public string ParentIndexId { get; set; }

    public string ChildItems { get; set; }

    public EMultipleLoad MultipleLoad { get; set; } = EMultipleLoad.LeastPing;

    public bool HasCycle()
    {
        return HasCycle(new HashSet<string>(), new HashSet<string>());
    }

    public bool HasCycle(HashSet<string> visited, HashSet<string> stack)
    {
        if (string.IsNullOrEmpty(ParentIndexId))
            return false;

        if (stack.Contains(ParentIndexId))
            return true;

        if (visited.Contains(ParentIndexId))
            return false;

        visited.Add(ParentIndexId);
        stack.Add(ParentIndexId);

        if (string.IsNullOrEmpty(ChildItems))
        {
            return false;
        }

        var childIds = Utils.String2List(ChildItems)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        var childProfiles = childIds.Select(ProfileGroupItemManager.Instance.GetOrDefault)//这里是内存访问
            .Where(p => p != null)
            .ToList();

        foreach (var child in childProfiles)
        {
            if (child.HasCycle(visited, stack))
            {
                return true;
            }
        }

        stack.Remove(ParentIndexId);
        return false;
    }
}
