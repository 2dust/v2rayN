using System.Collections.Concurrent;

//using System.Reactive.Linq;

namespace ServiceLib.Handler
{
    public class ProfileExHandler
    {
        private static readonly Lazy<ProfileExHandler> _instance = new(() => new());
        private ConcurrentBag<ProfileExItem> _lstProfileEx = [];
        private Queue<string> _queIndexIds = new();
        public ConcurrentBag<ProfileExItem> ProfileExs => _lstProfileEx;
        public static ProfileExHandler Instance => _instance.Value;

        public ProfileExHandler()
        {
            Init();
        }

        private async Task Init()
        {
            await InitData();
            await Task.Run(async () =>
            {
                while (true)
                {
                    await SaveQueueIndexIds();
                    await Task.Delay(1000 * 600);
                }
            });
        }

        private async Task InitData()
        {
            await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileExItem where indexId not in ( select indexId from ProfileItem )");

            _lstProfileEx = new(await SQLiteHelper.Instance.TableAsync<ProfileExItem>().ToListAsync());
        }

        private void IndexIdEnqueue(string indexId)
        {
            if (Utils.IsNotEmpty(indexId) && !_queIndexIds.Contains(indexId))
            {
                _queIndexIds.Enqueue(indexId);
            }
        }

        private async Task SaveQueueIndexIds()
        {
            var cnt = _queIndexIds.Count;
            if (cnt > 0)
            {
                var lstExists = await SQLiteHelper.Instance.TableAsync<ProfileExItem>().ToListAsync();
                List<ProfileExItem> lstInserts = [];
                List<ProfileExItem> lstUpdates = [];

                for (int i = 0; i < cnt; i++)
                {
                    var id = _queIndexIds.Dequeue();
                    var item = lstExists.FirstOrDefault(t => t.IndexId == id);
                    var itemNew = _lstProfileEx?.FirstOrDefault(t => t.IndexId == id);
                    if (itemNew is null)
                    {
                        continue;
                    }

                    if (item is not null)
                    {
                        lstUpdates.Add(itemNew);
                    }
                    else
                    {
                        lstInserts.Add(itemNew);
                    }
                }
                try
                {
                    if (lstInserts.Count() > 0)
                        await SQLiteHelper.Instance.InsertAllAsync(lstInserts);

                    if (lstUpdates.Count() > 0)
                        await SQLiteHelper.Instance.UpdateAllAsync(lstUpdates);
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("ProfileExHandler", ex);
                }
            }
        }

        private void AddProfileEx(string indexId, ref ProfileExItem? profileEx)
        {
            profileEx = new()
            {
                IndexId = indexId,
                Delay = 0,
                Speed = 0,
                Sort = 0
            };
            _lstProfileEx.Add(profileEx);
            IndexIdEnqueue(indexId);
        }

        public async Task ClearAll()
        {
            await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileExItem ");
            _lstProfileEx = new();
        }

        public async Task SaveTo()
        {
            try
            {
                await SaveQueueIndexIds();
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public void SetTestDelay(string indexId, string delayVal)
        {
            var profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
            if (profileEx == null)
            {
                AddProfileEx(indexId, ref profileEx);
            }

            int.TryParse(delayVal, out int delay);
            profileEx.Delay = delay;
            IndexIdEnqueue(indexId);
        }

        public void SetTestSpeed(string indexId, string speedVal)
        {
            var profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
            if (profileEx == null)
            {
                AddProfileEx(indexId, ref profileEx);
            }

            decimal.TryParse(speedVal, out decimal speed);
            profileEx.Speed = speed;
            IndexIdEnqueue(indexId);
        }

        public void SetSort(string indexId, int sort)
        {
            var profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
            if (profileEx == null)
            {
                AddProfileEx(indexId, ref profileEx);
            }
            profileEx.Sort = sort;
            IndexIdEnqueue(indexId);
        }

        public int GetSort(string indexId)
        {
            var profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
            if (profileEx == null)
            {
                return 0;
            }
            return profileEx.Sort;
        }

        public int GetMaxSort()
        {
            if (_lstProfileEx.Count <= 0)
            {
                return 0;
            }
            return _lstProfileEx.Max(t => t == null ? 0 : t.Sort);
        }
    }
}