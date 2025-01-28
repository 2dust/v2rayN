using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace ServiceLib.ViewModels
{
	public class SubEditViewModel : MyReactiveObject
	{
		[Reactive]
		public SubItem SelectedSource { get; set; }

		public ReactiveCommand<Unit, Unit> SaveCmd { get; }

		public SubEditViewModel(SubItem subItem, Func<EViewAction, object?, Task<bool>>? updateView)
		{
			_config = AppHandler.Instance.Config;
			_updateView = updateView;

			SaveCmd = ReactiveCommand.CreateFromTask(async () =>
			{
				await SaveSubAsync();
			});

			SelectedSource = subItem.Id.IsNullOrEmpty() ? subItem : JsonUtils.DeepCopy(subItem);
		}

		private async Task SaveSubAsync()
		{
			var remarks = SelectedSource.Remarks;
			if (Utils.IsNullOrEmpty(remarks))
			{
				NoticeHandler.Instance.Enqueue(ResUI.PleaseFillRemarks);
				return;
			}

			var url = SelectedSource.Url;
			if (url.IsNotEmpty())
			{
				var uri = Utils.TryUri(url);
				if (uri == null)
				{
					NoticeHandler.Instance.Enqueue(ResUI.InvalidUrlTip);
					return;
				}
				//Do not allow http protocol
				if (url.StartsWith(Global.HttpProtocol) && !Utils.IsPrivateNetwork(uri.IdnHost))
				{
					NoticeHandler.Instance.Enqueue(ResUI.InsecureUrlProtocol);
					//return;
				}
			}

			if (await ConfigHandler.AddSubItem(_config, SelectedSource) == 0)
			{
				NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
				_updateView?.Invoke(EViewAction.CloseWindow, null);
			}
			else
			{
				NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
			}
		}
	}
}
