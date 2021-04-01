namespace EventStore.Core.LogAbstraction {
	public interface IStreamNameToId<TStreamId> {
		TStreamId LookupId(string streamName);
	}
}
