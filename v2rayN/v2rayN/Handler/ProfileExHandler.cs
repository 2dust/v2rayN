using v2rayN.Base;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    class ProfileExHandler
    {
        private static readonly Lazy<ProfileExHandler> _instance = new(() => new());
        private List<ProfileExItem> _lstProfileEx;
        public List<ProfileExItem> ProfileExs => _lstProfileEx;
        public static ProfileExHandler Instance => _instance.Value;

        public ProfileExHandler()
        {
            Init();
        }

        private void Init()
        {
            SqliteHelper.Instance.Execute($"delete from ProfileExItem where indexId not in ( select indexId from ProfileItem )");

            _lstProfileEx = SqliteHelper.Instance.Table<ProfileExItem>().ToList();
        }

        private void AddProfileEx(string indexId, ref ProfileExItem profileEx)
        {
            profileEx = new()
            {
                indexId = indexId,
                delay = 0,
                speed = 0,
                sort = 0
            };
            _lstProfileEx.Add(profileEx);
            //SqliteHelper.Instance.Replace(profileEx);
        }

        public void ClearAll()
        {
            SqliteHelper.Instance.Execute($"delete from ProfileExItem ");
            _lstProfileEx = new();
        }

        public void SaveTo()
        {
            try
            {
                foreach (var item in _lstProfileEx)
                {
                    SqliteHelper.Instance.Replace(item);
                }
                //SqliteHelper.Instance.UpdateAll(_lstProfileEx);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        public Task SetTestDelay(string indexId, string delayVal)
        {
            var profileEx = _lstProfileEx.FirstOrDefault(t => t.indexId == indexId);
            if (profileEx == null)
            {
                AddProfileEx(indexId, ref profileEx);
            }

            int.TryParse(delayVal, out int delay);
            profileEx.delay = delay;
            return Task.CompletedTask;
        }

        public Task SetTestSpeed(string indexId, string speedVal)
        {
            var profileEx = _lstProfileEx.FirstOrDefault(t => t.indexId == indexId);
            if (profileEx == null)
            {
                AddProfileEx(indexId, ref profileEx);
            }

            decimal.TryParse(speedVal, out decimal speed);
            profileEx.speed = speed;
            return Task.CompletedTask;
        }

        public void SetSort(string indexId, int sort)
        {
            var profileEx = _lstProfileEx.FirstOrDefault(t => t.indexId == indexId);
            if (profileEx == null)
            {
                AddProfileEx(indexId, ref profileEx);
            }
            profileEx.sort = sort;
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
