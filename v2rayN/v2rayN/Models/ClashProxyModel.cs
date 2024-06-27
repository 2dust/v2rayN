using ReactiveUI.Fody.Helpers;

namespace v2rayN.Models
{
    [Serializable]
    public class ClashProxyModel
    {
        [Reactive]
        public string name { get; set; }

        [Reactive]
        public string type { get; set; }

        [Reactive]
        public string now { get; set; }

        [Reactive]
        public int delay { get; set; }

        [Reactive]
        public string delayName { get; set; }

        [Reactive]
        public bool isActive { get; set; }
    }
}