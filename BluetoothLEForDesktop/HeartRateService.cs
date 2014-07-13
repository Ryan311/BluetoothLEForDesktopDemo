using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace BluetoothLEForDesktop
{
	public sealed class HeartRateService : INotifyPropertyChanged, IDisposable
	{
		private GattDeviceService service_;
		private DateTime start_;
		private ObservableCollection<DeviceInformation> devices_ = new ObservableCollection<DeviceInformation>();
		private ObservableCollection<HeartRateMeasurement> datapoints_ = new ObservableCollection<HeartRateMeasurement>();
		private SynchronizationContext context_ = SynchronizationContext.Current;

		public HeartRateService()
		{
			this.StackingCount = 30;
		}

		public void Dispose()
		{
			if (service_ != null)
			{
				service_.Dispose();
				service_ = null;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public bool IsServiceInitialized
		{
			get;
			private set;
		}

		public int StackingCount
		{
			get;
			set;
		}

		public ObservableCollection<DeviceInformation> Devices
		{
			get
			{
				return devices_;
			}
		}

		public string BodySensorLocation
		{
			get;
			private set;
		}

		public ObservableCollection<HeartRateMeasurement> DataPoints
		{
			get
			{
				return datapoints_;
			}
		}

		private void OnPropertyChanged(string propertyName)
		{
			var propertyChanged = this.PropertyChanged;
			if (propertyChanged != null)
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public async Task FetchDevices()
		{
			devices_.Clear();

			var selector = GattDeviceService.GetDeviceSelectorFromUuid(GattServiceUuids.HeartRate);

			foreach (var device in await DeviceInformation.FindAllAsync(selector))
			{
				devices_.Add(device);
			}
		}

		public async Task InitializeServiceAsync(DeviceInformation deviceInformation)
		{
			try
			{
				service_ = await GattDeviceService.FromIdAsync(deviceInformation.Id);

				if (service_ == null)
				{
					//rootPage.NotifyUser("Access to the device is denied, because the application was not granted access, " +
					//	"or the device is currently in use by another application.",
					//	NotifyType.StatusMessage);
				}
				else
				{
					start_ = DateTime.Now;

					// The Heart Rate Profile specifies that the Heart Rate Service will contain a single 
					// Heart Rate Measurement Characteristic.
					var characteristics = service_.GetCharacteristics(GattCharacteristicUuids.HeartRateMeasurement);
					var characteristic = characteristics[0];

					// Register the event handler for receiving device notification data
					characteristic.ValueChanged += (sender, e) =>
						{
							var dataReader = DataReader.FromBuffer(e.CharacteristicValue);
							var data = new byte[e.CharacteristicValue.Length];
							dataReader.ReadBytes(data);

							// Process the raw data received from the device.
							var value = ProcessData(data);
							value.Timestamp = e.Timestamp - start_;

							context_.Post(nl =>
								{
									datapoints_.Add(value);
									while (datapoints_.Count > this.StackingCount)
									{
										datapoints_.RemoveAt(0);
									}
								}, null);
						};

					// Set the Client Characteristic Configuration descriptor on the device, 
					// registering for Characteristic Value Changed notifications
					var status =
						await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
						GattClientCharacteristicConfigurationDescriptorValue.Notify);

					if (status == GattCommunicationStatus.Unreachable)
					{
						//rootPage.NotifyUser("Your device is unreachable, most likely the device is out of range, " +
						//	"or is running low on battery, please make sure your device is working and try again.",
						//	NotifyType.StatusMessage);
					}
					else
					{
						this.IsServiceInitialized = true;

						this.OnPropertyChanged("IsServiceInitialized");
					}
				}
			}
			catch (Exception)
			{
				//rootPage.NotifyUser("ERROR: Accessing your device failed." + Environment.NewLine + e.Message,
				//	NotifyType.ErrorMessage);
			}
		}

		/// <summary>
		/// Reads the Body Sensor Location characteristic value.
		/// </summary>
		/// <param name="sender">The button that generated this action.</param>
		/// <param name="e"></param>
		public async Task FetchSensorLocationAsync()
		{
			try
			{
				var bodySensorLocationCharacteristics =
					service_.GetCharacteristics(GattCharacteristicUuids.BodySensorLocation);

				if (bodySensorLocationCharacteristics.Count > 0)
				{
					// Read the characteristic value
					GattReadResult readResult = await bodySensorLocationCharacteristics[0].ReadValueAsync();
					if (readResult.Status == GattCommunicationStatus.Success)
					{
						var dataReader = DataReader.FromBuffer(readResult.Value);
						byte[] bodySensorLocationData = new byte[readResult.Value.Length];
						dataReader.ReadBytes(bodySensorLocationData);

						string bodySensorLocation = ProcessBodySensorLocationData(bodySensorLocationData);
						if (string.IsNullOrWhiteSpace(bodySensorLocation) == false)
						{
							this.BodySensorLocation = bodySensorLocation;

							this.OnPropertyChanged("BodySensorLocation");
						}
					}
				}
			}
			catch (Exception)
			{
				//rootPage.NotifyUser("Error: " + exc.ToString(), NotifyType.ErrorMessage);
			}
		}

		/// <summary>
		/// Process the raw data read from the device into an application usable string, according to the Bluetooth
		/// Specification.
		/// </summary>
		/// <param name="bodySensorLocationData">Raw data read from the heart rate monitor.</param>
		/// <returns>The textual representation of the Body Sensor Location.</returns>
		private static string ProcessBodySensorLocationData(byte[] bodySensorLocationData)
		{
			// The Bluetooth Heart Rate Profile specifies that the Body Sensor Location characteristic value has
			// a single byte of data
			byte bodySensorLocationValue = bodySensorLocationData[0];
			string retval;

			retval = "";
			switch (bodySensorLocationValue)
			{
				case 0:
					retval += "Other";
					break;
				case 1:
					retval += "Chest";
					break;
				case 2:
					retval += "Wrist";
					break;
				case 3:
					retval += "Finger";
					break;
				case 4:
					retval += "Hand";
					break;
				case 5:
					retval += "Ear Lobe";
					break;
				case 6:
					retval += "Foot";
					break;
				default:
					retval = "";
					break;
			}
			return retval;
		}

		/// <summary>
		/// Process the raw data received from the device into application usable data, 
		/// according the the Bluetooth Heart Rate Profile.
		/// </summary>
		/// <param name="data">Raw data received from the heart rate monitor.</param>
		/// <returns>The heart rate measurement value.</returns>
		private static HeartRateMeasurement ProcessData(byte[] data)
		{
			// Heart Rate profile defined flag values
			const byte HEART_RATE_VALUE_FORMAT = 0x01;
			const byte ENERGY_EXPANDED_STATUS = 0x08;

			byte currentOffset = 0;
			byte flags = data[currentOffset];
			bool isHeartRateValueSizeLong = ((flags & HEART_RATE_VALUE_FORMAT) != 0);
			bool hasEnergyExpended = ((flags & ENERGY_EXPANDED_STATUS) != 0);

			currentOffset++;

			ushort heartRateMeasurementValue = 0;

			if (isHeartRateValueSizeLong)
			{
				heartRateMeasurementValue = (ushort)((data[currentOffset + 1] << 8) + data[currentOffset]);
				currentOffset += 2;
			}
			else
			{
				heartRateMeasurementValue = data[currentOffset];
				currentOffset++;
			}

			ushort expendedEnergyValue = 0;

			if (hasEnergyExpended)
			{
				expendedEnergyValue = (ushort)((data[currentOffset + 1] << 8) + data[currentOffset]);
				currentOffset += 2;
			}

			// The Heart Rate Bluetooth profile can also contain sensor contact status information,
			// and R-Wave interval measurements, which can also be processed here. 
			// For the purpose of this sample, we don't need to interpret that data.

			return new HeartRateMeasurement
			{
				HeartRateValue = heartRateMeasurementValue,
				ExpendedEnergy = expendedEnergyValue
			};

		}
	}
}
