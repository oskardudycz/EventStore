namespace EventStore.Core.LogAbstraction {
	//qq rename
	public interface IStreamNameToIdReadWrite<TStreamId> {
		bool GetOrAddId(string streamName, out TStreamId streamId);
	}
}
