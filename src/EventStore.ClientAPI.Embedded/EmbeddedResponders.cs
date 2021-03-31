using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Data;
using EventStore.Core.Messages;

namespace EventStore.ClientAPI.Embedded {
	internal static class EmbeddedResponders {
		internal class AppendToStream :
			EmbeddedResponderBase<WriteResult, ClientMessage.WriteEventsCompleted> {
			private readonly long _expectedVersion;
			private readonly string _stream;

			public AppendToStream(TaskCompletionSource<WriteResult> source, string stream, long expectedVersion)
				: base(source) {
				_stream = stream;
				_expectedVersion = expectedVersion;
			}

			protected override void InspectResponse(ClientMessage.WriteEventsCompleted response) {
				switch (response.Result) {
					case OperationResult.Success:
						Succeed(response);
						break;
					case OperationResult.PrepareTimeout:
					case OperationResult.ForwardTimeout:
					case OperationResult.CommitTimeout:
						break;
					case OperationResult.WrongExpectedVersion:
						var err = string.Format(
							"Append failed due to WrongExpectedVersion. Stream: {0}, Expected version: {1}", _stream,
							_expectedVersion);
						Fail(new WrongExpectedVersionException(err, _expectedVersion, response.CurrentVersion));
						break;
					case OperationResult.StreamDeleted:
						Fail(new StreamDeletedException(_stream));
						break;
					case OperationResult.InvalidTransaction:
						Fail(new InvalidTransactionException());
						break;
					case OperationResult.AccessDenied:
						Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.",
							_stream)));
						break;
					default:
						throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
				}
			}

			protected override WriteResult TransformResponse(ClientMessage.WriteEventsCompleted response) {
				return new WriteResult(response.LastEventNumber,
					new Position(response.CommitPosition, response.PreparePosition));
			}
		}

		internal class ConditionalAppendToStream :
			EmbeddedResponderBase<ConditionalWriteResult, ClientMessage.WriteEventsCompleted> {
			private readonly string _stream;

			public ConditionalAppendToStream(TaskCompletionSource<ConditionalWriteResult> source, string stream)
				: base(source) {
				_stream = stream;
			}

			protected override void InspectResponse(ClientMessage.WriteEventsCompleted response) {
				switch (response.Result) {
					case OperationResult.Success:
						Succeed(response);
						break;
					case OperationResult.PrepareTimeout:
					case OperationResult.ForwardTimeout:
					case OperationResult.CommitTimeout:
						break;
					case OperationResult.WrongExpectedVersion:
						Succeed(response);
						break;
					case OperationResult.StreamDeleted:
						Succeed(response);
						break;
					case OperationResult.InvalidTransaction:
						Fail(new InvalidTransactionException());
						break;
					case OperationResult.AccessDenied:
						Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.",
							_stream)));
						break;
					default:
						throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
				}
			}

			protected override ConditionalWriteResult TransformResponse(ClientMessage.WriteEventsCompleted response) {
				if (response.Result == OperationResult.WrongExpectedVersion) {
					return new ConditionalWriteResult(ConditionalWriteStatus.VersionMismatch);
				}

				if (response.Result == OperationResult.StreamDeleted) {
					return new ConditionalWriteResult(ConditionalWriteStatus.StreamDeleted);
				}

				return new ConditionalWriteResult(response.LastEventNumber,
					new Position(response.CommitPosition, response.PreparePosition));
			}
		}

		internal class DeleteStream :
			EmbeddedResponderBase<DeleteResult, ClientMessage.DeleteStreamCompleted> {
			private readonly long _expectedVersion;
			private readonly string _stream;

			public DeleteStream(TaskCompletionSource<DeleteResult> source, string stream, long expectedVersion) :
				base(source) {
				_stream = stream;
				_expectedVersion = expectedVersion;
			}

			protected override void InspectResponse(
				ClientMessage.DeleteStreamCompleted response) {
				switch (response.Result) {
					case OperationResult.Success:
						Succeed(response);
						break;
					case OperationResult.PrepareTimeout:
					case OperationResult.CommitTimeout:
					case OperationResult.ForwardTimeout:
						break;
					case OperationResult.WrongExpectedVersion:
						var err =
							string.Format(
								"Delete stream failed due to WrongExpectedVersion. Stream: {0}, Expected version: {1}.",
								_stream, _expectedVersion);
						Fail(new WrongExpectedVersionException(err));
						break;
					case OperationResult.StreamDeleted:
						Fail(new StreamDeletedException(_stream));
						break;
					case OperationResult.InvalidTransaction:
						Fail(new InvalidTransactionException());
						break;
					case OperationResult.AccessDenied:
						Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.",
							_stream)));
						break;
					default:
						throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
				}
			}

			protected override DeleteResult TransformResponse(ClientMessage.DeleteStreamCompleted response) {
				return new DeleteResult(new Position(response.CommitPosition, response.PreparePosition));
			}
		}

		internal class ReadAllEventsBackward :
			EmbeddedResponderBase<AllEventsSlice, ClientMessage.ReadAllEventsBackwardCompleted> {
			public ReadAllEventsBackward(TaskCompletionSource<AllEventsSlice> source)
				: base(source) {
			}

			protected override void InspectResponse(ClientMessage.ReadAllEventsBackwardCompleted response) {
				switch (response.Result) {
					case ReadAllResult.Success:
						Succeed(response);
						break;
					case ReadAllResult.Error:
						Fail(new ServerErrorException(string.IsNullOrEmpty(response.Error)
							? "<no message>"
							: response.Error));
						break;
					case ReadAllResult.AccessDenied:
						Fail(new AccessDeniedException("Read access denied for $all."));
						break;
					default:
						throw new Exception(string.Format("Unexpected ReadAllResult: {0}.", response.Result));
				}
			}

			protected override AllEventsSlice TransformResponse(
				ClientMessage.ReadAllEventsBackwardCompleted response) {
				return new AllEventsSlice(ReadDirection.Backward,
					new Position(response.CurrentPos.CommitPosition, response.CurrentPos.PreparePosition),
					new Position(response.NextPos.CommitPosition, response.NextPos.PreparePosition),
					response.Events.ConvertToClientResolvedEvents());
			}
		}

		internal class ReadAllEventsForward :
			EmbeddedResponderBase<AllEventsSlice, ClientMessage.ReadAllEventsForwardCompleted> {
			public ReadAllEventsForward(TaskCompletionSource<AllEventsSlice> source) : base(source) {
			}

			protected override void InspectResponse(ClientMessage.ReadAllEventsForwardCompleted response) {
				switch (response.Result) {
					case ReadAllResult.Success:
						Succeed(response);
						break;
					case ReadAllResult.Error:
						Fail(new ServerErrorException(string.IsNullOrEmpty(response.Error)
							? "<no message>"
							: response.Error));
						break;
					case ReadAllResult.AccessDenied:
						Fail(new AccessDeniedException("Read access denied for $all."));
						break;
					default:
						throw new Exception(string.Format("Unexpected ReadAllResult: {0}.", response.Result));
				}
			}

			protected override AllEventsSlice TransformResponse(
				ClientMessage.ReadAllEventsForwardCompleted response) {
				return new AllEventsSlice(ReadDirection.Forward,
					new Position(response.CurrentPos.CommitPosition, response.CurrentPos.PreparePosition),
					new Position(response.NextPos.CommitPosition, response.NextPos.PreparePosition),
					response.Events.ConvertToClientResolvedEvents());
			}
		}
		
		internal class FilteredReadAllEventsForward :
			EmbeddedResponderBase<AllEventsSlice, ClientMessage.FilteredReadAllEventsForwardCompleted> {
			public FilteredReadAllEventsForward(TaskCompletionSource<AllEventsSlice> source) : base(source) {
			}

			protected override void InspectResponse(ClientMessage.FilteredReadAllEventsForwardCompleted response) {
				switch (response.Result) {
					case FilteredReadAllResult.Success:
						Succeed(response);
						break;
					case FilteredReadAllResult.Error:
						Fail(new ServerErrorException(string.IsNullOrEmpty(response.Error)
							? "<no message>"
							: response.Error));
						break;
					case FilteredReadAllResult.AccessDenied:
						Fail(new AccessDeniedException("Read access denied for $all."));
						break;
					default:
						throw new Exception(string.Format("Unexpected ReadAllResult: {0}.", response.Result));
				}
			}

			protected override AllEventsSlice TransformResponse(
				ClientMessage.FilteredReadAllEventsForwardCompleted response) {
				return new AllEventsSlice(ReadDirection.Forward,
					new Position(response.CurrentPos.CommitPosition, response.CurrentPos.PreparePosition),
					new Position(response.NextPos.CommitPosition, response.NextPos.PreparePosition),
					response.Events.ConvertToClientResolvedEvents(), response.IsEndOfStream);
			}
		}
		
		internal class FilteredReadAllEventsBackward :
			EmbeddedResponderBase<AllEventsSlice, ClientMessage.FilteredReadAllEventsBackwardCompleted> {
			public FilteredReadAllEventsBackward(TaskCompletionSource<AllEventsSlice> source) : base(source) {
			}

			protected override void InspectResponse(ClientMessage.FilteredReadAllEventsBackwardCompleted response) {
				switch (response.Result) {
					case FilteredReadAllResult.Success:
						Succeed(response);
						break;
					case FilteredReadAllResult.Error:
						Fail(new ServerErrorException(string.IsNullOrEmpty(response.Error)
							? "<no message>"
							: response.Error));
						break;
					case FilteredReadAllResult.AccessDenied:
						Fail(new AccessDeniedException("Read access denied for $all."));
						break;
					default:
						throw new Exception(string.Format("Unexpected ReadAllResult: {0}.", response.Result));
				}
			}

			protected override AllEventsSlice TransformResponse(
				ClientMessage.FilteredReadAllEventsBackwardCompleted response) {
				return new AllEventsSlice(ReadDirection.Backward,
					new Position(response.CurrentPos.CommitPosition, response.CurrentPos.PreparePosition),
					new Position(response.NextPos.CommitPosition, response.NextPos.PreparePosition),
					response.Events.ConvertToClientResolvedEvents(), response.IsEndOfStream);
			}
		}

		internal class ReadEvent :
			EmbeddedResponderBase<EventReadResult, ClientMessage.ReadEventCompleted> {
			private readonly long _eventNumber;
			private readonly string _stream;

			public ReadEvent(TaskCompletionSource<EventReadResult> source, string stream, long eventNumber)
				: base(source) {
				_stream = stream;
				_eventNumber = eventNumber;
			}

			protected override void InspectResponse(ClientMessage.ReadEventCompleted response) {
				switch (response.Result) {
					case ReadEventResult.Success:
					case ReadEventResult.NotFound:
					case ReadEventResult.NoStream:
					case ReadEventResult.StreamDeleted:
						Succeed(response);
						return;
					case ReadEventResult.Error:
						Fail(new ServerErrorException(string.IsNullOrEmpty(response.Error)
							? "<no message>"
							: response.Error));
						return;
					case ReadEventResult.AccessDenied:
						Fail(new AccessDeniedException(string.Format("Read access denied for stream '{0}'.", _stream)));
						return;
					default:
						throw new Exception(string.Format("Unexpected ReadEventResult: {0}.", response.Result));
				}
			}

			protected override EventReadResult TransformResponse(ClientMessage.ReadEventCompleted response) {
				return new EventReadResult(Convert(response.Result), _stream, _eventNumber,
					response.Record.ConvertToClientResolvedIndexEvent());
			}


			private static EventReadStatus Convert(ReadEventResult result) {
				switch (result) {
					case ReadEventResult.Success:
						return EventReadStatus.Success;
					case ReadEventResult.NotFound:
						return EventReadStatus.NotFound;
					case ReadEventResult.NoStream:
						return EventReadStatus.NoStream;
					case ReadEventResult.StreamDeleted:
						return EventReadStatus.StreamDeleted;
					default:
						throw new Exception(string.Format("Unexpected ReadEventResult: {0}.", result));
				}
			}
		}

		internal class ReadStreamEventsBackward :
			EmbeddedResponderBase<StreamEventsSlice, ClientMessage.ReadStreamEventsBackwardCompleted> {
			private readonly long _fromEventNumber;
			private readonly string _stream;

			public ReadStreamEventsBackward(TaskCompletionSource<StreamEventsSlice> source, string stream,
				long fromEventNumber)
				: base(source) {
				_stream = stream;
				_fromEventNumber = fromEventNumber;
			}

			protected override void InspectResponse(ClientMessage.ReadStreamEventsBackwardCompleted response) {
				switch (response.Result) {
					case ReadStreamResult.Success:
					case ReadStreamResult.StreamDeleted:
					case ReadStreamResult.NoStream:
						Succeed(response);
						break;
					case ReadStreamResult.Error:
						Fail(new ServerErrorException(string.IsNullOrEmpty(response.Error)
							? "<no message>"
							: response.Error));
						break;
					case ReadStreamResult.AccessDenied:
						Fail(new AccessDeniedException(string.Format("Read access denied for stream '{0}'.", _stream)));
						break;
					default:
						throw new Exception(string.Format("Unexpected ReadStreamResult: {0}.", response.Result));
				}
			}

			protected override StreamEventsSlice TransformResponse(
				ClientMessage.ReadStreamEventsBackwardCompleted response) {
				return new StreamEventsSlice(Convert(response.Result),
					_stream,
					_fromEventNumber,
					ReadDirection.Backward,
					response.Events.ConvertToClientResolvedIndexEvents(),
					response.NextEventNumber,
					response.LastEventNumber,
					response.IsEndOfStream);
			}

			SliceReadStatus Convert(ReadStreamResult result) {
				switch (result) {
					case ReadStreamResult.Success:
						return SliceReadStatus.Success;
					case ReadStreamResult.NoStream:
						return SliceReadStatus.StreamNotFound;
					case ReadStreamResult.StreamDeleted:
						return SliceReadStatus.StreamDeleted;
					default:
						throw new Exception(string.Format("Unexpected ReadStreamResult: {0}.", result));
				}
			}
		}

		internal class ReadStreamForwardEvents :
			EmbeddedResponderBase<StreamEventsSlice, ClientMessage.ReadStreamEventsForwardCompleted> {
			private readonly long _fromEventNumber;
			private readonly string _stream;

			public ReadStreamForwardEvents(TaskCompletionSource<StreamEventsSlice> source, string stream,
				long fromEventNumber) : base(source) {
				_stream = stream;
				_fromEventNumber = fromEventNumber;
			}

			protected override void InspectResponse(ClientMessage.ReadStreamEventsForwardCompleted response) {
				switch (response.Result) {
					case ReadStreamResult.Success:
					case ReadStreamResult.StreamDeleted:
					case ReadStreamResult.NoStream:
						Succeed(response);
						break;
					case ReadStreamResult.Error:
						Fail(new ServerErrorException(string.IsNullOrEmpty(response.Error)
							? "<no message>"
							: response.Error));
						break;
					case ReadStreamResult.AccessDenied:
						Fail(new AccessDeniedException(string.Format("Read access denied for stream '{0}'.", _stream)));
						break;
					default:
						throw new Exception(string.Format("Unexpected ReadStreamResult: {0}.", response.Result));
				}
			}

			protected override StreamEventsSlice TransformResponse(
				ClientMessage.ReadStreamEventsForwardCompleted response) {
				return new StreamEventsSlice(Convert(response.Result),
					_stream,
					_fromEventNumber,
					ReadDirection.Forward,
					response.Events.ConvertToClientResolvedIndexEvents(),
					response.NextEventNumber,
					response.LastEventNumber,
					response.IsEndOfStream);
			}

			SliceReadStatus Convert(ReadStreamResult result) {
				switch (result) {
					case ReadStreamResult.Success:
						return SliceReadStatus.Success;
					case ReadStreamResult.NoStream:
						return SliceReadStatus.StreamNotFound;
					case ReadStreamResult.StreamDeleted:
						return SliceReadStatus.StreamDeleted;
					default:
						throw new Exception(string.Format("Unexpected ReadStreamResult: {0}.", result));
				}
			}
		}

		internal class TransactionCommit :
			EmbeddedResponderBase<WriteResult, ClientMessage.TransactionCommitCompleted> {
			public TransactionCommit(TaskCompletionSource<WriteResult> source)
				: base(source) {
			}

			protected override void InspectResponse(ClientMessage.TransactionCommitCompleted response) {
				switch (response.Result) {
					case OperationResult.Success:
						Succeed(response);
						break;
					case OperationResult.PrepareTimeout:
					case OperationResult.CommitTimeout:
					case OperationResult.ForwardTimeout:
						break;
					case OperationResult.WrongExpectedVersion:
						var err = string.Format(
							"Commit transaction failed due to WrongExpectedVersion. TransactionID: {0}.",
							response.TransactionId);
						Fail(new WrongExpectedVersionException(err));
						break;
					case OperationResult.StreamDeleted:
						Fail(new StreamDeletedException());
						break;
					case OperationResult.InvalidTransaction:
						Fail(new InvalidTransactionException());
						break;
					case OperationResult.AccessDenied:
						Fail(new AccessDeniedException("Write access denied."));
						break;
					default:
						throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
				}
			}

			protected override WriteResult TransformResponse(ClientMessage.TransactionCommitCompleted response) {
				return new WriteResult(response.LastEventNumber,
					new Position(response.CommitPosition, response.PreparePosition));
			}
		}

		internal class TransactionStart :
			EmbeddedResponderBase<EventStoreTransaction, ClientMessage.TransactionStartCompleted> {
			private readonly long _expectedVersion;
			private readonly IEventStoreTransactionConnection _parentConnection;
			private readonly string _stream;

			public TransactionStart(TaskCompletionSource<EventStoreTransaction> source,
				IEventStoreTransactionConnection parentConnection, string stream, long expectedVersion) : base(source) {
				_parentConnection = parentConnection;
				_stream = stream;
				_expectedVersion = expectedVersion;
			}

			protected override void InspectResponse(ClientMessage.TransactionStartCompleted response) {
				switch (response.Result) {
					case OperationResult.Success:
						Succeed(response);
						break;
					case OperationResult.PrepareTimeout:
					case OperationResult.CommitTimeout:
					case OperationResult.ForwardTimeout:
						break;
					case OperationResult.WrongExpectedVersion:
						var err = string.Format(
							"Start transaction failed due to WrongExpectedVersion. Stream: {0}, Expected version: {1}.",
							_stream, _expectedVersion);
						Fail(new WrongExpectedVersionException(err));
						break;
					case OperationResult.StreamDeleted:
						Fail(new StreamDeletedException(_stream));
						break;
					case OperationResult.InvalidTransaction:
						Fail(new InvalidTransactionException());
						break;
					case OperationResult.AccessDenied:
						Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.",
							_stream)));
						break;
					default:
						throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
				}
			}

			protected override EventStoreTransaction
				TransformResponse(ClientMessage.TransactionStartCompleted response) {
				return new EventStoreTransaction(response.TransactionId, null, _parentConnection);
			}
		}

		internal class TransactionWrite :
			EmbeddedResponderBase<EventStoreTransaction, ClientMessage.TransactionWriteCompleted> {
			private readonly IEventStoreTransactionConnection _parentConnection;

			public TransactionWrite(TaskCompletionSource<EventStoreTransaction> source,
				IEventStoreTransactionConnection parentConnection)
				: base(source) {
				_parentConnection = parentConnection;
			}

			protected override void InspectResponse(ClientMessage.TransactionWriteCompleted response) {
				switch (response.Result) {
					case OperationResult.Success:
						Succeed(response);
						break;
					case OperationResult.PrepareTimeout:
					case OperationResult.CommitTimeout:
					case OperationResult.ForwardTimeout:
						break;
					case OperationResult.AccessDenied:
						Fail(new AccessDeniedException(string.Format("Write access denied.")));
						break;
					default:
						throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
				}
			}

			protected override EventStoreTransaction
				TransformResponse(ClientMessage.TransactionWriteCompleted response) {
				return new EventStoreTransaction(response.TransactionId, null, _parentConnection);
			}
		}

		internal class CreatePersistentSubscription :
			EmbeddedResponderBase<PersistentSubscriptionCreateResult,
				ClientMessage.CreatePersistentSubscriptionToStreamCompleted> {
			private readonly string _stream;
			private readonly string _groupName;

			public CreatePersistentSubscription(TaskCompletionSource<PersistentSubscriptionCreateResult> source,
				string stream, string groupName)
				: base(source) {
				_groupName = groupName;
				_stream = stream;
			}

			protected override void InspectResponse(ClientMessage.CreatePersistentSubscriptionToStreamCompleted response) {
				switch (response.Result) {
					case ClientMessage.CreatePersistentSubscriptionToStreamCompleted.CreatePersistentSubscriptionToStreamResult.Success:
						Succeed(response);
						break;
					case ClientMessage.CreatePersistentSubscriptionToStreamCompleted.CreatePersistentSubscriptionToStreamResult.Fail:
						Fail(new InvalidOperationException(String.Format(
							"Subscription group {0} on stream {1} failed '{2}'", _groupName, _stream,
							response.Reason)));
						break;
					case ClientMessage.CreatePersistentSubscriptionToStreamCompleted.CreatePersistentSubscriptionToStreamResult
						.AccessDenied:
						Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.",
							_stream)));
						break;
					case ClientMessage.CreatePersistentSubscriptionToStreamCompleted.CreatePersistentSubscriptionToStreamResult
						.AlreadyExists:
						Fail(new InvalidOperationException(
							String.Format("Subscription group {0} on stream {1} already exists", _groupName, _stream)));
						break;
					default:
						throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
				}
			}

			protected override PersistentSubscriptionCreateResult TransformResponse(
				ClientMessage.CreatePersistentSubscriptionToStreamCompleted response) {
				return new PersistentSubscriptionCreateResult((PersistentSubscriptionCreateStatus)response.Result);
			}
		}

		internal class UpdatePersistentSubscription :
			EmbeddedResponderBase<PersistentSubscriptionUpdateResult,
				ClientMessage.UpdatePersistentSubscriptionToStreamCompleted> {
			private readonly string _stream;
			private readonly string _groupName;

			public UpdatePersistentSubscription(TaskCompletionSource<PersistentSubscriptionUpdateResult> source,
				string stream, string groupName)
				: base(source) {
				_groupName = groupName;
				_stream = stream;
			}

			protected override void InspectResponse(ClientMessage.UpdatePersistentSubscriptionToStreamCompleted response) {
				switch (response.Result) {
					case ClientMessage.UpdatePersistentSubscriptionToStreamCompleted.UpdatePersistentSubscriptionToStreamResult.Success:
						Succeed(response);
						break;
					case ClientMessage.UpdatePersistentSubscriptionToStreamCompleted.UpdatePersistentSubscriptionToStreamResult.Fail:
						Fail(new InvalidOperationException(String.Format(
							"Subscription group {0} on stream {1} failed '{2}'", _groupName, _stream,
							response.Reason)));
						break;
					case ClientMessage.UpdatePersistentSubscriptionToStreamCompleted.UpdatePersistentSubscriptionToStreamResult
						.AccessDenied:
						Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.",
							_stream)));
						break;
					case ClientMessage.UpdatePersistentSubscriptionToStreamCompleted.UpdatePersistentSubscriptionToStreamResult
						.DoesNotExist:
						Fail(new InvalidOperationException(
							String.Format("Subscription group {0} on stream {1} does not exist", _groupName, _stream)));
						break;
					default:
						throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
				}
			}

			protected override PersistentSubscriptionUpdateResult TransformResponse(
				ClientMessage.UpdatePersistentSubscriptionToStreamCompleted response) {
				return new PersistentSubscriptionUpdateResult((PersistentSubscriptionUpdateStatus)response.Result);
			}
		}

		internal class DeletePersistentSubscription :
			EmbeddedResponderBase<PersistentSubscriptionDeleteResult,
				ClientMessage.DeletePersistentSubscriptionToStreamCompleted> {
			private readonly string _stream;
			private readonly string _groupName;

			public DeletePersistentSubscription(TaskCompletionSource<PersistentSubscriptionDeleteResult> source,
				string stream, string groupName)
				: base(source) {
				_groupName = groupName;
				_stream = stream;
			}

			protected override void InspectResponse(ClientMessage.DeletePersistentSubscriptionToStreamCompleted response) {
				switch (response.Result) {
					case ClientMessage.DeletePersistentSubscriptionToStreamCompleted.DeletePersistentSubscriptionToStreamResult.Success:
						Succeed(response);
						break;
					case ClientMessage.DeletePersistentSubscriptionToStreamCompleted.DeletePersistentSubscriptionToStreamResult.Fail:
						Fail(new InvalidOperationException(String.Format(
							"Subscription group {0} on stream {1} failed '{2}'", _groupName, _stream,
							response.Reason)));
						break;
					case ClientMessage.DeletePersistentSubscriptionToStreamCompleted.DeletePersistentSubscriptionToStreamResult
						.AccessDenied:
						Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.",
							_stream)));
						break;
					case ClientMessage.DeletePersistentSubscriptionToStreamCompleted.DeletePersistentSubscriptionToStreamResult
						.DoesNotExist:
						Fail(new InvalidOperationException(
							String.Format("Subscription group {0} on stream {1} does not exist", _groupName, _stream)));
						break;
					default:
						throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
				}
			}

			protected override PersistentSubscriptionDeleteResult TransformResponse(
				ClientMessage.DeletePersistentSubscriptionToStreamCompleted response) {
				return new PersistentSubscriptionDeleteResult((PersistentSubscriptionDeleteStatus)response.Result);
			}
		}
	}
}
