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

            //_snackbarMessageQueue = snackbarMessageQueue;
        }

        public void Enqueue(object content)
        {
            _snackbarMessageQueue?.Enqueue(content);
        }

        public void SendMessage(string msg)
        {
            MessageBus.Current.SendMessage(msg, "MsgView");
        }

        public void SendMessage(string msg, bool time)
        {
            msg = $"{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")} {msg}";
            MessageBus.Current.SendMessage(msg, "MsgView");
        }
    }
}