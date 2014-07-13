using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Devices.Enumeration;

namespace BluetoothLEForDesktop
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			var service = new HeartRateService();
			var task = service.FetchDevices();

			this.DataContext = service;
		}

		public void button_Click(object sender, RoutedEventArgs e)
		{
			var service = (HeartRateService)this.DataContext;
			var control = (FrameworkElement)e.Source;
			var device = (DeviceInformation)control.DataContext;

			var task = service.InitializeServiceAsync(device);
		}
	}
}
