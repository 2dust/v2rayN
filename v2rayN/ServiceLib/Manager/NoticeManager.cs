namespace ServiceLib.Manager;

public class NoticeManager
{
    private static readonly Lazy<NoticeManager> _instance = new(() => new());
    public static NoticeManager Instance => _instance.Value;

    public void Enqueue(string? content)
    {
        if (content.IsNullOrEmpty())
        {
            return;
        }
        AppEvents.SendSnackMsgRequested.Publish(content);
    }

    public void SendMessage(string? content)
    {
        if (content.IsNullOrEmpty())
        {
            return;
        }
        AppEvents.SendMsgViewRequested.Publish(content);
    }

    public void SendMessageEx(string? content)
    {
        if (content.IsNullOrEmpty())
        {
            return;
        }
        content = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {content}";
        SendMessage(content);
    }

    public void SendMessageAndEnqueue(string? msg)
    {
        Enqueue(msg);
        SendMessage(msg);
    }
}
