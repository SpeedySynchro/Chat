using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data
{
	public class WeatherResponse
	{
		public double latitude {  get; set; }
		public double longitude { get; set; }
		public double generationtime_ms {  get; set; }
		public double utc_offset_seconds { get; set; }
		public string timezone { get; set; }
		public string timezone_abbreviation { get; set; }
		public double elevation { get; set; }
		public WeatherData current_units { get; set; }
		public WeatherData current {  get; set; }
		public HourlyUnits hourly_units { get; set; }
		public HourlyData hourly { get; set; }
	}
}
