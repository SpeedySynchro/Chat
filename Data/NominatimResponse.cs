using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data
{
	public class NominatimResponse
	{
		public double place_id {  get; set; }
		public string licence { get; set; }
		public string osm_type	{ get; set; }
		public double osm_id { get; set; }
		public double lat { get; set; }
		public double lon { get; set; }
		public string @class { get;set;}
		public string type { get; set; }
		public string place_rank { get; set; }
		public string importance { get; set; }
		public string addresstype { get; set; }
		public string name { get; set; }
		public string display_name { get; set; }
		public List<double> boundingbox { get; set; }
	}
}
