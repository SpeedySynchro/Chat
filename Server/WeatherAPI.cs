using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Data;
using System.Globalization;

namespace Client
{
	public class WeatherAPI
	{
		private readonly HttpClient _httpClient = new HttpClient();
		private string errorCode= "Error: ";

		/// <summary>
		/// Combining Method to get the Weather as string. The number is used for interacting more directly ( maybe in the futur), but isn't used by now.
		/// </summary>
		public async Task<string> GetWeather(string adress, int? number)
		{
			// setting an Customized user Agent. Otherwise the api call for coordinates would be forbidden
			_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MYPUVSAPPGROUP2/Ver.0.1"); ;

			//Get the list of possible Coordinates of the adress
			List<NominatimResponse> nominatimResponse = await GetCoordinates(adress);
			
			//checking if Error
			if (nominatimResponse == null)
			{
				if (errorCode == null)
				{
					//If no errorCode then we are assuming the api gave us an empty array -> nothing found
					return "Der Ort konnte nicht gefunden werden. Bitte Überprüfen Sie ob Sie einen Existierenden und korrekt geschriebenen Ort eingegeben haben und probieren Sie es erneut!";
				}
				return errorCode;
			}
			// setting a retry string-> indicating a decision 
			string retryString = "retry";

			//If the nomatimResponse has 2 choices:
			if(nominatimResponse.Count() == 2)
			{
				//checking if the choices are the same -> 2 indentically Responses is a Bug that appeared on Dez. 12, 2024 
				if(nominatimResponse.First().display_name == nominatimResponse.Last().display_name)
				{
					// if there are the same, remove the last one
					nominatimResponse.Remove(nominatimResponse[1]);
				}
			}
			// checking if it allready is clear when there are multiple addresses which should be choosen
			if (number != null && number >0)
			{
				// The number exist in the list:
				if (number <= nominatimResponse.Count)
				{ 
					// cutting list to one entry
					nominatimResponse = new List<NominatimResponse> { nominatimResponse[number.Value-1] };
				}
				else // The number do not exist in the List
				{
					// return the given choices the user can do
					int count = 0;
					foreach (var response in nominatimResponse)
					{
						count++;
						retryString += $"[{count}]" + response.display_name + "\n";
					}
					return retryString;
				}
			}
			// If no number is set and the List of possible adresses has more than one entry:
			if ( nominatimResponse.Count > 1 )
			{
				// return the given choices the user can do
				int count = 0;
				foreach (var response in nominatimResponse)
				{
					count++;
					retryString += $"[{count}]" + response.display_name + "\n";
				}
				return retryString;
			}

			//extracting the coordinates
			Coordinates coordinates = new Coordinates();
			coordinates.latitude = nominatimResponse.First().lat;
			coordinates.longitude = nominatimResponse.First().lon;
			
			//Get the weather data of the coordinates
			WeatherResponse weatherResponse = await GetWeatherInformation(coordinates);
			// if error:
			if (weatherResponse == null)
			{
				return errorCode;
			}
			//setting up the return message with a string builder
			StringBuilder weatherReport = new StringBuilder();
			weatherReport.AppendLine("Aktuells Wetter:");
			weatherReport.AppendLine($" Zeit: {weatherResponse.current.time}");
			weatherReport.AppendLine($" Temperatur: {weatherResponse.current.temperature_2m} {weatherResponse.current_units.temperature_2m}");
			weatherReport.AppendLine($" Relative Luftfeuchtigkeit: {weatherResponse.current.relative_humidity_2m} {weatherResponse.current_units.relative_humidity_2m}");
			weatherReport.AppendLine($" Windgeschwindigkeit: {weatherResponse.current.wind_speed_10m} {weatherResponse.current_units.wind_speed_10m}");
			weatherReport.AppendLine(); weatherReport.AppendLine("Stündliche Vorhersage:");
			weatherReport.AppendLine($"{"Zeit",-25} {"Temperatur (2m)",-20} {"Relative Luftfeuchtigkeit (2m)",-30} {"Windgeschwindigkeit (10m)",-25}");

			for (int i = 0; i < weatherResponse.hourly.time.Count(); i++)
			{
				weatherReport.AppendLine($"{weatherResponse.hourly.time[i],-25} {weatherResponse.hourly.temperature_2m[i],-4} {weatherResponse.hourly_units.temperature_2m,-15} {weatherResponse.hourly.relative_humidity_2m[i],-4} {weatherResponse.hourly_units.relative_humidity_2m,-25} {weatherResponse.hourly.wind_speed_10m[i],-4} {weatherResponse.hourly_units.wind_speed_10m,-22}"); 
			} 

			return weatherReport.ToString(); 
		}



		// Methode to get the Coordinates of the given adress (over api)
		private async Task<List<NominatimResponse> > GetCoordinates(string adress)
		{
			try
			{
				var url = $"https://nominatim.openstreetmap.org/search?q={adress}&format=json";
				//api get Coordinates call
				HttpResponseMessage response = await _httpClient.GetAsync(url);


				if (response.IsSuccessStatusCode)
				{
					// transforming the response to a List of NominatimResponse Objects
					string responseBody = await response.Content.ReadAsStringAsync();
					return  JsonConvert.DeserializeObject<List<NominatimResponse>>(responseBody);	
				
				}
				else
				{
					// if the api call had no success -> save the error
					errorCode += response.StatusCode;
					return null;
				}
			}
			catch (Exception ex)
			{
				//if something went wrong save the error code 
				errorCode += ex.ToString();
				return null;
			}
		}
		// Methode to get the WeatherObjects of the given coordinates (over api)
		private async Task<WeatherResponse> GetWeatherInformation(Coordinates coordinates)
		{
			try
			{
				var url = $"https://api.open-meteo.com/v1/forecast?latitude={coordinates.latitude.ToString(CultureInfo.InvariantCulture)}&longitude={coordinates.longitude.ToString(CultureInfo.InvariantCulture)}&current=temperature_2m,relative_humidity_2m,wind_speed_10m&hourly=temperature_2m,relative_humidity_2m,wind_speed_10m";

				//api get weather call
				HttpResponseMessage response = await _httpClient.GetAsync(url);


				if (response.IsSuccessStatusCode)
				{
					// transforming the response to a WeatherResponse object and return
					string responseBody = await response.Content.ReadAsStringAsync();
					WeatherResponse weatherResponse = JsonConvert.DeserializeObject<WeatherResponse>(responseBody);
					return weatherResponse;
				}
				else
				{
					// if the api call had no success -> save the error
					errorCode += response.StatusCode;
					return null;
				}
			}
			catch (Exception ex)
			{
				//if something went wrong save the error code 
				errorCode += ex.ToString();
				return null;
			}
		}

	}
}
