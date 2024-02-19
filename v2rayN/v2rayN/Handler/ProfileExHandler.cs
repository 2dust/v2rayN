using System.Collections.Concurrent;
using System.Reactive.Linq;
using v2rayN.Model;

namespace v2rayN.Handler
{
    internal class ProfileExHandler
    {
        private static readonly Lazy<ProfileExHandler> _instance = new(() => new());
        private ConcurrentBag<ProfileExItem> _lstProfileEx;
        private Queue<string> _queIndexIds = new();
        public ConcurrentBag<ProfileExItem> ProfileExs => _lstProfileEx;
        public static ProfileExHandler Instance => _instance.Value;

        public ProfileExHandler()
        {
            Init();
        }

        private void Init()
        {
            SQLiteHelper.Instance.Execute($"delete from ProfileExItem where indexId not in ( select indexId from ProfileItem )");

            _lstProfileEx = new(SQLiteHelper.Instance.Table<ProfileExItem>());

            Task.Run(async () =>
            {
                while (true)
                {
                    var cnt = _queIndexIds.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        var id = _queIndexIds.Dequeue();
                        var item = _lstProfileEx.FirstOrDefault(t => t.indexId == id);
                        if (item is not null)
                        {
                            SQLiteHelper.Instance.Replace(item);
                        }
                    }
                    await Task.Delay(1000 * 60);
                }
            });
        }

        private void IndexIdEnqueue(string indexId)
        {
            if (!Utile.IsNullOrEmpty(indexId) && !_queIndexIds.Contains(indexId))
            {
                _queIndexIds.Enqueue(indexId);
            }
        }

        private void AddProfileEx(string indexId, ref ProfileExItem? profileEx)
        {
            profileEx = new()
            {
                indexId = indexId,
                delay = 0,
                speed = 0,
                sort = 0
            };
            _lstProfileEx.Add(profileEx);
            IndexIdEnqueue(indexId);
        }

        public void ClearAll()
        {
            SQLiteHelper.Instance.Execute($"delete from ProfileExItem ");
            _lstProfileEx = new();
        }

        public void SaveTo()
        {
            try
            {
                //foreach (var item in _lstProfileEx)
                //{
                //    SQLiteHelper.Instance.Replace(item);
                //}
                SQLiteHelper.Instance.UpdateAll(_lstProfileEx);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public void SetTestDelay(string indexId, string delayVal)
        {
            var profileEx = _lstProfileEx.FirstOrDefault(t => t.indexId == indexId);
            if (profileEx == null)
            {
                AddProfileEx(indexId, ref profileEx);
            }

            int.TryParse(delayVal, out int delay);
            profileEx.delay = delay;
            IndexIdEnqueue(indexId);
        }

        public void SetTestSpeed(string indexId, string speedVal)
        {
            var profileEx = _lstProfileEx.FirstOrDefault(t => t.indexId == indexId);
            if (profileEx == null)
            {
                AddProfileEx(indexId, ref profileEx);
            }

            decimal.TryParse(speedVal, out decimal speed);
            profileEx.speed = speed;
            IndexIdEnqueue(indexId);
        }

        public void SetSort(string indexId, int sort)
        {
            var profileEx = _lstProfileEx.FirstOrDefault(t => t.indexId == indexId);
            if (profileEx == null)
            {
                AddProfileEx(indexId, ref profileEx);
            }
            profileEx.sort = sort;
            IndexIdEnqueue(indexId);
        }

        public int GetSort(string indexId)
        {
            var profileEx = _lstProfileEx.FirstOrDefault(t => t.indexId == indexId);
            if (profileEx == null)
            {
                return 0;
            }
            return profileEx.sort;
        }

        public int GetMaxSort()
        {
            if (_lstProfileEx.Count <= 0)
            {
                return 0;
            }
            return _lstProfileEx.Max(t => t == null ? 0 : t.sort);
        }
    }
}