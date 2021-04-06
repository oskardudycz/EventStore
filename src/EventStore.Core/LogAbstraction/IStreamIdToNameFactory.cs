namespace EventStore.Core.LogAbstraction {
	public interface IStreamIdToNameFactory<T> {
		IStreamIdToName<T> Create(object input = null);
	}
}
