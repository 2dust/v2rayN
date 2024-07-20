using v2rayMiniConsole;

namespace v2rayN.Handler
{
    public class NoticeHandler
    {
        public void SendMessage(string msg)
        {
            if (RunningObjects.Instance.IsMessageOn())
            {
                Console.WriteLine($"message: {msg}");
            }            
        }

        public void SendMessage(string msg, bool time)
        {
            if (RunningObjects.Instance.IsMessageOn())
            {
                msg = $"{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")} {msg}";
                SendMessage(msg);
            }            
        }
    }
}