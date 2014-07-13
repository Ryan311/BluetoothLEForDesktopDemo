using System;

namespace BluetoothLEForDesktop
{
	public class HeartRateMeasurement
	{
		public ushort HeartRateValue { get; set; }
		public ushort ExpendedEnergy { get; set; }
	
		public TimeSpan Timestamp { get; set; }

		public double OffsetTime
		{
			get
			{
				return (double)((int)(this.Timestamp.TotalMilliseconds) / 100) / 10.0;
			}
		}

		public override string ToString()
		{
			return string.Format("{0} bpm / {1} @ {2}", HeartRateValue, ExpendedEnergy, Timestamp);
		}
	}
}
