using MaterialDesignThemes.Wpf;
using ReactiveUI;

namespace v2rayN.Handler
{
    public class NoticeHandler
    {
        private readonly ISnackbarMessageQueue _snackbarMessageQueue;

        public NoticeHandler(ISnackbarMessageQueue snackbarMessageQueue)
        {
            _snackbarMessageQueue = snackbarMessageQueue ?? throw new ArgumentNullException(nameof(snackbarMessageQueue));
        }

        public void Enqueue(string content)
        {
            if (content.IsNullOrEmpty())
            {
                return;
            }
            _snackbarMessageQueue?.Enqueue(content);
        }

        public void SendMessage(string msg)
        {
            MessageBus.Current.SendMessage(msg, Global.CommandSendMsgView);
        }

        public void SendMessage(string msg, bool time)
        {
            msg = $"{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")} {msg}";
            SendMessage(msg);
        }

        public void SendMessageAndEnqueue(string msg)
        {
            Enqueue(msg);
            SendMessage(msg);
        }
    }
}