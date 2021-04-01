namespace EventStore.Core.LogAbstraction {
	public class LogV2StreamLookup :
		IStreamNameToIdReadWrite<string>,
		IStreamNameToId<string>,
		IStreamIdToName<string> {

		public LogV2StreamLookup() {
		}

		public bool GetOrAddId(string streamName, out string streamId) {
			streamId = streamName;
			return true;
		}

		public string LookupId(string streamName) => streamName;
		public string LookupName(string streamId) => streamId;
	}
}
