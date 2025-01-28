using System.Collections;

namespace ServiceLib.Models
{
	internal class V2rayMetricsVars
	{
		public V2rayMetricsVarsStats? stats { get; set; }
	}
}

public class V2rayMetricsVarsStats
{
	public Hashtable? outbound { get; set; }
}

public class V2rayMetricsVarsLink
{
	public long downlink { get; set; }
	public long uplink { get; set; }
}
