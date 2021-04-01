namespace EventStore.Core.LogAbstraction {
	public class LogV2Sizer : ISizer<string> {
		public int GetSizeInBytes(string t) => 2 * t.Length;
	}
}
