using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
	// This Class is for custom Responses
	public class Response
	{
		//Indecates with true or false if statuscode 2XX or not
		public bool Success { get; set; }

		//Message string is used for Weather output
		public string? Message { get; set; }
	}
}
