namespace EventStore.Core.LogAbstraction {
	public interface IStreamIdToName<TStreamId> {
		string LookupName(TStreamId streamId);
	}
}
