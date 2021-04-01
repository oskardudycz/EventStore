using EventStore.Common.Utils;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.LogAbstraction {
	public class LogFormatAbstractor {
		public static LogFormatAbstractor<string> V2 { get; }

		static LogFormatAbstractor() {
			var lookup = new LogV2StreamLookup();
			V2 = new LogFormatAbstractor<string>(
				new XXHashUnsafe(),
				new Murmur3AUnsafe(),
				lookup,
				lookup,
				new PreLoadedStreamLookupFactory<string>(lookup),
				new LogV2SystemStreams(),
				new LogV2StreamIdValidator(),
				new LogV2Sizer(),
				new LogV2RecordFactory());
		}
	}

	public class LogFormatAbstractor<TStreamId> : LogFormatAbstractor {
		public LogFormatAbstractor(
			IHasher<TStreamId> lowHasher,
			IHasher<TStreamId> highHasher,
			IStreamNameToIdReadWrite<TStreamId> streamIdsReadWrite,
			IStreamNameToId<TStreamId> streamIdsReadOnly,
			IStreamIdToNameFactory<TStreamId> streamNamesFactory,
			ISystemStreamLookup<TStreamId> systemStreams,
			IValidator<TStreamId> streamIdValidator,
			ISizer<TStreamId> streamIdSizer,
			IRecordFactory<TStreamId> recordFactory) {

			LowHasher = lowHasher;
			HighHasher = highHasher;
			StreamIdsReadWrite = streamIdsReadWrite;
			StreamIdsReadOnly = streamIdsReadOnly;
			StreamNamesFactory = streamNamesFactory;
			SystemStreams = systemStreams;
			StreamIdValidator = streamIdValidator;
			StreamIdSizer = streamIdSizer;
			RecordFactory = recordFactory;
		}

		public IHasher<TStreamId> LowHasher { get; }
		public IHasher<TStreamId> HighHasher { get; }
		public IStreamNameToIdReadWrite<TStreamId> StreamIdsReadWrite { get; }
		public IStreamNameToId<TStreamId> StreamIdsReadOnly { get; }
		public IStreamIdToNameFactory<TStreamId> StreamNamesFactory { get; } //qq rename
		public ISystemStreamLookup<TStreamId> SystemStreams { get; }
		public IValidator<TStreamId> StreamIdValidator { get; }
		public ISizer<TStreamId> StreamIdSizer { get; }
		public IRecordFactory<TStreamId> RecordFactory { get; }
	}
}
