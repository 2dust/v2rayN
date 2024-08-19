using ReactiveUI;
using v2rayN.Handler;

namespace v2rayN.Base
{
    public class MyReactiveObject : ReactiveObject
    {
        protected static Config? _config;
        protected Func<EViewAction, object?, bool>? _updateView;
        protected NoticeHandler? _noticeHandler;
    }
}