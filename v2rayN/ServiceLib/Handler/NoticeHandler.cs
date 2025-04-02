using ReactiveUI;

namespace ServiceLib.Handler;

public class NoticeHandler
{
    private static readonly Lazy<NoticeHandler> _instance = new(() => new());
    public static NoticeHandler Instance => _instance.Value;

    public void Enqueue(string? content)
    {
        if (content.IsNullOrEmpty())
        {
            return;
        }
        MessageBus.Current.SendMessage(content, EMsgCommand.SendSnackMsg.ToString());
    }

    public void SendMessage(string? content)
    {
        if (content.IsNullOrEmpty())
        {
            return;
        }
        MessageBus.Current.SendMessage(content, EMsgCommand.SendMsgView.ToString());
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
