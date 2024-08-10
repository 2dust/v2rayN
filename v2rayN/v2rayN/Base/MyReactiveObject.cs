using ReactiveUI;
using v2rayN.Enums;
using v2rayN.Handler;
using v2rayN.Models;

namespace v2rayN.Base
{
    public class MyReactiveObject : ReactiveObject
    {
        protected static Config? _config;
        protected Func<EViewAction, bool>? _updateView;
        protected NoticeHandler? _noticeHandler;
    }
}