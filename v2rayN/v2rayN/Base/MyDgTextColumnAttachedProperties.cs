using System.Windows;

namespace v2rayN.Base
{
	public static class MyDgTextColumnAttachedProperties
	{
		public static readonly DependencyProperty ExNameProperty =
			DependencyProperty.RegisterAttached("ExName", typeof(string), typeof(MyDgTextColumnAttachedProperties), new PropertyMetadata(default(string)));

		public static void SetExName(DependencyObject element, string value)
		{
			element.SetValue(ExNameProperty, value);
		}

		public static string GetExName(DependencyObject element)
		{
			return (string)element.GetValue(ExNameProperty);
		}
	}
}