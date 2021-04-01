namespace EventStore.Core.LogAbstraction {
	//qq rename
	public class PreLoadedStreamLookupFactory<TStreamId> : IStreamIdToNameFactory<TStreamId> {
		private readonly IStreamIdToName<TStreamId> _streamIdToName;

		public PreLoadedStreamLookupFactory(IStreamIdToName<TStreamId> streamIdToName) {
			_streamIdToName = streamIdToName;
		}

		public IStreamIdToName<TStreamId> Create(object input = null) {
			return _streamIdToName;
		}
	}
}
